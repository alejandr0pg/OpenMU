# 10 - Bugs de Lógica de Negocio: Trade, Items, Shops

**Severidad:** CRÍTICA a ALTA
**CWE:** CWE-362 (Race Condition), CWE-190 (Integer Overflow), CWE-20 (Input Validation)

---

## CRÍTICO 1: Bug de duplicación de items — Collision Detection rota

**Archivo:** `src/GameLogic/PlayerActions/Items/MoveItemAction.cs:306-320`

```csharp
for (var i = toStorage.StartIndex; i < toStorage.EndIndex; i++)
{
    if (toStorage.StartIndex == fromSlot && sameStorage)  // BUG: debería ser 'i'
    {
        continue;
    }

    var blockingItem = toStorage.Storage.GetItem(toStorage.StartIndex);  // BUG: debería ser 'i'
    if (blockingItem is null)
    {
        continue;
    }

    this.SetUsedSlots(toStorage, blockingItem, usedSlots);
}
```

### El bug

Hay **dos errores** en este loop:

1. **Línea 308:** `toStorage.StartIndex == fromSlot` debería ser `i == fromSlot`. Actualmente solo compara el primer slot, no el slot del iterador.

2. **Línea 313:** `toStorage.Storage.GetItem(toStorage.StartIndex)` debería ser `toStorage.Storage.GetItem(i)`. El loop itera sobre todos los slots, pero **siempre consulta el mismo slot** (el primero).

### Qué pasa en la práctica

El array `usedSlots[]` que marca qué posiciones del inventario están ocupadas solo registra el item del **primer slot**. Todos los demás items son invisibles para la detección de colisiones.

```
Inventario real:
[Slot 0: Espada]  [Slot 1: Escudo]  [Slot 2: Armadura]  [Slot 3: Vacío]

Lo que ve el collision detection:
[Slot 0: Espada]  [Slot 1: ???]  [Slot 2: ???]  [Slot 3: ???]
                     ↑ Solo revisa Slot 0 en cada iteración
```

### Cómo explotar

1. Jugador tiene inventario lleno excepto en posiciones que "solapan" con items existentes
2. Envía paquete para mover un item a una posición que DEBERÍA estar bloqueada
3. El servidor aprueba el movimiento porque la collision detection solo ve el primer item
4. Resultado: **dos items ocupan el mismo espacio**, posible duplicación al guardar

### Fix

```csharp
for (var i = toStorage.StartIndex; i < toStorage.EndIndex; i++)
{
    if (i == fromSlot && sameStorage)  // FIX: usar 'i'
    {
        continue;
    }

    var blockingItem = toStorage.Storage.GetItem(i);  // FIX: usar 'i'
    if (blockingItem is null)
    {
        continue;
    }

    this.SetUsedSlots(toStorage, blockingItem, usedSlots);
}
```

**Nota adicional:** En `AreTargetSlotsBlocked` (línea 327-333) hay otro loop sospechoso que itera pero siempre llama `IsTargetSlotBlocked` con los mismos parámetros — parece ser código que debería validar múltiples slots pero solo verifica uno.

---

## CRÍTICO 2: Trade money bypass — Integer overflow en cast uint→int

**Archivo:** `src/GameLogic/PlayerActions/Trade/TradeMoneyAction.cs:36-38`

```csharp
public async ValueTask TradeMoneyAsync(Player player, uint moneyAmount)
{
    // moneyAmount es uint (0 a 4,294,967,295)

    player.TryAddMoney(player.TradingMoney);         // Devuelve el money anterior
    player.TryAddMoney((int)(-1 * moneyAmount));      // ← OVERFLOW AQUÍ
    player.TradingMoney = (int)moneyAmount;            // ← OVERFLOW AQUÍ
}
```

### El bug

`moneyAmount` es `uint` (unsigned 32-bit). Se multiplica por `-1` y se castea a `int` (signed 32-bit):

```
Si moneyAmount = 2,147,483,648 (uint, válido)
   -1 * 2,147,483,648 = -2,147,483,648 (long)
   (int)(-2,147,483,648) = -2,147,483,648 (int.MinValue)

Pero: TryAddMoney recibe int, y verifica:
   if (this.Money + value < 0) return false;

Si Money = 100:
   100 + (-2,147,483,648) = -2,147,483,548 < 0 → falla ✓ (protegido)

PERO si moneyAmount = 2,147,483,649:
   -1 * 2,147,483,649 = -2,147,483,649 (long)
   (int)(-2,147,483,649) = 2,147,483,647 (¡POSITIVO! overflow)

Ahora TryAddMoney recibe +2,147,483,647
   → El jugador GANA dinero en lugar de perderlo
```

### Cómo explotar

1. Abrir trade con otro jugador
2. Enviar paquete con `moneyAmount = 2147483649` (0x80000001)
3. El cast produce un número positivo
4. `TryAddMoney` suma en vez de restar
5. El jugador gana ~2 mil millones de zen gratis

### Fix

