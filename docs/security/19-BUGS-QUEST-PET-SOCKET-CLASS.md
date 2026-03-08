# 19 - Bugs de Quests, Mascotas, Sockets y Evolución de Clase

**Severidad:** CRÍTICA a ALTA
**CWE:** CWE-670 (Always-Incorrect Control Flow), CWE-362 (Race Condition), CWE-20 (Improper Input Validation)

---

## CRÍTICO 1: Pet Raven — durability check no tiene efecto

**Archivo:** `src/GameLogic/Pet/RavenCommandManager.cs:58-63`

```csharp
if (this._pet.Durability == 0.0)
{
    this._currentBehaviour = PetBehaviour.Idle;  // ← Se asigna Idle
}

this._currentBehaviour = newBehaviour;  // ← SOBREESCRITO inmediatamente
```

### El bug

La línea 63 se ejecuta **incondicionalmente**, sobreescribiendo la asignación de Idle que protege contra mascotas con durabilidad 0. El Raven puede atacar sin durabilidad.

### Escenario de exploit

```
1. Equipar Dark Raven (durabilidad = 0)
2. Enviar comando PetBehaviour.AttackRandom
3. El check de durabilidad asigna Idle...
4. ...pero inmediatamente se sobreescribe con AttackRandom
5. El pet ataca sin durabilidad → daño gratis sin degradación
```

### Fix

```csharp
public async ValueTask SetBehaviourAsync(PetBehaviour newBehaviour, IAttackable? target)
{
    await (this._attackCts?.CancelAsync() ?? Task.CompletedTask).ConfigureAwait(false);
    this._attackCts?.Dispose();
    this._attackCts = null;

    if (this._pet.Durability == 0.0)
    {
        this._currentBehaviour = PetBehaviour.Idle;
        await this._owner.InvokeViewPlugInAsync<IPetBehaviourChangedViewPlugIn>(
            p => p.PetBehaviourChangedAsync(this._pet, PetBehaviour.Idle, null));
        return;  // ← RETURN para evitar sobreescritura
    }

    this._currentBehaviour = newBehaviour;
    // ... resto del código
}
```

---

## CRÍTICO 2: Evolución de clase sin validación de generación

**Archivo:** `src/GameLogic/PlayerActions/Quests/QuestCompletionAction.cs:136-156`

```csharp
case QuestRewardType.CharacterEvolutionFirstToSecond:
    player.SelectedCharacter!.CharacterClass =
        player.SelectedCharacter.CharacterClass?.NextGenerationClass  // ← No valida generación actual
        ?? throw new InvalidOperationException("...");
    break;

case QuestRewardType.CharacterEvolutionSecondToThird:
    player.SelectedCharacter!.CharacterClass =
        player.SelectedCharacter.CharacterClass?.NextGenerationClass  // ← Mismo código exacto
        ?? throw new InvalidOperationException("...");
    break;
```

### El bug

Ambos reward types (`FirstToSecond` y `SecondToThird`) ejecutan **código idéntico**: `NextGenerationClass`. No hay validación de que el personaje esté en la generación correcta antes de evolucionar.

### Escenario de exploit

```
1. Personaje ya es 2da generación (ej: Blade Knight)
2. Si puede re-completar la quest de "FirstToSecond" (ver quest repeatable bug)
3. Se aplica NextGenerationClass → pasa a 3ra generación (Blade Master)
4. Saltó la quest de "SecondToThird" con todos sus requisitos
```

### Fix

```csharp
case QuestRewardType.CharacterEvolutionFirstToSecond:
    if (player.SelectedCharacter!.CharacterClass?.Generation != 0)
        throw new InvalidOperationException("Character is not first generation");
    player.SelectedCharacter.CharacterClass =
        player.SelectedCharacter.CharacterClass.NextGenerationClass
        ?? throw new InvalidOperationException("No next generation class");
    break;

case QuestRewardType.CharacterEvolutionSecondToThird:
    if (player.SelectedCharacter!.CharacterClass?.Generation != 1)
        throw new InvalidOperationException("Character is not second generation");
    player.SelectedCharacter.CharacterClass =
        player.SelectedCharacter.CharacterClass.NextGenerationClass
        ?? throw new InvalidOperationException("No next generation class");
    break;
```

---

## ALTA 3: Quest reward Money — sin verificación de retorno

**Archivo:** `src/GameLogic/PlayerActions/Quests/QuestCompletionAction.cs:163`

```csharp
case QuestRewardType.Money:
    player.TryAddMoney(reward.Value);  // ← Retorno ignorado
    await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync());
    break;
```

### El bug

`TryAddMoney` retorna `bool` indicando si la operación fue exitosa. El retorno se ignora. Si el jugador ya tiene el máximo de dinero, `TryAddMoney` retorna `false` pero el quest se marca como completado y los items de quest se consumen.

