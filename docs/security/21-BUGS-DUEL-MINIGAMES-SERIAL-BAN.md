# 21 - Bugs de Duelos, Mini-Games, Item Serials y Sistema de Ban

**Severidad:** CRÍTICA a MEDIA
**CWE:** CWE-193 (Off-by-One), CWE-367 (TOCTOU), CWE-284 (Improper Access Control)

---

## CRÍTICO 1: DuelRoom — IndexOutOfRangeException en spectators

**Archivo:** `src/GameLogic/DuelRoom.cs:210-214`

```csharp
for (int j = this.Spectators.Count; j >= 0; --j)
{
    var player = this.Spectators[j];  // ← CRASH en primera iteración
    await player.InvokeViewPlugInAsync<IDuelSpectatorRemovedPlugIn>(
        p => p.SpectatorRemovedAsync(spectator));
}
```

### El bug

El loop comienza con `j = this.Spectators.Count`, pero los índices válidos son `0` a `Count - 1`. En la primera iteración, `Spectators[Count]` lanza `IndexOutOfRangeException`.

### Impacto

Cada vez que un espectador sale de un duelo, el servidor crashea el handler. El duelo queda en estado inconsistente — los demás espectadores nunca reciben la notificación de que alguien salió.

### Fix

```csharp
for (int j = this.Spectators.Count - 1; j >= 0; --j)
{
    var player = this.Spectators[j];
    await player.InvokeViewPlugInAsync<IDuelSpectatorRemovedPlugIn>(
        p => p.SpectatorRemovedAsync(spectator));
}
```

---

## CRÍTICO 2: Duel entry fee — resultado ignorado explícitamente

**Archivo:** `src/GameLogic/PlayerActions/Duel/DuelActions.cs:112-115`

```csharp
// we risk that the money is not sufficient anymore,
// but we don't care anymore if it fails.
player.TryRemoveMoney(duelConfig.EntranceFee);
target.TryRemoveMoney(duelConfig.EntranceFee);
```

### El bug

El código **explícitamente** dice que ignora si la remoción de dinero falla. Si un jugador gastó su dinero entre el check inicial y este punto, entra al duelo sin pagar.

### Escenario de exploit

```
1. Jugador tiene 100,000 zen (entrance fee)
2. Acepta duelo → check de dinero pasa (línea anterior)
3. Rápidamente compra un item en NPC por 100,000 zen
4. TryRemoveMoney falla silenciosamente
5. Jugador entra al duelo gratis
6. Si gana, recibe el prize pool → ganancia neta
```

### Fix

```csharp
if (!player.TryRemoveMoney(duelConfig.EntranceFee)
    || !target.TryRemoveMoney(duelConfig.EntranceFee))
{
    // Reembolsar si uno falló
    player.TryAddMoney(duelConfig.EntranceFee);
    target.TryAddMoney(duelConfig.EntranceFee);
    await duelRoom.ResetAndDisposeAsync(DuelStartResult.FailedByError);
    return;
}
```

---

## ALTA 3: Spectator capacity — TOCTOU race condition

**Archivo:** `src/GameLogic/PlayerActions/Duel/DuelActions.cs:168-173`

```csharp
if (duelRoom.Spectators.Count >= config.MaximumSpectatorsPerDuelRoom)
{
    player.Logger.LogWarning("...");
    return;
}
// ← VENTANA: otro thread puede agregar spectator aquí
// La adición real ocurre más tarde en DuelRoom.TryAddSpectatorAsync
```

### El bug

El check de capacidad ocurre fuera del lock. Múltiples jugadores pueden pasar el check simultáneamente y exceder el límite de espectadores.

### Consecuencia

- Más espectadores de los permitidos
- Posible overflow del buffer de paquetes de duelo
- Degradación de performance

---

## ALTA 4: DuelRoom.IsOpen — propiedad sin sincronización

**Archivo:** `src/GameLogic/DuelRoom.cs:185`

```csharp
public bool IsOpen => this.Spectators.Count < this._maximumSpectators;
```

### El bug

Esta propiedad se lee por múltiples threads sin sincronización. `Spectators.Count` puede cambiar mientras se evalúa la condición, retornando información obsoleta.

---

## ALTA 5: Duel RunDuelAsync — lanzado como fire-and-forget

**Archivo:** `src/GameLogic/PlayerActions/Duel/DuelActions.cs:121`

```csharp
_ = Task.Run(duelRoom.RunDuelAsync);
```

### El bug

`RunDuelAsync` es lanzado con `Task.Run` y el resultado descartado. Si `RunDuelAsync` lanza una excepción no capturada, se pierde. El duelo quedaría activo pero sin lógica de juego ejecutándose — los jugadores atrapados en el mapa de duelo.

---

## ALTA 6: Blood Castle — quest item sin validar inventario

**Archivo:** `src/GameLogic/MiniGames/BloodCastleContext.cs:182-195`

```csharp
protected override async ValueTask OnPlayerPickedUpItemAsync(
    (Player Picker, ILocateable DroppedItem) args)
{
    await base.OnPlayerPickedUpItemAsync(args);
    if (args.DroppedItem is DroppedItem { Item.Definition: { } definition }
        && definition.IsArchangelQuestItem())
    {
        this._questItemOwner = args.Picker;  // ← Asigna owner sin verificar pickup
```

