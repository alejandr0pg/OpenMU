# 17 - Bugs de Daño, Muerte, Escudo y Experiencia

**Severidad:** CRÍTICA a MEDIA
**CWE:** CWE-190 (Integer Overflow), CWE-682 (Incorrect Calculation), CWE-191 (Integer Underflow)

---

## CRÍTICO 1: Daño negativo puede CURAR al enemigo

**Archivo:** `src/GameLogic/AttackableExtensions.cs:118,141,147,162,176,191`

### El flujo de cálculo de daño

```csharp
// Paso 1: Daño base - defensa (puede ser NEGATIVO)
dmg = (int)((dmg * duelDmgDec) - defense);        // Línea 118

// Paso 2: Si dmg es negativo, las reducciones porcentuales lo INVIERTEN:
dmg -= (int)(dmg * WeaknessPhysDmgDecrement);      // Línea 176
// Si dmg = -500 y WeaknessPhysDmgDecrement = 0.3:
//   -500 - (-500 * 0.3) = -500 - (-150) = -500 + 150 = -350
// (menos negativo, va hacia positivo)

dmg -= (int)(dmg * ArmorDamageDecrease);            // Línea 191
// Otro paso que acerca dmg a 0 o lo cruza a positivo

// Paso 3: Mínimo check (línea 196-198)
var minLevelDmg = Math.Max(1, (int)attackerLevel / 10);
if (dmg < minLevelDmg)
    dmg = minLevelDmg;
// ← ESTO PREVIENE daño < 1... PERO:

// Paso 4: Multiplicadores POST-mínimo (línea 201-204)
dmg = (int)(dmg * AttackDamageIncrease);            // Línea 201
dmg = (int)(dmg * DamageReceiveDecrement);          // Línea 204
```

### El bug real

El check de mínimo en línea 196 **SÍ protege** contra daño negativo al final, PERO:

Hay una ventana entre los pasos donde `dmg` es negativo y se usa en cálculos intermedios que producen resultados incorrectos:

```
dmg = 100 (base) - 500 (defense) = -400
dmg -= (-400 * 0.3) = -400 + 120 = -280
dmg -= (-280 * 0.2) = -280 + 56 = -224

minLevelDmg = max(1, 400/10) = 40
dmg = max(40, -224) → dmg = 40  ← Corregido al final

PERO en línea 201:
dmg = (int)(40 * AttackDamageIncrease)
// Si AttackDamageIncrease = 1.5 → dmg = 60

dmg = (int)(60 * DamageReceiveDecrement)
// Si DamageReceiveDecrement = 0.0 → dmg = 0  ← AHORA EL DAÑO ES 0

// Un jugador con DamageReceiveDecrement = 0 es INVULNERABLE
```

### Escenario de invulnerabilidad

Si `DamageReceiveDecrement` = 0 (o si algún buff lo pone a 0):

```csharp
dmg = (int)(dmg * defender.Attributes[Stats.DamageReceiveDecrement]);
// dmg * 0 = 0 → daño SIEMPRE es 0
```

### Fix

```csharp
// Aplicar mínimo DESPUÉS de todas las multiplicaciones:
dmg = (int)(dmg * attacker.Attributes[Stats.AttackDamageIncrease]);
dmg = (int)(dmg * defender.Attributes[Stats.DamageReceiveDecrement]);

// Mínimo final (no intermedio):
dmg = Math.Max(1, dmg);  // Siempre al menos 1 de daño
```

---

## CRÍTICO 2: Mana puede ser negativa — ManaToll sin bounds

**Archivo:** `src/GameLogic/Player.cs:722`

```csharp
this.Attributes[Stats.CurrentMana] =
    (manaFullyRecovered ? this.Attributes[Stats.MaximumMana] : this.Attributes[Stats.CurrentMana])
    - hitInfo.ManaToll;
```

### El bug

