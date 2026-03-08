# 16 - Bugs del Sistema Social, Mensajería y Consumibles

**Severidad:** ALTA a MEDIA
**CWE:** CWE-193 (Off-by-One), CWE-799 (Improper Control of Interaction Frequency)

---

## ALTA 1: Off-by-One en lectura de cartas — IndexOutOfRangeException

**Archivo:** `src/GameLogic/PlayerActions/Messenger/LetterReadRequestAction.cs:22`

```csharp
if (player.SelectedCharacter?.Letters.Count < letterIndex)
{
    player.Logger.LogWarning("Player {0} requested non-existing letter...");
    return;
}

var letter = player.SelectedCharacter?.Letters[letterIndex];  // ← CRASH
```

### El bug

La condición usa `<` en vez de `<=`. Si el jugador tiene 5 cartas (índices 0-4):

```
Letters.Count = 5
letterIndex = 5

Verificación: 5 < 5 = false → NO entra al guard
Letters[5] → IndexOutOfRangeException → CRASH
```

### Cómo explotar

1. Tener cualquier cantidad de cartas (N)
2. Enviar paquete de leer carta con `letterIndex = N`
3. Servidor crashea el handler del jugador
4. Potencial DoS si no hay try-catch

### Fix

```csharp
if (letterIndex >= (player.SelectedCharacter?.Letters.Count ?? 0))
{
    player.Logger.LogWarning("...");
    return;
}
```

---

## ALTA 2: Friend request sin consentimiento mutuo

**Archivo:** `src/FriendServer/FriendServer.cs:100-126`

```csharp
public async ValueTask FriendResponseAsync(string characterName, string friendName, bool accepted)
{
    var requester = await context.GetFriendByNamesAsync(friendName, characterName);
    requester.Accepted = accepted;

    if (accepted)
    {
        // Crea entrada reversa automáticamente
        var responder = await context.GetFriendByNamesAsync(characterName, friendName)
            ?? await context.CreateNewFriendAsync(characterName, friendName);
        responder.RequestOpen = false;
        responder.Accepted = true;  // ← Auto-acepta para el otro jugador
    }
}
```

### El bug

Cuando A acepta la solicitud de B, el sistema automáticamente marca a B como amigo de A **sin que B haya aceptado**. Esto viola el principio de consentimiento mutuo.

### Cómo explotar

1. Atacante envía solicitud de amistad a Víctima
2. Víctima está offline o no responde
3. Si el atacante puede manipular la respuesta (o si hay otro bug en el flujo), puede auto-aceptar
4. Atacante aparece en la lista de amigos de Víctima
5. Puede ver cuándo Víctima está online/offline

---

## ALTA 3: Enumeración de personajes via friend requests

**Archivo:** `src/FriendServer/FriendServer.cs:54-84`

```csharp
public async ValueTask<bool> FriendRequestAsync(string playerName, string friendName)
{
    var friend = await context.GetFriendByNamesAsync(playerName, friendName);
    friendIsNew = friend is null;
    if (friendIsNew)
    {
        friend = await context.CreateNewFriendAsync(playerName, friendName);
        // ...
    }
    return friendIsNew && saveSuccess;  // ← true = personaje existe, false = ya era amigo
}
```

### Cómo explotar

```python
# Script para enumerar personajes existentes:
for name in wordlist:
    result = send_friend_request(name)
    if result == True:
        print(f"[EXISTE] {name}")
        cancel_friend_request(name)  # Limpiar

# Sin rate limiting, puede probar miles de nombres por minuto
```

---

## ALTA 4: Town Portal escape de dungeons/eventos

**Archivo:** `src/GameLogic/PlayerActions/ItemConsumeActions/TownPortalScrollConsumeHandlerPlugIn.cs:29-43`

```csharp
public override async ValueTask<bool> ConsumeItemAsync(Player player, Item item, ...)
{
    if (await base.ConsumeItemAsync(player, item, targetItem, fruitUsage))
    {
        var targetMapDef = player.CurrentMap!.Definition.SafezoneMap
            ?? player.SelectedCharacter!.CharacterClass!.HomeMap;
        await player.WarpToAsync(spawnGate);
        return true;
    }
    return false;
}
```

### El bug