### El bug

El quest item owner se asigna sin verificar que el item fue realmente agregado al inventario del jugador. Si el inventario está lleno, el item se dropea al piso pero `_questItemOwner` ya fue asignado.

### Consecuencia

Un jugador con inventario lleno puede ser marcado como quest item owner sin tener el item, ganando el Blood Castle sin completar la quest.

---

## ALTA 7: Sistema de Ban — sin re-verificación durante gameplay

**Análisis global del sistema de ban:**

El ban solo se verifica en `LoginAction` al momento del login. No existe verificación durante el gameplay.

### El problema

```
1. Admin banea al jugador desde el Admin Panel
2. Estado en BD cambia a AccountState.Banned
3. Jugador ya logueado NO es afectado
4. Jugador sigue jugando indefinidamente hasta que se desconecte

No hay mecanismo para:
- Notificar al servidor de game sobre el ban
- Desconectar forzosamente al jugador baneado
- Re-verificar el estado periódicamente
```

### TemporarilyBanned sin duración

```csharp
// AccountState enum:
Normal,
Banned,
TemporarilyBanned  // ← No hay campo de duración/fecha de expiración
```

No existe un campo `BannedUntil` en el modelo de Account, haciendo que `TemporarilyBanned` sea efectivamente igual a `Banned` permanente.

### Fix

```csharp
// En Account model:
public DateTime? BannedUntil { get; set; }

// En LoginAction:
if (account.State == AccountState.TemporarilyBanned)
{
    if (account.BannedUntil.HasValue && account.BannedUntil.Value < DateTime.UtcNow)
    {
        account.State = AccountState.Normal;
        account.BannedUntil = null;
    }
    else
    {
        await player.ShowLoginResultAsync(LoginResult.TemporaryBlocked);
        return;
    }
}

// Verificación periódica en GameContext:
private async Task CheckBannedPlayersAsync()
{
    foreach (var player in this.Players)
    {
        if (player.Account?.State == AccountState.Banned)
        {
            await player.DisconnectAsync();
        }
    }
}
```

---

## MEDIA 8: Item Serial Numbers — sin sistema de tracking

### El problema

OpenMU no tiene un sistema de tracking de serial numbers de items. Los items tienen un `ItemSlot` pero no un serial number único global que permita:

1. **Detectar items duplicados** — Si un item es dupeado por race condition, ambas copias son indistinguibles
2. **Auditar transacciones** — No hay forma de rastrear el historial de un item
3. **Detectar items forjados** — Si un atacante crea un item via packet manipulation, no hay serial para validar su origen

### Cómo otros servidores lo resuelven

```
Cada item recibe un serial number único al ser creado:
- Drop de monstruo → nuevo serial
- Compra en NPC → nuevo serial
- Craft → nuevo serial
- Trade → serial se mantiene (mismo item)

Si dos items tienen el mismo serial → uno es dupe → eliminar ambos
```

### Fix conceptual

```csharp
// En Item model:
public Guid ItemSerial { get; set; } = Guid.NewGuid();

// En cada operación que crea items:
var newItem = new Item { ItemSerial = Guid.NewGuid(), ... };

// Verificación periódica:
var duplicates = allItems.GroupBy(i => i.ItemSerial)
    .Where(g => g.Count() > 1);
foreach (var dupeGroup in duplicates)
{
    _logger.LogCritical("Duplicate item detected: {serial}", dupeGroup.Key);
    // Eliminar o marcar para revisión
}
```

---

## MEDIA 9: Duel — sin validar estado del jugador

No hay verificación de que el jugador no esté en trade, crafting, o otro estado incompatible antes de aceptar un duelo. Un jugador en trade que acepta un duelo puede causar:

```
1. Items en TemporaryStorage (trade) al ser teleportado a duelo
2. Trade queda abierto sin jugador
3. Partner del trade queda bloqueado
```

---

## Tabla resumen

| # | Bug | Tipo | Impacto | Archivo:Línea |
|---|-----|------|---------|---------------|
| 1 | Spectator loop off-by-one | Off-by-One | **Crash** | DuelRoom.cs:210 |
| 2 | Entry fee ignorado | Ignored Return | **Duelo gratis** | DuelActions.cs:112-115 |
| 3 | Spectator capacity TOCTOU | Race Condition | Exceder límite | DuelActions.cs:168 |
| 4 | IsOpen sin sync | Data Race | Lectura inconsistente | DuelRoom.cs:185 |
| 5 | RunDuelAsync fire-forget | Lost Exception | Duelo muerto | DuelActions.cs:121 |
| 6 | Blood Castle quest item | Missing Check | **Ganar sin quest** | BloodCastleContext.cs:182 |
| 7 | Ban sin re-verificación | Missing Check | **Ban inefectivo** | LoginAction.cs |
| 8 | Sin item serial tracking | Missing Feature | **Dupes indetectables** | Global |
| 9 | Duel sin validar estado | Missing Check | Trade exploit | DuelActions.cs |
