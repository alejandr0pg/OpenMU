# 13 - Bugs de Crafting, Upgrades, Consumibles y Vault

**Severidad:** CRÍTICA a MEDIA
**CWE:** CWE-190 (Integer Overflow), CWE-362 (Race Condition), CWE-20 (Input Validation)

---

## CRÍTICO 1: Byte underflow en downgrade de items — Level se vuelve 252-254

**Archivo:** `src/GameLogic/PlayerActions/Items/BaseItemCraftingHandler.cs:177-181`

```csharp
case MixResult.ThirdWingsDowngradedRandom:
    itemLink.Items.ForEach(item =>
    {
        var previousLevel = item.Level;
        item.Level -= (byte)(Rand.NextRandomBool() ? 2 : 3);
        // Si item.Level es 0 o 1:
        //   0 - 2 = -2 → como byte = 254
        //   1 - 3 = -2 → como byte = 254
        //   0 - 3 = -3 → como byte = 253
```

### El bug

`item.Level` es `byte` (0-255). La resta no tiene `checked` ni validación de mínimo. Cuando el resultado es negativo, el byte hace **wraparound**:

```
Level = 0, resta 2:  (byte)(0 - 2) = (byte)(-2) = 254
Level = 1, resta 3:  (byte)(1 - 3) = (byte)(-2) = 254
Level = 2, resta 3:  (byte)(2 - 3) = (byte)(-1) = 255
```

### Cómo explotar

1. Obtener Third Wings level 0 (o crear condiciones para ello)
2. Ir al Chaos Machine y hacer un mix que falle con resultado `ThirdWingsDowngradedRandom`
3. El item pasa de level 0 a level **254 o 255**
4. Un item level 255 tendría stats astronómicos

### Alcance del bug

El mismo patrón existe en **línea 147** para `ChaosWeaponAndFirstWingsDowngradedRandom`:
```csharp
item.Level = (byte)Rand.NextInt(0, previousLevel);
```
Este caso es seguro porque usa `NextInt(0, previousLevel)` que siempre da >= 0.

**PERO** el caso de Third Wings en línea 181 es vulnerable.

### Fix

```csharp
case MixResult.ThirdWingsDowngradedRandom:
    itemLink.Items.ForEach(item =>
    {
        var previousLevel = item.Level;
        var reduction = (byte)(Rand.NextRandomBool() ? 2 : 3);
        item.Level = previousLevel > reduction ? (byte)(previousLevel - reduction) : (byte)0;
        // ...
    });
```

---

## ALTA 2: Crafting con items de otro storage — TemporaryStorage sin ownership

**Archivo:** `src/GameLogic/PlayerActions/Items/SimpleItemCraftingHandler.cs:36-45`

```csharp
var storage = player.TemporaryStorage?.Items.ToList() ?? new List<Item>();
foreach (var requiredItem in this._settings.RequiredItems.OrderByDescending(i => i.MinimumAmount))
{
    var foundItems = storage.Where(item => this.RequiredItemMatches(item, requiredItem)).ToList();
    var itemCount = foundItems.Sum(i => i.IsStackable() ? i.Durability : 1);
    if (itemCount < requiredItem.MinimumAmount)
    {
        return CraftingResult.LackingMixItems;
    }
}
```

### El bug

Los items en `TemporaryStorage` se validan solo por tipo y cantidad. No se verifica:
- Que el jugador sea dueño real de los items
- Que los items vengan del inventario del jugador y no fueron inyectados via paquetes
- Que los items no fueron modificados entre que se movieron al TemporaryStorage y el craft

### Cómo explotar

1. Abrir diálogo con NPC de Chaos Machine
2. Mover items al TemporaryStorage
3. Via paquete manipulado, intentar mover items que no posees (de otro storage o con datos modificados)
4. Si el MoveItemAction no valida correctamente (ver bug de collision detection en Doc 10), items fantasma pueden terminar en TemporaryStorage
5. Craftear con items que no deberías tener

---

## ALTA 3: Race condition en craft success/failure — items consumidos pero craft falla

**Archivo:** `src/GameLogic/PlayerActions/Items/BaseItemCraftingHandler.cs:42-71`

```csharp
var success = Rand.NextRandomBool(successRate);
if (success)
{
    if (await this.DoTheMixAsync(items, player, socketSlot, successRate) is { } item)
    {
        // ... éxito ...
        return (CraftingResult.Success, item);
    }

    // DoTheMixAsync retornó null pero success era true
    return (CraftingResult.Failed, null);  // ← Items ya consumidos, sin resultado
}
```

### El bug

Si `DoTheMixAsync` determina `success = true` pero luego falla al crear el item resultado (inventario lleno, error de persistencia, etc.):
- Los items de entrada YA fueron consumidos/destruidos en las líneas anteriores
- El resultado se marca como `Failed`
- El jugador pierde sus items sin recibir nada

### Cuándo pasa

- Inventario lleno al momento de crear el resultado
- Error transitorio de base de datos
- Otro paquete modifica el inventario concurrentemente

### Fix

