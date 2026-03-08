# 18 - Bugs de Concurrencia: Race Conditions Sistémicas

**Severidad:** CRÍTICA
**CWE:** CWE-362 (Race Condition), CWE-662 (Improper Synchronization), CWE-367 (TOCTOU)

---

Este documento cubre las **race conditions sistémicas** — bugs que afectan a todo el servidor porque están en las operaciones más fundamentales: dinero, inventario, party, y atributos.

## Concepto: Check-Then-Act sin atomicidad

El patrón más peligroso en código concurrente es:

```
Thread A                         Thread B
─────────                        ─────────
1. Leer valor (100)              1. Leer valor (100)
2. Verificar condición (≥ 50?)   2. Verificar condición (≥ 50?)
3. Modificar valor (100-50=50)   3. Modificar valor (100-50=50)

Resultado final: 50 (debería ser 0)
Thread B no vio la modificación de Thread A
```

---

## CRÍTICO 1: Player.Money — TODA operación de dinero es race condition

**Archivo:** `src/GameLogic/Player.cs:164-183, 965-979`

```csharp
public int Money
{
    get => this.SelectedCharacter?.Inventory?.Money ?? 0;
    set
    {
        this.SelectedCharacter.Inventory.Money = value;  // ← Sin lock
        _ = this.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(...);
    }
}

public virtual bool TryAddMoney(int value)
{
    if (this.Money + value > MaximumInventoryMoney) return false;  // ← Read
    if (this.Money + value < 0) return false;                      // ← Read
    this.Money = checked(this.Money + value);                      // ← Read + Write
    return true;
}
```

### Por qué es crítico

`Money` se lee 3 veces y se escribe 1 vez, **sin ningún lock**. Cada lectura puede retornar un valor diferente si otro thread modifica Money entre lecturas.

### Escenario de dupe de dinero

```
Money = 1,000,000 (en la BD)

Thread A (Comprar item):           Thread B (Depositar en vault):
1. Lee Money → 1,000,000          1. Lee Money → 1,000,000
2. 1M - 500K >= 0? Sí ✓           2. 1M - 500K >= 0? Sí ✓
3. Money = 1M - 500K              3. Vault += 500K
   Money = 500,000                    Money = 1M - 500K = 500,000

Resultado: Money = 500K, Vault = 500K, Item comprado
Total gastado: 1M (compra) + 500K (vault) = 1.5M
Pero solo tenía 1M → 500K apareció de la nada
```

### Dónde se llama Money sin lock

| Caller | Archivo | Operación |
|--------|---------|-----------|
| TradeMoneyAction | TradeMoneyAction.cs:36-38 | Read + Write × 3 |
| TradeButtonAction | TradeButtonAction.cs:101-102 | Write directo (+=) |
| BuyNpcItemAction | BuyNpcItemAction.cs | TryRemoveMoney |
| SellItemToNpcAction | SellItemToNpcAction.cs | TryAddMoney |
| BuyRequestAction (shop) | BuyRequestAction.cs | TryRemoveMoney + TryAddMoney |
| DepositVaultMoney | Player.cs:915 | TryRemoveMoney + Vault.TryAddMoney |
| TakeVaultMoney | Player.cs:935 | Vault.TryRemoveMoney + TryAddMoney |
| PickupMoney | PickupItemAction.cs | TryAddMoney |

**Cada una de estas operaciones puede correr concurrentemente.** Un jugador que compra en NPC mientras su partner completa un trade puede duplicar dinero.

### Fix completo

```csharp
private readonly SemaphoreSlim _moneyLock = new(1, 1);

public async ValueTask<bool> TryAddMoneyAsync(int value)
{
    await _moneyLock.WaitAsync();
    try
    {
        if (this.Money + value > MaximumInventoryMoney) return false;
        if (this.Money + value < 0) return false;
        this.Money = checked(this.Money + value);
        return true;
    }
    finally
    {
        _moneyLock.Release();
    }
}

// TODAS las operaciones de dinero deben usar TryAddMoneyAsync
```