`ManaToll` es `uint`. Si `CurrentMana = 50` y `ManaToll = 200`:

```
CurrentMana - ManaToll = 50 - 200

Pero Attributes almacena como float/double internamente.
50.0 - 200.0 = -150.0 → Mana = -150
```

### Consecuencias de mana negativa

1. **Regeneración rota:** Si el sistema regenera `+10 mana/tick`, con mana -150 tarda 15 ticks para llegar a 0
2. **Skills gratuitos:** Si algún check hace `if (CurrentMana > 0)` en vez de `if (CurrentMana >= skillCost)`, un jugador con mana -150 puede usar skills que cuestan < 0 (ninguno, pero edge case)
3. **Attribute system corruption:** Valores negativos en atributos pueden causar cálculos incorrectos en cascada

### Fix

```csharp
if (hitInfo.ManaToll > 0)
{
    var currentMana = manaFullyRecovered
        ? this.Attributes[Stats.MaximumMana]
        : this.Attributes[Stats.CurrentMana];

    this.Attributes[Stats.CurrentMana] = Math.Max(0, currentMana - hitInfo.ManaToll);
}
```

---

## CRÍTICO 3: Shield overflow transfiere daño incorrecto a health

**Archivo:** `src/GameLogic/Player.cs:2092-2096`

```csharp
int oversd = (int)(this.Attributes![Stats.CurrentShield] - hitInfo.ShieldDamage);
if (oversd < 0)
{
    this.Attributes[Stats.CurrentShield] = 0;
    healthDamage += (uint)(oversd * (-1));  // ← Cast uint de valor negativo negado
}
```

### El bug

`CurrentShield` es float/double en el attribute system. `ShieldDamage` es uint.

```
CurrentShield = 100.5 (float)
ShieldDamage = 300 (uint)

oversd = (int)(100.5 - 300) = (int)(-199.5) = -199 (truncado)

healthDamage += (uint)(-199 * -1) = (uint)(199) = 199
// Esto es CORRECTO en este caso

PERO:
CurrentShield = 0.5 (casi vacío)
ShieldDamage = 2,147,483,700 (cerca de uint max)

oversd = (int)(0.5 - 2147483700) = (int)(-2147483699.5)
// (int) cast de un valor < int.MinValue = OVERFLOW → resultado impredecible

oversd * (-1) = ??? → healthDamage += ??? → daño incorrecto
```

### Escenario real

Con Soul Barrier activo y un ataque masivo:
- Si ShieldDamage es enorme (ataque amplificado), el cast a `int` puede hacer overflow
- `oversd` se convierte en un valor positivo por overflow
- La condición `oversd < 0` NO se cumple
- El escudo queda con un valor positivo incorrecto
- El jugador **no recibe daño en health** → invulnerable

### Fix

```csharp
double shieldAfterDamage = this.Attributes[Stats.CurrentShield] - hitInfo.ShieldDamage;
if (shieldAfterDamage < 0)
{
    this.Attributes[Stats.CurrentShield] = 0;
    healthDamage += (uint)Math.Abs(shieldAfterDamage);  // Usar Math.Abs con double
}
else
{
    this.Attributes[Stats.CurrentShield] = (float)shieldAfterDamage;
}
```

---

## ALTA 4: Health -= damage sin bounds check

**Archivo:** `src/GameLogic/Player.cs:2103`

```csharp
this.Attributes[Stats.CurrentHealth] -= healthDamage;
```

### El bug

Health se resta directamente sin verificar que no sea negativa. El death check en línea 2112 (`if (this.Attributes[Stats.CurrentHealth] < 1)`) lo detecta, pero entre las líneas 2103 y 2112, health es negativa.

Si cualquier otro código lee health entre estas líneas (ej: regeneración tick, buff check), obtiene un valor negativo que puede causar cálculos incorrectos.

---

## ALTA 5: Death penalty solo por monstruos — exploit PvP