```csharp
public async ValueTask TradeMoneyAsync(Player player, uint moneyAmount)
{
    if (player.PlayerState.CurrentState != PlayerState.TradeOpened)
        return;

    // Validar que el monto cabe en int positivo
    if (moneyAmount > int.MaxValue)
        return;

    int amount = (int)moneyAmount;

    if (player.Money < amount)
        return;

    // Devolver trading money anterior
    if (player.TradingMoney > 0)
        player.TryAddMoney(player.TradingMoney);

    // Restar nuevo monto
    if (!player.TryRemoveMoney(amount))
        return;

    player.TradingMoney = amount;
    // ... notificaciones UI
}
```

---

## CRÍTICO 3: Trade money sin TryAddMoney — bypass de límite máximo

**Archivo:** `src/GameLogic/PlayerActions/Trade/TradeButtonAction.cs:101-102`

```csharp
trader.Money += trader.TradingPartner.TradingMoney;
trader.TradingPartner.Money += trader.TradingMoney;
```

### El bug

Usa `+=` directo en vez de `TryAddMoney()`. Compara:

```csharp
// TryAddMoney (seguro):
if (this.Money + value > MaximumInventoryMoney) return false;  // ← Verifica límite
if (this.Money + value < 0) return false;                      // ← Verifica underflow
this.Money = checked(this.Money + value);                      // ← checked overflow

// El código del trade:
trader.Money += trader.TradingPartner.TradingMoney;  // Sin verificar NADA
```

### Cómo explotar

1. Jugador A tiene 2,000,000,000 zen (cerca del máximo)
2. Jugador B pone 1,000,000,000 zen en trade
3. Trade se completa
4. `trader.Money += 1,000,000,000` → 3,000,000,000 — supera el máximo configurado
5. O si `MaximumInventoryMoney` es 2,147,483,647 y la suma da 3 mil millones → `int` overflow → número negativo

### Fix

```csharp
if (!trader.TryAddMoney(trader.TradingPartner.TradingMoney))
{
    // Cancelar trade - receptor no tiene espacio para el dinero
    await CancelTradeAsync(trader);
    return TradeResult.Cancelled;
}

if (!trader.TradingPartner.TryAddMoney(trader.TradingMoney))
{
    // Revertir la primera operación
    trader.TryAddMoney(-trader.TradingPartner.TradingMoney);
    await CancelTradeAsync(trader);
    return TradeResult.Cancelled;
}
```

---

## ALTA 4: Race condition en Player Shop — doble compra

**Archivo:** `src/GameLogic/PlayerActions/PlayerStore/BuyRequestAction.cs:34-120`

### El flujo vulnerable

```
Comprador A                    Servidor                     Comprador B
    |                            |                              |
    |-- BuyItem(slot=5) -------->|                              |
    |                            |<-------- BuyItem(slot=5) ----|
    |                            |                              |
    |   [Verifica item existe]   |   [Verifica item existe]     |
    |   [item != null ✓]         |   [item != null ✓]           |
    |                            |                              |
    |   [Espera lock...]         |   [Espera lock...]           |
    |   [Obtiene lock]           |                              |
    |   [Re-verifica item ✓]     |                              |
    |   [Cobra dinero A]         |                              |
    |   [Transfiere item]        |                              |
    |   [Libera lock]            |                              |
    |                            |   [Obtiene lock]             |
    |                            |   [Re-verifica item → NULL]  |
    |                            |   [PERO dinero ya cobrado?]  |
```

El lock dentro de `BuyRequestAction` protege la transferencia, pero el flujo completo de "verificar precio → cobrar → transferir" no es atómico. Si el item fue vendido a A, B no obtiene item pero ¿se le devuelve el dinero?

Revisando el código: la re-verificación dentro del lock (línea 83-87) retorna si item es null **ANTES** de cobrar dinero. Esto parece correcto. Pero hay un edge case:

```csharp
if (player.TryRemoveMoney(itemPrice))  // Cobra al comprador
{
    if (requestedPlayer.TryAddMoney(itemPrice))  // Paga al vendedor
    {
        // Transferir item...
    }
    // Si TryAddMoney falla (vendedor tiene max money)
    // → Comprador perdió dinero, vendedor no lo recibió
    // → NO HAY ROLLBACK del TryRemoveMoney
}
```

### Fix

```csharp
if (player.TryRemoveMoney(itemPrice))
{
    if (requestedPlayer.TryAddMoney(itemPrice))
    {
        // Transferir item
    }
    else
    {
        // ROLLBACK: devolver dinero al comprador
        player.TryAddMoney(itemPrice);
        return;
    }
}
```

---

## ALTA 5: Trade cancel — race condition en BackupInventory

**Archivo:** `src/GameLogic/PlayerActions/Trade/BaseTradeAction.cs:38-61`