---

## CRÍTICO 2: Storage.TryAddMoney/TryRemoveMoney — mismo problema

**Archivo:** `src/GameLogic/Storage.cs:169-190`

```csharp
public bool TryAddMoney(int value)
{
    if (this.ItemStorage.Money + value < 0) return false;      // ← Read
    this.ItemStorage.Money = this.ItemStorage.Money + value;    // ← Read + Write
    return true;
}

public bool TryRemoveMoney(int value)
{
    if (this.ItemStorage.Money - value < 0) return false;      // ← Read
    this.ItemStorage.Money = this.ItemStorage.Money - value;    // ← Read + Write
    return true;
}
```

### El bug

Exactamente el mismo patrón: read-check-write sin atomicidad. Afecta:
- Vault money (depositar/retirar)
- Inventario money (todas las transacciones)

### Dupe via vault

```
Vault Money = 1,000,000

Thread A (Retirar 800K):            Thread B (Retirar 800K):
1. 1M - 800K >= 0? Sí ✓            1. 1M - 800K >= 0? Sí ✓
2. Money = 1M - 800K = 200K         2. Money = 1M - 800K = 200K

Resultado: Vault = 200K, ambos threads recibieron 800K
Total retirado: 1.6M de un vault con 1M
```

---

## CRÍTICO 3: Party operations — List<T> sin synchronization

**Archivo:** `src/GameLogic/Party.cs`

```csharp
public IList<IPartyMember> PartyList { get; }  // Plain List<T>
```

### Operaciones concurrentes sobre PartyList

```
Thread A (jugador se une):        Thread B (jugador sale):
PartyList.Add(newMember)          PartyList.Remove(oldMember)

List<T> NO es thread-safe.
Add y Remove concurrentes pueden causar:
- Corruption interna del array
- IndexOutOfRangeException
- Elementos perdidos o duplicados
- InvalidOperationException en enumeraciones
```

### Dónde se accede sin lock

| Método | Operación |
|--------|-----------|
| `AddAsync()` | `PartyList.Add()` |
| `ExitPartyAsync()` | `PartyList.Remove()` |
| `KickPlayerAsync()` | `PartyList[index]` |
| `KickMySelfAsync()` | For loop sobre `PartyList` |
| `DistributeExperienceAfterKillAsync()` | Iteración sobre `PartyList` |
| `HealthUpdateElapsed()` | Iteración sobre `PartyList` |

**`HealthUpdateElapsed` es `async void`** — si crashea por concurrent modification, la excepción se pierde y el party health update se detiene silenciosamente.

### Fix

```csharp
private readonly SemaphoreSlim _partyLock = new(1, 1);

// O usar ConcurrentBag/ImmutableList:
private ImmutableList<IPartyMember> _partyList = ImmutableList<IPartyMember>.Empty;

public async ValueTask<bool> AddAsync(IPartyMember newMember)
{
    await _partyLock.WaitAsync();
    try
    {
        if (_partyList.Count >= _maxPartySize) return false;
        _partyList = _partyList.Add(newMember);
        return true;
    }
    finally { _partyLock.Release(); }
}
```

---

## CRÍTICO 4: MagicEffectsList — Dictionary público sin sync

**Archivo:** `src/GameLogic/MagicEffectsList.cs:35`

```csharp
public IDictionary<short, MagicEffect> ActiveEffects { get; }
```

### El bug

`ActiveEffects` es un `Dictionary<short, MagicEffect>` público. Código externo lee y modifica sin pasar por el `_addLock`:

```csharp
// En múltiples archivos:
if (!target.MagicEffectList.ActiveEffects.ContainsKey(effectId))  // ← No lock
{
    // Asumir que no tiene el efecto
}

// Mientras tanto otro thread:
this.ActiveEffects.Add(effectId, newEffect);  // ← Dentro de _addLock

// Dictionary<K,V> no es thread-safe para lectura concurrente con escritura
// → InvalidOperationException o datos corruptos
```