**Archivo:** `src/GameLogic/PlugIns/PlayerLosesExperienceAfterDeathPlugIn.cs:29-31`

```csharp
if (killer is not Monster || killed is not Player player || ...)
{
    return;  // Solo monstruos causan penalty
}
```

### Cómo explotar

```
Escenario: Jugador en zona peligrosa, a punto de morir por monstruo

1. Jugador A está peleando contra monstruo fuerte
2. Jugador A va a morir (perdería 5% de experiencia)
3. Amigo de A (Jugador B) lo mata con un ataque PvP
4. Como el killer es Player (no Monster) → SIN penalty de experiencia
5. A revive sin perder experiencia

Resultado: Evasión total de death penalty usando PvP cooperativo
```

### Fix

```csharp
// Aplicar penalty por cualquier muerte en zona PvE:
if (killed is not Player player)
    return;

if (player.CurrentMiniGame is not null)
    return;

// Aplicar penalty independiente de quién mató
var expLoss = CalculateExperienceLoss(player);
player.AddExperience(-expLoss, null);
```

---

## ALTA 6: Experiencia recursiva puede crashear el servidor

**Archivo:** `src/GameLogic/Player.cs:1260-1268`

```csharp
var remainingExp = experience - exp;
if (remainingExp > 0 && this.Attributes![Stats.Level] < this.GameContext.Configuration.MaximumLevel)
{
    if (!this.GameContext.Configuration.PreventExperienceOverflow)
    {
        await this.AddExperienceAsync((int)remainingExp, killedObject);  // ← RECURSIÓN
    }
}
```

### El bug

Si `PreventExperienceOverflow = false` y la experiencia es suficiente para múltiples level-ups, `AddExperienceAsync` se llama recursivamente. Con experiencia masiva:

```
Exp para level up: 1,000
Exp ganada: 1,000,000
→ 1000 llamadas recursivas → StackOverflowException
```

### Fix

```csharp
// Usar loop en vez de recursión:
while (remainingExp > 0 && this.Attributes![Stats.Level] < maxLevel)
{
    var expForNextLevel = GetExpForNextLevel();
    if (remainingExp >= expForNextLevel)
    {
        await LevelUpAsync();
        remainingExp -= expForNextLevel;
    }
    else
    {
        this.CurrentExperience += remainingExp;
        break;
    }
}
```

---

## MEDIA 7: Respawn durante trade — estado inconsistente

**Archivo:** `src/GameLogic/Player.cs:2175-2214`

```csharp
this.IsAlive = false;
this._respawnAfterDeathCts = new CancellationTokenSource();
await Task.Delay(3000, cancellationToken);  // Respawn en 3 segundos
```

El respawn corre en un `Task.Delay` background. Si el jugador está en trade al morir:
- La muerte pone `IsAlive = false`
- El trade sigue abierto (no se cancela automáticamente)
- Al respawnear, el jugador está vivo y en trade
- Posible duplicación si los items están en `TemporaryStorage`

---

## Tabla resumen

| # | Bug | Tipo | Impacto | Archivo:Línea |
|---|-----|------|---------|---------------|
| 1 | DamageReceiveDecrement = 0 | Calc Error | **Invulnerabilidad** | AttackableExtensions.cs:204 |
| 2 | Mana negativa por ManaToll | Integer Underflow | **Attribute corruption** | Player.cs:722 |
| 3 | Shield overflow → int cast | Integer Overflow | **Invulnerabilidad** | Player.cs:2092 |
| 4 | Health negativa | Missing Bounds | Cálculos incorrectos | Player.cs:2103 |
| 5 | Death penalty solo Monster | Logic Error | **Evadir penalties** | PlayerLosesExp...cs:31 |
| 6 | Experience recursión | Stack Overflow | **Server crash** | Player.cs:1266 |
| 7 | Respawn durante trade | State Inconsistency | Item exploit | Player.cs:2175 |