### Consecuencia

El jugador pierde los items de quest pero no recibe el dinero → pérdida de recursos.

---

## ALTA 4: Pet attack — fire-and-forget sin await

**Archivo:** `src/GameLogic/Pet/RavenCommandManager.cs:75,78,81`

```csharp
case PetBehaviour.AttackRandom:
    _ = this.AttackRandomAsync(this._attackCts.Token);        // ← Discarded Task
    break;
case PetBehaviour.AttackTarget when target is { }:
    _ = this.AttackTargetUntilDeathAsync(target, this._attackCts.Token);  // ← Discarded
    break;
case PetBehaviour.AttackWithOwner:
    _ = this.AttackSameAsOwnerAsync(this._attackCts.Token);   // ← Discarded
    break;
```

### El bug

Las tareas de ataque se lanzan como fire-and-forget (`_ =`). Aunque los métodos internos tienen `try-catch`, si alguna excepción escapa, se perderá silenciosamente. Además, el método `SetBehaviourAsync` retorna antes de que el ataque comience, permitiendo race conditions si se llama repetidamente.

### Escenario

```
1. Enviar SetBehaviour(AttackRandom)
2. Inmediatamente enviar SetBehaviour(AttackTarget, boss)
3. Ambas tareas corren concurrentemente
4. El pet ataca random Y al target al mismo tiempo
5. Doble DPS del pet
```

### Fix

```csharp
// Await la tarea anterior antes de lanzar nueva:
await (this._attackCts?.CancelAsync() ?? Task.CompletedTask);
// Esperar a que la tarea anterior termine:
if (this._currentAttackTask is not null)
{
    await this._currentAttackTask;
}
```

---

## ALTA 5: Mini-game entrada — dinero deducido sin verificar resultado

**Archivo:** `src/GameLogic/PlayerActions/MiniGames/EnterMiniGameAction.cs:106-108`

```csharp
if (enterResult == EnterResult.Success)
{
    if (entranceFee > 0)
    {
        player.TryRemoveMoney(entranceFee);  // ← Retorno IGNORADO
    }
```

### El bug

Si `TryRemoveMoney` falla (el jugador no tiene suficiente dinero), el jugador **entra al mini-game gratis**. El check de dinero debería ser antes de `TryEnterAsync` o el resultado debería verificarse.

### Fix

```csharp
if (entranceFee > 0 && !player.TryRemoveMoney(entranceFee))
{
    await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(
        p => p.ShowMessageAsync("Not enough money."));
    return;
}

var enterResult = await miniGame.TryEnterAsync(player);
```

---

## ALTA 6: Mini-game exit — estado inconsistente antes del lock

**Archivo:** `src/GameLogic/MiniGames/MiniGameContext.cs:407-410`

```csharp
player.CurrentMiniGame = null;                              // ← SIN LOCK
player.PlayerPickedUpItem -= this.OnPlayerPickedUpItemAsync; // ← SIN LOCK
bool cantGameProceed;
using (await this._enterLock.WriterLockAsync())              // ← LOCK aquí
{
    player.Died -= this.OnPlayerDied;
    this._enteredPlayers.Remove(player);
```

### El bug

`player.CurrentMiniGame = null` se ejecuta ANTES de adquirir el lock. Esto crea una ventana donde:
1. `CurrentMiniGame` es null (jugador "no está en mini-game")
2. Pero `_enteredPlayers` aún contiene al jugador (aún "dentro")

### Escenario

```
Thread A (salida):                Thread B (re-entrada):
1. CurrentMiniGame = null
                                  2. Check: CurrentMiniGame == null? ✓
                                  3. TryEnter → success (cree que no está)
4. _enteredPlayers.Remove()
                                  5. _enteredPlayers.Add() → jugador duplicado
```

---

## Tabla resumen

| # | Bug | Tipo | Impacto | Archivo:Línea |
|---|-----|------|---------|---------------|
| 1 | Pet durability check inefectivo | Logic Error | **Pet ataca gratis** | RavenCommandManager.cs:58-63 |
| 2 | Evolución sin validar generación | Missing Validation | **Saltar evolución** | QuestCompletionAction.cs:136-156 |
| 3 | Quest money retorno ignorado | Ignored Return | Pérdida de items | QuestCompletionAction.cs:163 |
| 4 | Pet attack fire-and-forget | Race Condition | **Doble DPS** | RavenCommandManager.cs:75-81 |
| 5 | Mini-game entrada sin pagar | Ignored Return | **Entrada gratis** | EnterMiniGameAction.cs:106-108 |
| 6 | Mini-game exit antes del lock | TOCTOU | Re-entrada duplicada | MiniGameContext.cs:407-410 |