### Fix

```csharp
// Usar ConcurrentDictionary:
public ConcurrentDictionary<short, MagicEffect> ActiveEffects { get; }

// O hacer ActiveEffects private y exponer métodos thread-safe:
public bool HasEffect(short effectId) { ... } // con lock
public bool TryGetEffect(short effectId, out MagicEffect effect) { ... } // con lock
```

---

## ALTA 5: Vault deposit/withdraw — two-phase sin atomicidad

**Archivo:** `src/GameLogic/Player.cs:915-958`

```csharp
public bool TryDepositVaultMoney(int value)
{
    if (this.Vault.ItemStorage.Money + value > MaximumVaultMoney)
        return false;

    if (this.TryRemoveMoney(value))      // Fase 1: quitar de inventario
    {
        return this.Vault.TryAddMoney(value);  // Fase 2: agregar a vault
        // Si Fase 2 falla → dinero desapareció del inventario
        // NO HAY ROLLBACK
    }
    return false;
}
```

### El bug

Fase 1 y Fase 2 no son atómicas:
1. Check: vault tiene espacio ✓
2. Fase 1: quitar dinero del inventario ✓
3. **Otro thread deposita dinero al vault** → vault ahora está lleno
4. Fase 2: agregar a vault → FALLA
5. Dinero quitado del inventario pero no en vault → **PERDIDO**

### Fix

```csharp
public async ValueTask<bool> TryDepositVaultMoneyAsync(int value)
{
    await _moneyLock.WaitAsync();
    try
    {
        if (this.Vault.ItemStorage.Money + value > MaximumVaultMoney)
            return false;

        if (this.Money < value)
            return false;

        // Ambas operaciones dentro del mismo lock:
        this.Money -= value;
        this.Vault.ItemStorage.Money += value;
        return true;
    }
    finally { _moneyLock.Release(); }
}
```

---

## ALTA 6: 14 métodos `async void` — excepciones perdidas

**Archivos con async void:**

```csharp
// Cada uno puede crashear silenciosamente:
async void SafeTick()                    // BasicMonsterIntelligence.cs:192
async void SafeTick()                    // TrapIntelligenceBase.cs:108
async void CleanUpOnFinish()             // BaseInvasionPlugIn.cs:125
async void OnDestructibleDied()          // BloodCastleContext.cs:151
async void OnMonsterDied()               // ChaosCastleContext.cs:266
async void DisposeAndDelete()            // DroppedItem.cs:159
async void ExecutePeriodicTasks()        // GameContext.cs:468
async void RecoverTimerElapsed()         // GameContext.cs:484
async void OnDamageTimerElapsed()        // PoisonMagicEffect.cs:55
async void OnTimerTimeout()              // MagicEffect.cs:115
async void OnScoreChanged()              // SoccerGameMap.cs:115
async void OnTimerTimeout()              // DroppedMoney.cs:156
async void HealthUpdateElapsed()         // Party.cs:331
async void OnScorePropertyChanged()      // GuildWarAnswerAction.cs:113
```

### El problema

`async void` significa:
1. El caller no puede `await` el resultado
2. Si una excepción ocurre, se propaga al `SynchronizationContext`
3. En un server sin SynchronizationContext (console app), la excepción va al ThreadPool
4. La excepción se PIERDE — no logs, no crash, simplemente desaparece
5. El sistema queda en estado inconsistente

### Ejemplos de impacto

```csharp
// Party.cs:331
async void HealthUpdateElapsed(object? sender, EventArgs e)
{
    // Si falla → party health updates se detienen SILENCIOSAMENTE
    // Los jugadores dejan de ver la vida de sus party members
    // Sin error visible
}

// DroppedItem.cs:159
async void DisposeAndDelete(object? state)
{
    await this.DisposeAsync();     // Si esto falla → item en mapa pero no en DB
    await this.DeleteItemAsync();  // Si esto falla → item en DB pero no en mapa
}

// GameContext.cs:468
async void ExecutePeriodicTasks(object? state)
{
    // Si falla → TODAS las tareas periódicas se detienen
    // Regeneración, spawns, eventos, todo muere
}
```

