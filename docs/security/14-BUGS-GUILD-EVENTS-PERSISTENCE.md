# 14 - Bugs de Guild, Eventos, Persistencia y Character Management

**Severidad:** CRÍTICA a MEDIA
**CWE:** CWE-284 (Access Control), CWE-367 (TOCTOU), CWE-662 (Synchronization)

---

## CRÍTICO 1: Disconnect para revertir cambios — Rollback exploit

**Archivo:** `src/GameLogic/Player.cs` (DisconnectAsync / InternalDisconnectAsync)

### El bug

Cuando un jugador se desconecta, el servidor NO guarda automáticamente el progreso en todos los casos. Si `SaveProgressAsync()` no se llama entre una acción y la desconexión:

```
Estado en BD: Jugador tiene [Espada +13] y [1,000,000 zen]

1. Jugador vende Espada +13 al NPC por 500,000 zen
   Estado en memoria: Sin espada, 1,500,000 zen
   Estado en BD: Todavía [Espada +13] y [1,000,000 zen]

2. Jugador desconecta (kill process / cortar internet)

3. Jugador reconecta
   El servidor carga desde BD: [Espada +13] y [1,000,000 zen]

Resultado: Jugador recuperó su espada Y tiene el zen original
```

### Cuándo funciona este exploit

Depende de CUÁNDO el servidor hace `SaveChangesAsync()`:
- Si guarda después de cada transacción → protegido
- Si guarda periódicamente (cada X minutos) → **vulnerable**
- Si guarda solo al desconectar limpiamente → **vulnerable** (kill -9 no trigger save)

### El caso especial del Trade

```csharp
// TradeButtonAction.cs:96-102
await itemContext.SaveChangesAsync();           // Items guardados ✓
trader.Money += trader.TradingPartner.TradingMoney;  // Money en memoria, NO en BD
// Si desconexión aquí ↑
// Items transferidos en BD pero money no actualizado
// → Jugador A tiene items nuevos + money original
```

### Cómo explotar en trade

```
Jugador A: Tiene 0 zen, tiene [Item Valioso]
Jugador B: Tiene 10,000,000 zen

1. A y B abren trade
2. A ofrece [Item Valioso], B ofrece 10,000,000 zen
3. Ambos aceptan
4. Servidor: SaveChangesAsync() guarda transferencia de items
5. Servidor: Money += ... (en memoria)
6. A DESCONECTA INMEDIATAMENTE (antes de que money se persista)
7. A reconecta: Tiene [Item de B] + 0 zen (money no se guardó)
   B tiene: [Item Valioso de A] + 0 zen (money ya restado en memoria, pero...)

Si B también desconecta rápido: B recupera sus 10M zen de la BD
Resultado: AMBOS tienen los items del otro + su money original = DUPE
```

### Fix

```csharp
// Todo cambio de money debe persistirse inmediatamente:
trader.Money += trader.TradingPartner.TradingMoney;
trader.TradingPartner.Money += trader.TradingMoney;
await trader.PersistenceContext.SaveChangesAsync();
await trader.TradingPartner.PersistenceContext.SaveChangesAsync();

// O mejor: usar una transacción que incluya items Y money:
await using var transaction = await context.Database.BeginTransactionAsync();
// ... items + money ...
await transaction.CommitAsync();
```

---

## CRÍTICO 2: Guild creation sin requisitos — cualquier nivel puede crear guild

**Archivo:** `src/GameLogic/PlayerActions/Guild/GuildCreateAction.cs:20-51`

```csharp
public async ValueTask CreateGuildAsync(Player creator, string guildName, byte[] guildEmblem)
{
    // Solo verifica:
    // 1. Estado del jugador (EnteredWorld) ✓
    // 2. GuildServer disponible ✓
    // 3. Nombre de guild no duplicado ✓

    // NO verifica:
    // - Nivel mínimo del personaje
    // - Costo de zen para crear guild
    // - Item requerido (Guild Insignia)
    // - Que el jugador no esté ya en una guild
    // - Longitud/caracteres válidos del nombre
    // - Tamaño/formato del emblema

    await guildServer.CreateGuildAsync(guildName, creator.SelectedCharacter!.Name, ...);
}
```

### Cómo explotar

1. Crear personaje level 1
2. Enviar paquete de crear guild directamente
3. Guild creada sin gastar zen ni tener nivel requerido
4. Repetir para crear guilds spam

### Fix