No valida si el jugador está en:
- Blood Castle (evento)
- Devil Square (evento)
- Chaos Castle (evento)
- Duel arena
- Castle Siege
- Cualquier zona restringida

### Cómo explotar

1. Entrar a Blood Castle
2. Cuando la situación se pone difícil, usar Town Portal Scroll
3. Escapar del evento sin morir → sin penalty
4. Los items/recompensas parciales podrían mantenerse

### Fix

```csharp
public override async ValueTask<bool> ConsumeItemAsync(Player player, ...)
{
    // Verificar que no está en zona restringida
    if (player.CurrentMiniGame is not null)
        return false;

    if (player.CurrentMap?.Definition.MapType == MapType.Event)
        return false;

    // ... proceder con teleport
}
```

---

## MEDIA 5: Item consumption durante trade/NPC dialog

**Archivo:** `src/GameLogic/PlayerActions/ItemConsumeActions/BaseConsumeHandlerPlugIn.cs:49-58`

```csharp
protected virtual bool CheckPreconditions(Player player, Item item)
{
    if (player.PlayerState.CurrentState != PlayerState.EnteredWorld
        || item.Durability == 0)
    {
        return false;
    }
    return true;
    // NO verifica: trading, NPC dialog, duel, mini-game
}
```

### El bug

Solo verifica que el jugador esté en `EnteredWorld`. No verifica estados adicionales como:
- `TradeOpened` → consumir items mientras están en ventana de trade
- Diálogo con NPC → consumir items de quest durante interacción
- En combate → spam de pociones sin cooldown (parcialmente mitigado por PotionCooldown)

### Fix

```csharp
protected virtual bool CheckPreconditions(Player player, Item item)
{
    if (player.PlayerState.CurrentState != PlayerState.EnteredWorld)
        return false;

    if (item.Durability == 0)
        return false;

    // No permitir consumo durante trade
    if (player.TradingPartner is not null)
        return false;

    // No permitir durante NPC dialog (crafting)
    if (player.OpenedNpc is not null && ShouldBlockDuringNpcDialog(item))
        return false;

    return true;
}
```

---

## MEDIA 6: Spam de cartas sin rate limiting

**Archivo:** `src/GameLogic/PlayerActions/Messenger/LetterSendAction.cs:27-75`

Solo verifica que el jugador tenga zen suficiente para el costo de la carta. No hay:
- Cooldown entre envíos
- Límite de cartas por hora/día
- Límite de cartas al mismo destinatario

### Cómo explotar

```
1. Tener 10,000,000 zen
2. Enviar 100,000 cartas a un jugador (costo ~100 zen cada una)
3. La bandeja de entrada del jugador se llena
4. Potencial DoS del storage de la base de datos
```

---

## MEDIA 7: PK status change sin audit trail

**Archivo:** `src/GameLogic/PlugIns/ChatCommands/PKChatCommandPlugIn.cs:54`

```csharp
character.State = HeroState.Normal + arguments.Level;
character.PlayerKillCount = arguments.Count;
```

Un GM puede cambiar el PK status de cualquier jugador sin que quede registro de quién lo hizo. Si una cuenta GM es comprometida, el atacante puede:
- Marcar jugadores inocentes como asesinos
- Limpiar el PK status de sus propias cuentas
- Sin forma de auditar los cambios

---

## Tabla resumen

| # | Bug | Tipo | Impacto | Archivo:Línea |
|---|-----|------|---------|---------------|
| 1 | Off-by-one en letter read | Off-by-One | **Crash/DoS** | LetterReadRequestAction.cs:22 |
| 2 | Friend sin consentimiento | Logic Error | Stalking | FriendServer.cs:116 |
| 3 | Enumerar personajes | Info Disclosure | Reconocimiento | FriendServer.cs:60 |
| 4 | Town Portal escape eventos | Missing Validation | Bypass penalties | TownPortalScrollConsume.cs:29 |
| 5 | Consume durante trade | Missing State Check | Item exploit | BaseConsumeHandlerPlugIn.cs:51 |
| 6 | Letter spam | Missing Rate Limit | DoS bandeja | LetterSendAction.cs:27 |
| 7 | PK change sin audit | Missing Audit | Abuso de GM | PKChatCommandPlugIn.cs:54 |