### Fix

```csharp
// Reemplazar async void con async Task + error handling:
private async Task HealthUpdateElapsedAsync()
{
    try
    {
        // ... logic ...
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Party health update failed");
    }
}

// Para event handlers que requieren async void:
async void HealthUpdateElapsed(object? sender, EventArgs e)
{
    try
    {
        await HealthUpdateElapsedAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unhandled exception in health update");
    }
}
```

---

## ALTA 7: 18 `Task.Run` fire-and-forget

Encontrados en Walker.cs, GateNpc.cs, BloodCastleContext.cs, WizardTeleportAction.cs, y más.

```csharp
// WizardTeleportAction.cs:45
_ = Task.Run(() => player.TeleportAsync(target, skill));
// Si TeleportAsync falla → excepción perdida
// Jugador queda en limbo: cliente muestra teleport pero servidor no lo procesó
```

### Fix

```csharp
_ = Task.Run(async () =>
{
    try
    {
        await player.TeleportAsync(target, skill);
    }
    catch (Exception ex)
    {
        player.Logger.LogError(ex, "Teleport failed");
        // Corregir posición del cliente
        await player.InvokeViewPlugInAsync<ITeleportPlugIn>(
            p => p.ShowTeleportedAsync());
    }
});
```

---

## Mapa de race conditions por sistema

```
┌─────────────────────────────────────────────────────────┐
│                    PLAYER                                │
│                                                         │
│  Money ──── TryAddMoney() ──── NO LOCK ────── CRÍTICO  │
│    │                                                    │
│    ├── Trade ──── TradeMoneyAction ──── NO LOCK         │
│    ├── Shop ──── BuyRequestAction ──── NO LOCK          │
│    ├── NPC ──── BuyNpcItemAction ──── NO LOCK           │
│    ├── Vault ──── TryDeposit/Take ──── NO LOCK          │
│    └── Pickup ──── PickupItemAction ──── NO LOCK        │
│                                                         │
│  Inventory ──── AddItem/RemoveItem ──── PARCIAL LOCK   │
│    │                                                    │
│    ├── Move ──── MoveItemAction ──── NO LOCK            │
│    ├── Trade ──── TradeButtonAction ──── PARCIAL        │
│    └── Craft ──── CraftingHandler ──── NO LOCK          │
│                                                         │
│  Attributes ──── Set/Get ──── NO LOCK                   │
│    │                                                    │
│    ├── Health/Mana ──── HitAsync ──── NO LOCK           │
│    ├── Shield ──── HitAsync ──── NO LOCK                │
│    └── Stats ──── IncreaseStatsAction ──── NO LOCK      │
│                                                         │
│  Party ──── List<T> ──── NO LOCK ──── CRÍTICO          │
│  Effects ──── Dictionary ──── PARCIAL LOCK              │
│  Observers ──── List<T> ──── NO LOCK                    │
└─────────────────────────────────────────────────────────┘
```

---

## Tabla resumen

| # | Bug | Tipo | Impacto | Ubicación |
|---|-----|------|---------|-----------|
| 1 | Money read-modify-write | Race Condition | **Dupe de dinero** | Player.cs:965 |
| 2 | Storage money sin lock | Race Condition | **Dupe vault money** | Storage.cs:169 |
| 3 | PartyList sin sync | Concurrent Modify | **Server crash** | Party.cs:55 |
| 4 | ActiveEffects público | Concurrent Dict | **Corruption/crash** | MagicEffectsList.cs:35 |
| 5 | Vault two-phase | Non-Atomic | **Pérdida de dinero** | Player.cs:915 |
| 6 | 14× async void | Lost Exceptions | **Fallas silenciosas** | Múltiples archivos |
| 7 | 18× Task.Run F&F | Lost Exceptions | **Estado inconsistente** | Múltiples archivos |