```csharp
public async ValueTask CreateGuildAsync(Player creator, string guildName, byte[] guildEmblem)
{
    // Validar nivel mínimo
    if (creator.SelectedCharacter!.Level < MinimumGuildCreationLevel)
        return;

    // Validar que no está en una guild
    if (creator.GuildStatus is not null)
        return;

    // Validar costo
    if (!creator.TryRemoveMoney(GuildCreationCost))
        return;

    // Validar nombre (longitud, caracteres)
    if (guildName.Length < 2 || guildName.Length > 8 || !guildName.All(char.IsLetterOrDigit))
        return;

    // Validar emblema (tamaño fijo)
    if (guildEmblem.Length != ExpectedEmblemSize)
        return;

    // ... proceder con creación
}
```

---

## ALTA 3: Guild kick sin security code si SecurityCode es null

**Archivo:** `src/GameLogic/PlayerActions/Guild/GuildKickPlayerAction.cs:44`

```csharp
if (player.Account!.SecurityCode != null && player.Account.SecurityCode != securityCode)
{
    // Solo verifica si SecurityCode NO es null
    return;
}
// Si SecurityCode ES null → NO se verifica NADA → kick sin código
```

### El bug

La condición `SecurityCode != null &&` significa que si `SecurityCode` es `null`, toda la verificación se salta. Dado que las cuentas OAuth se crean con `SecurityCode = "123456"` (ver Doc 01), y si alguien lo pone a null:

- Cuentas sin security code → guild kick sin verificación
- Un guild master sin security code puede disbandear la guild sin ninguna confirmación

### Fix

```csharp
// Siempre requerir security code
if (string.IsNullOrEmpty(player.Account!.SecurityCode))
{
    // Forzar al jugador a configurar security code primero
    await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.SecurityCodeRequired));
    return;
}

if (player.Account.SecurityCode != securityCode)
{
    await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.WrongSecurityCode));
    return;
}
```

---

## ALTA 4: Security code logueado en plaintext

**Archivo:** `src/GameLogic/PlayerActions/Guild/GuildKickPlayerAction.cs:47`

```csharp
player.Logger.LogDebug("Wrong Security Code: [{0}] <> [{1}], Player: {2}",
    securityCode,           // ← Lo que envió el jugador (input)
    player.Account.SecurityCode,  // ← El código real de la cuenta
    player.SelectedCharacter?.Name);
```

### El bug

El **security code real de la cuenta** se escribe en los logs. Si alguien tiene acceso a los logs:
- Obtiene todos los security codes de las cuentas
- Puede borrar personajes, kickear de guilds, etc.

### Fix

```csharp
player.Logger.LogDebug("Wrong Security Code attempt for Player: {0}", player.SelectedCharacter?.Name);
// NUNCA loguear el código real ni el intentado
```

---

## ALTA 5: Gate 0 = teleport a coordenadas arbitrarias

**Archivo:** `src/GameServer/MessageHandler/WarpGateHandlerPlugIn.cs:47-50`

```csharp
if (gateNumber == 0)
{
    await this._teleportAction.TryTeleportWithSkillAsync(
        player, new Point(request.TeleportTargetX, request.TeleportTargetY));
    return;
}
```

### El análisis

Gate 0 invoca `WizardTeleportAction.TryTeleportWithSkillAsync` que SÍ valida:
- `WalkMap[target.X, target.Y]` — posición caminable ✓
- `!SafezoneMap[target.X, target.Y]` — no teleportar a safezone ✓
- `player.IsInRange(target, skill.Range)` — dentro del rango del skill ✓
- Que el jugador tenga el skill de teleport ✓
- Que tenga mana suficiente ✓

**Veredicto: Parcialmente protegido.** Las validaciones existen, pero un jugador con el skill de Teleport puede abusar si `skill.Range` es muy alto o si la validación de `IsInRange` usa posición desactualizada.

### El bug real

```csharp
// WizardTeleportAction.cs:39
&& player.CurrentMap!.Terrain.WalkMap[target.X, target.Y]
```

Si `target.X` o `target.Y` son 255 (máximo byte), y el WalkMap es `bool[256, 256]`:
- Acceso a `WalkMap[255, 255]` → dentro de bounds pero ¿es un tile válido?
- Algunos mapas pueden tener tiles walkables en los bordes que no deberían ser accesibles

---

## ALTA 6: Character deletion — Security code en plaintext vs BCrypt

**Archivo:** `src/GameLogic/PlayerActions/Character/DeleteCharacterAction.cs:48-57`

```csharp
var checkAsPassword = string.IsNullOrEmpty(player.Account.SecurityCode);

if (checkAsPassword && !BCrypt.Net.BCrypt.Verify(securityCode, player.Account.PasswordHash))
{
    return CharacterDeleteResult.WrongSecurityCode;
}

if (!checkAsPassword && player.Account.SecurityCode != securityCode)
{
    return CharacterDeleteResult.WrongSecurityCode;
}
```