```csharp
protected async ValueTask CancelTradeAsync(ITrader trader, bool checkState = true)
{
    trader.TradingMoney = 0;
    if (trader.BackupInventory != null)  // Thread A lee: no null
    {                                     // Thread B también lee: no null
        trader.Inventory!.Clear();
        trader.BackupInventory.RestoreItemStates();
        foreach (var item in trader.BackupInventory.Items)
        {
            await trader.Inventory.AddItemAsync(item.ItemSlot, item);
        }
        trader.BackupInventory = null;  // Thread A pone null
    }                                    // Thread B: BackupInventory ya es null
                                         // → Inventory ya fue cleared pero items no restaurados
```

### Cómo explotar

1. Jugador A y B están en trade
2. A envía paquete de cancelar trade
3. Inmediatamente A envía otro paquete de cancelar (o B también cancela)
4. Ambas cancelaciones procesan concurrentemente
5. Posible resultado: inventario queda vacío (items perdidos) o items duplicados

### Fix

```csharp
private readonly SemaphoreSlim _cancelLock = new(1, 1);

protected async ValueTask CancelTradeAsync(ITrader trader, bool checkState = true)
{
    await _cancelLock.WaitAsync();
    try
    {
        if (trader.BackupInventory == null) return; // Ya cancelado

        trader.TradingMoney = 0;
        trader.Inventory!.Clear();
        trader.BackupInventory.RestoreItemStates();
        foreach (var item in trader.BackupInventory.Items)
        {
            await trader.Inventory.AddItemAsync(item.ItemSlot, item);
        }
        trader.Inventory.ItemStorage.Money = trader.BackupInventory.Money;
        trader.BackupInventory = null;
    }
    finally
    {
        _cancelLock.Release();
    }
}
```

---

## ALTA 6: Shop slot sin validar máximo

**Archivo:** `src/GameLogic/PlayerActions/PlayerStore/BuyRequestAction.cs:41-45`

```csharp
if (slot < InventoryConstants.FirstStoreItemSlotIndex)
{
    player.Logger.LogWarning("Store Slot too low: {0}, possible hacker", slot);
    return;
}
// ¡FALTA verificar el máximo!
```

### Cómo explotar

Solo valida que `slot >= FirstStoreItemSlotIndex` (30), pero no que `slot < FirstStoreItemSlotIndex + StoreSize` (62). Un comprador podría enviar `slot = 200` y potencialmente acceder a items fuera de la tienda del jugador.

### Fix

```csharp
if (slot < InventoryConstants.FirstStoreItemSlotIndex
    || slot >= InventoryConstants.FirstStoreItemSlotIndex + InventoryConstants.StoreSize)
{
    player.Logger.LogWarning("Store Slot out of range: {0}", slot);
    return;
}
```

---

## MEDIA 7: Persistencia no transaccional en trade

**Archivo:** `src/GameLogic/PlayerActions/Trade/TradeButtonAction.cs:94-109`

```csharp
// Paso 1: Detach items del contexto original
this.DetachItemsFromPersistenceContext(traderItems, trader.PersistenceContext);
this.DetachItemsFromPersistenceContext(tradePartnerItems, trader.TradingPartner!.PersistenceContext);

// Paso 2: Guardar cambios
await itemContext.SaveChangesAsync();  // ← Si esto falla...

// Paso 3: Attach items al nuevo contexto
this.AttachItemsToPersistenceContext(traderItems, trader.TradingPartner.PersistenceContext);

// Paso 4: Actualizar dinero (sin persistir)
trader.Money += trader.TradingPartner.TradingMoney;
```

### El problema

Si `SaveChangesAsync()` falla entre Detach y Attach:
- Items están detachados de ambos contextos
- No pertenecen a ningún jugador en la BD
- Los items se "pierden" hasta el próximo restart

Si el servidor crashea entre Paso 2 y Paso 4:
- Items transferidos pero dinero no actualizado
- Un jugador tiene items + dinero, el otro perdió ambos

### Fix

Usar una transacción de base de datos:

```csharp
await using var transaction = await itemContext.Database.BeginTransactionAsync();
try
{
    // ... transferencias ...
    await itemContext.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    // Restaurar estado original
}
```

---

## Tabla resumen

| # | Bug | Tipo | Impacto | Archivo:Línea |
|---|-----|------|---------|---------------|
| 1 | Collision detection usa slot fijo | Logic Error | Dupe items | MoveItemAction.cs:308,313 |
| 2 | uint→int overflow en trade money | Integer Overflow | Zen infinito | TradeMoneyAction.cs:37 |
| 3 | Money += sin validar en trade | Missing Validation | Bypass max money | TradeButtonAction.cs:101 |
| 4 | Sin rollback en shop buy | Missing Rollback | Pérdida de dinero | BuyRequestAction.cs:91-109 |
| 5 | BackupInventory race condition | Race Condition | Dupe/pérdida items | BaseTradeAction.cs:38-61 |
| 6 | Shop slot sin máximo | Missing Bounds | Acceso fuera de tienda | BuyRequestAction.cs:41 |
| 7 | Trade sin transacción | Non-atomic Op | Pérdida de items | TradeButtonAction.cs:94 |