```csharp
// Crear el item resultado ANTES de destruir los inputs:
if (success)
{
    var resultItem = await this.DoTheMixAsync(items, player, socketSlot, successRate);
    if (resultItem is null)
    {
        // No destruir items si no podemos crear el resultado
        return (CraftingResult.Failed, null);
    }

    // Ahora sí destruir items de entrada
    await this.DestroyInputItemsAsync(items, player);
    return (CraftingResult.Success, resultItem);
}
```

---

## ALTA 4: ItemOption.Level++ sin bounds check

**Archivo:** `src/GameLogic/PlayerActions/ItemConsumeActions/ItemUpgradeConsumeHandlerPlugIn.cs:99-102`

```csharp
if (Rand.NextRandomBool(this.Configuration.SuccessChance))
{
    itemOption.Level++;  // Sin verificar máximo
}
```

### El bug

Aunque hay una pre-validación de `higherOptionPossible`, el `Level++` no verifica que el nuevo nivel tenga un `LevelDependentOption` definido. Si se ejecuta repetidamente:

```
Level 1 → 2 → 3 → 4 (si max es 4, ok)
Pero si la configuración tiene levels [1, 2, 4] (sin 3):
Level 2 → 3 (Level 3 no tiene LevelDependentOption definido)
→ El juego busca stats para level 3, no encuentra, posible NullReferenceException o stats de 0
```

### Fix

```csharp
var nextLevel = itemOption.Level + 1;
if (itemOption.ItemOption?.LevelDependentOptions.Any(o => o.Level == nextLevel) != true)
    return false;

itemOption.Level = nextLevel;
```

---

## ALTA 5: Stack overflow en durability — items infinitos

**Archivo:** `src/GameLogic/PlayerActions/Items/MoveItemAction.cs:97-107` (FullStack)

```csharp
private async ValueTask FullStackAsync(Player player, Item sourceItem, Item targetItem)
{
    targetItem.Durability += sourceItem.Durability;  // Sin verificar máximo
    // ...
}
```

### El bug

Al stackear items (ej: pociones), la durabilidad del item destino se incrementa sin verificar el máximo permitido. Si `targetItem.Durability = 200` y `sourceItem.Durability = 200`:

```
200 + 200 = 400 → si Durability es byte: 400 % 256 = 144 → PIERDE items
Si Durability es double/int: 400 → stack de 400 cuando max debería ser 255
```

### Fix

```csharp
var maxStack = targetItem.Definition?.Durability ?? 255;
var total = targetItem.Durability + sourceItem.Durability;
if (total > maxStack)
{
    sourceItem.Durability = (byte)(total - maxStack);
    targetItem.Durability = maxStack;
}
else
{
    targetItem.Durability = (byte)total;
    // Remove sourceItem
}
```

---

## MEDIA 6: BackupInventory incompleto en crafting — solo Inventory, no Vault

**Archivo:** `src/GameLogic/PlayerActions/TalkNpcAction.cs:141-144`

```csharp
if (npcStats.ItemCraftings.Any())
{
    player.BackupInventory = new BackupItemStorage(player.Inventory!.ItemStorage);
    // ← Solo respalda Inventory, no TemporaryStorage ni Vault
}
```

### El problema

Si un craft falla y se restaura el backup:
- Items del inventario se restauran correctamente
- Items del vault que se movieron al TemporaryStorage se pierden
- Items creados en TemporaryStorage durante el craft quedan huérfanos

---

## MEDIA 7: System.Random para craft success — predecible

**Archivo:** `src/GameLogic/Rand.cs:32-56`

```csharp
[ThreadStatic]
private static Random? RandomInstance;

public static bool NextRandomBool(int percent)
{
    return NextInt(0, 101) < percent;
}
```

### El problema

`System.Random` no es criptográficamente seguro. El seed se puede inferir si:
- El atacante puede observar múltiples resultados consecutivos
- El servidor se reinicia y el seed es predecible (basado en tiempo)
- El atacante tiene acceso al proceso del servidor

Con el seed, se puede predecir el resultado de CADA craft futuro y solo craftear cuando el RNG indica éxito.

### Fix

```csharp
// Para decisiones que afectan economía/items:
using System.Security.Cryptography;

public static bool NextSecureRandomBool(int percent)
{
    return RandomNumberGenerator.GetInt32(0, 101) < percent;
}
```

---

## Tabla resumen

| # | Bug | Tipo | Impacto | Archivo:Línea |
|---|-----|------|---------|---------------|
| 1 | Byte underflow en downgrade | Integer Underflow | **Items level 254** | BaseItemCraftingHandler.cs:181 |
| 2 | TemporaryStorage sin ownership | Missing Validation | Craft con items ajenos | SimpleItemCraftingHandler.cs:36 |
| 3 | Craft success pero DoTheMix null | State Inconsistency | Pérdida de items | BaseItemCraftingHandler.cs:42 |
| 4 | Option Level++ sin bounds | Missing Bounds | Stats inválidos/crash | ItemUpgradeConsumeHandler.cs:101 |
| 5 | Stack overflow en durability | Integer Overflow | Items infinitos/perdidos | MoveItemAction.cs:97 |
| 6 | Backup incompleto en craft | State Inconsistency | Items perdidos en fail | TalkNpcAction.cs:141 |
| 7 | System.Random predecible | Weak Randomness | Predecir craft success | Rand.cs:32 |