### El bug de diseño

Hay **dos niveles de seguridad diferentes**:
1. Si NO hay security code → usa BCrypt para verificar contra el **password hash** (fuerte)
2. Si hay security code → **comparación en texto plano** (débil)

Esto es contradictorio: los usuarios con security code tienen MENOS seguridad que los que no lo tienen.

### Problemas específicos:

1. **Timing attack en comparación de strings:** `SecurityCode != securityCode` es vulnerable a timing analysis. La comparación se detiene en el primer carácter diferente, revelando información.

2. **Security code no hasheado:** Se almacena y compara en texto plano. Si la BD es comprometida, todos los security codes son legibles.

3. **Brute force:** Un security code de 7 dígitos tiene solo 10,000,000 combinaciones. Sin rate limiting, se puede forzar en minutos.

### Fix

```csharp
// 1. Hashear security code con BCrypt (como el password)
newAccount.SecurityCode = BCrypt.Net.BCrypt.HashPassword(securityCode);

// 2. Usar comparación constant-time
if (!CryptographicOperations.FixedTimeEquals(
    Encoding.UTF8.GetBytes(storedCode),
    Encoding.UTF8.GetBytes(providedCode)))
{
    return WrongSecurityCode;
}

// 3. Rate limiting
if (player.SecurityCodeAttempts >= MaxAttempts)
{
    // Bloquear cuenta temporalmente
}
```

---

## MEDIA 7: Eventos — Ticket index underflow en DevilSquare

**Archivo:** `src/GameServer/MessageHandler/MiniGames/DevilSquareEnterHandlerPlugIn.cs:45`

```csharp
var ticketIndex = request.TicketItemInventoryIndex - InventoryConstants.EquippableSlotsCount;
```

Si `TicketItemInventoryIndex < EquippableSlotsCount`, el resultado es negativo. Dependiendo del tipo de `ticketIndex`, puede causar underflow y acceder a slots inválidos del inventario.

---

## MEDIA 8: Eventos — Sin validar si el evento está activo

**Archivo:** `src/GameLogic/PlayerActions/MiniGames/EnterMiniGameAction.cs`

La validación verifica requisitos de nivel y ticket, pero en algunos paths no verifica si el evento está actualmente en fase de registro. Un jugador podría intentar entrar fuera del horario del evento.

---

## MEDIA 9: Item drop — async void en DisposeAndDelete

**Archivo:** `src/GameLogic/DroppedItem.cs:159-174`

```csharp
private async void DisposeAndDelete(object? state)
{
    await this.DisposeAsync().ConfigureAwait(false);
    if (player != null)
    {
        await this.DeleteItemAsync(player).ConfigureAwait(false);
    }
}
```

### El bug

`async void` es fire-and-forget. Si `DeleteItemAsync` falla:
- La excepción se pierde (no hay caller que la capture)
- El item se removió del mapa (`DisposeAsync`) pero NO de la BD
- El item persiste en la base de datos como huérfano
- Al siguiente restart, items fantasma podrían reaparecer

### Fix

```csharp
private async Task DisposeAndDeleteAsync(object? state)
{
    try
    {
        await this.DisposeAsync().ConfigureAwait(false);
        if (player != null)
        {
            await this.DeleteItemAsync(player).ConfigureAwait(false);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to dispose and delete dropped item");
    }
}
```

---

## Tabla resumen

| # | Bug | Tipo | Impacto | Archivo |
|---|-----|------|---------|---------|
| 1 | Disconnect reverts progress | Missing Persistence | **Dupe via rollback** | Player.cs / TradeButtonAction.cs |
| 2 | Guild sin requisitos | Missing Validation | Guilds spam gratis | GuildCreateAction.cs:20 |
| 3 | Guild kick sin security code | Auth Bypass | Kick sin verificación | GuildKickPlayerAction.cs:44 |
| 4 | Security code en logs | Info Disclosure | Robo de códigos | GuildKickPlayerAction.cs:47 |
| 5 | Gate 0 teleport | Partial Validation | Teleport edge cases | WarpGateHandlerPlugIn.cs:47 |
| 6 | Security code plaintext | Weak Auth | Timing attack, brute force | DeleteCharacterAction.cs:54 |
| 7 | Ticket index underflow | Integer Underflow | Acceso a slots inválidos | DevilSquareEnterHandler.cs:45 |
| 8 | Evento sin validar estado | Missing Validation | Entrar fuera de horario | EnterMiniGameAction.cs |
| 9 | async void en drop delete | Error Handling | Items huérfanos en BD | DroppedItem.cs:159 |
