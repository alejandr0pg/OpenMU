# 11 - Bugs de Combate, Stats, Movimiento y Party

**Severidad:** CRÍTICA a MEDIA
**CWE:** CWE-129 (Array Index), CWE-362 (Race Condition), CWE-190 (Integer Overflow)

---

## CRÍTICO 1: Party Kick — Array Index Out of Bounds (Server Crash)

**Archivo:** `src/GameLogic/Party.cs:92-96`

```csharp
public async ValueTask KickPlayerAsync(byte index)
{
    var toKick = this.PartyList[index];  // Sin bounds check
    await this.ExitPartyAsync(toKick, index).ConfigureAwait(false);
}
```

### El bug

`index` es un `byte` (0-255) que viene directamente del paquete del cliente. `PartyList` tiene máximo 5 miembros. Si envías `index = 200`:

```
PartyList.Count = 4
PartyList[200] → IndexOutOfRangeException → CRASH del servidor
```

### Por qué es grave

Esta excepción no está capturada en un try-catch. Si sube hasta el loop principal del servidor, puede:
- Crashear el thread que procesa al jugador
- En el peor caso, crashear todo el game server
- Un atacante puede repetir esto para hacer **DoS permanente** mientras tenga una cuenta

### Cómo explotar

```
1. Unirse a un party (solo necesitas 2 jugadores)
2. Enviar paquete de party kick con index = 255
3. Servidor crashea
4. Repetir para mantener el servidor caído
```

### Fix

```csharp
public async ValueTask KickPlayerAsync(byte index)
{
    if (index >= this.PartyList.Count)
    {
        return; // Índice inválido, ignorar silenciosamente
    }

    var toKick = this.PartyList[index];
    await this.ExitPartyAsync(toKick, index).ConfigureAwait(false);
}
```

---

## CRÍTICO 2: Dapr GameServerController — Shutdown remoto sin auth

**Archivo:** `src/Dapr/GameServer.Host/GameServerController.cs:34-39`

```csharp
[HttpPost(nameof(IGameServer.ShutdownAsync))]
public async ValueTask ShutdownAsync()
{
    await this._gameServer.ShutdownAsync().ConfigureAwait(false);
    Environment.Exit(0);  // MATA EL PROCESO
}
```

### El bug

NINGUNO de los endpoints del GameServerController tiene `[Authorize]`:

```csharp
POST /ShutdownAsync          → Apaga el servidor
POST /BanPlayerAsync         → Banea a cualquier jugador
POST /GuildChatMessageAsync  → Envía mensajes como cualquier guild
POST /AllianceChatMessageAsync
POST /LetterSendAsync
POST /AssignGuildToPlayerAsync
POST /InitializeMessengerAsync
POST /IsPlayerOnlineAsync
```

### Cómo explotar

Si el puerto del Dapr sidecar es accesible (default 3500):

```bash
# Apagar el servidor:
curl -X POST http://servidor:3500/ShutdownAsync

# Banear a cualquier jugador:
curl -X POST http://servidor:3500/BanPlayerAsync \
  -H "Content-Type: application/json" \
  -d '"NombreJugador"'

# Enviar mensaje falso de guild:
curl -X POST http://servidor:3500/GuildChatMessageAsync \
  -H "Content-Type: application/json" \
  -d '{"GuildId":1,"Sender":"GM","Message":"Server se cierra, vayan a www.phishing.com"}'

# Registrar servidor falso (ConnectServer):
curl -X POST http://connectserver:3500/GameServerHeartbeat \
  -H "Content-Type: application/json" \
  -d '{"ServerInfo":{"Id":99,"Description":"Fake Server"},"PublicEndPoint":"malicious-ip:55901"}'
```

### Fix

```csharp
// 1. Agregar middleware de autenticación Dapr:
[ApiController]
[Route("")]
[Authorize]  // Requiere token de servicio Dapr
public class GameServerController : ControllerBase

// 2. O usar Dapr API token:
// dapr run --app-id gameserver --dapr-api-token "mi-token-secreto"

// 3. Network policies: asegurar que solo los sidecars pueden comunicarse
```

---

## CRÍTICO 3: TryRemoveMoney acepta valores negativos → genera dinero

**Archivo:** `src/GameLogic/Player.cs:965-979` (verificado)

```csharp
public virtual bool TryAddMoney(int value)
{
    if (this.Money + value > this.GameContext?.Configuration?.MaximumInventoryMoney)
        return false;

    if (this.Money + value < 0)
        return false;

    this.Money = checked(this.Money + value);
    return true;
}
```

### El bug

`TryAddMoney` con valor negativo = quita dinero. Pero no hay un `TryRemoveMoney` separado que valide que `value > 0`. Cualquier lugar que llame `TryAddMoney(-x)` sin verificar que `x` sea positivo podría:

```csharp
// Si algún código hace:
player.TryAddMoney((int)(-1 * someUintValue));
// Y someUintValue > int.MaxValue → overflow → valor positivo
// → El jugador GANA dinero
```

Esto ya ocurre en `TradeMoneyAction.cs:37` (ver documento 10).

### Fix general

```csharp
public bool TryRemoveMoney(int value)
{
    if (value <= 0)
        return false;  // Solo valores positivos

    if (this.Money < value)
        return false;

    this.Money = checked(this.Money - value);
    return true;
}
```

---

## ALTA 4: Skill consume sin lock — uso múltiple de skills gratis

**Archivo:** `src/GameLogic/Player.cs` (método TryConsumeForSkill o equivalente)

### El patrón

```csharp
// Thread 1 (skill A):                    // Thread 2 (skill B):
if (mana >= skillA.ManaCost)              if (mana >= skillB.ManaCost)
{                                         {
  // mana = 100, cost = 80                  // mana = 100, cost = 80
  // 100 >= 80 ✓                            // 100 >= 80 ✓ (aún no restado)
  mana -= 80;                               mana -= 80;
  // mana = 20                              // mana = -60 ← NEGATIVO
}                                         }
```

### Cómo explotar

1. Configurar un bot que envíe 2+ paquetes de skill en el mismo frame
2. El servidor procesa ambos asincrónicamente
3. Ambos pasan la verificación de mana antes de que se reste
4. El jugador usa 2 skills por el costo de 1
5. Repetir para spam infinito de skills

### Fix

```csharp
private readonly SemaphoreSlim _consumeLock = new(1, 1);

public async ValueTask<bool> TryConsumeForSkillAsync(Skill skill)
{
    await _consumeLock.WaitAsync();
    try
    {
        if (skill.ConsumeRequirements.Any(r =>
            GetRequiredValue(r) > this.Attributes![r.Attribute]))
            return false;

        foreach (var req in skill.ConsumeRequirements)
            this.Attributes![req.Attribute] -= GetRequiredValue(req);

        return true;
    }
    finally
    {
        _consumeLock.Release();
    }
}
```

---

## ALTA 5: NPC interaction sin validar distancia

**Archivo:** `src/GameServer/MessageHandler/TalkNpcHandlerPlugInBase.cs:32-36`

```csharp
if (player.CurrentMap?.GetObject(message.NpcId) is NonPlayerCharacter npc)
{
    await this.TalkNpcAction.TalkToNpcAsync(player, npc).ConfigureAwait(false);
}
```

### El bug

El jugador envía un NPC ID y el servidor abre el diálogo **sin verificar distancia**. Esto permite:

1. **Comprar/vender desde cualquier parte del mapa:** Enviar paquete de TalkNpc con el ID del NPC vendedor desde el otro lado del mapa
2. **Acceder a NPCs de eventos sin estar presente:** Interactuar con NPCs de Blood Castle, Devil Square, etc. sin entrar al evento
3. **Abrir vault/warehouse remotamente**

### Fix

```csharp
if (player.CurrentMap?.GetObject(message.NpcId) is NonPlayerCharacter npc
    && player.IsInRange(npc, MaxNpcInteractionDistance))  // ej: 5 tiles
{
    await this.TalkNpcAction.TalkToNpcAsync(player, npc);
}
else
{
    player.Logger.LogWarning("NPC interaction out of range");
}
```

---

## ALTA 6: Master Skill point overflow

**Archivo:** `src/GameLogic/PlayerActions/Character/AddMasterPointAction.cs:74-101`

```csharp
private async ValueTask AddMasterPointToLearnedSkillAsync(Player player, SkillEntry learnedSkill)
{
    var requiredPoints = learnedSkill.Level == 0
        ? learnedSkill.Skill.MasterDefinition!.MinimumLevel : 1;

    if (player.SelectedCharacter!.MasterLevelUpPoints >= requiredPoints
        && learnedSkill.Level < learnedSkill.Skill.MasterDefinition!.MaximumLevel)
    {
        learnedSkill.Level += requiredPoints;  // Sin checked, puede overflow
        player.SelectedCharacter.MasterLevelUpPoints -= requiredPoints;
    }
}
```

### El bug

Si `learnedSkill.Level` es `byte` (0-255) y `requiredPoints` es grande, la suma puede hacer overflow:
- `Level = 250`, `requiredPoints = 10` → `Level = 260` → overflow a `4` (si es byte)
- El level "vuelve a 0" efectivamente, permitiendo re-subir la skill

### Fix

```csharp
if (learnedSkill.Level + requiredPoints > learnedSkill.Skill.MasterDefinition.MaximumLevel)
    return;

learnedSkill.Level = checked((byte)(learnedSkill.Level + requiredPoints));
```

---

## MEDIA 7: Warp gate position validation con datos stale

**Archivo:** `src/GameLogic/PlayerActions/WarpGateAction.cs:58-65`

```csharp
var currentPosition = player.IsWalking ? player.WalkTarget : player.Position;
var inaccuracy = player.GameContext.Configuration.InfoRange;

if (player.CurrentMap!.Definition.EnterGates.Contains(enterGate)
    && !(this.IsXInRange(currentPosition, enterGate, inaccuracy)
         && this.IsYInRange(currentPosition, enterGate, inaccuracy)))
{
    return false;
}
```

### El bug

1. `currentPosition` se lee del estado del jugador, que puede estar desactualizado
2. `InfoRange` se usa como "margen de error", pero si es grande (ej: 20 tiles), el jugador puede estar lejos del gate
3. Entre leer la posición y ejecutar el warp, el jugador podría haberse movido

### Cómo explotar

1. Jugador camina hacia un gate
2. Envía paquete de warp cuando está a 15 tiles del gate
3. Si `InfoRange` es >= 15, el warp se acepta
4. El jugador se teletransporta sin haber llegado al gate

---

## MEDIA 8: Whisper para enumerar jugadores online

**Archivo:** `src/GameLogic/PlayerActions/Chat/ChatMessageWhisperProcessor.cs:17-29`

```csharp
var whisperReceiver = sender.GameContext.GetPlayerByCharacterName(content.PlayerName);
if (whisperReceiver != null)
{
    // Envía mensaje
}
// Si es null: silencio total, sin feedback
```

### Cómo explotar

```
Script automático:
for each nombre in diccionario:
    enviar whisper a "nombre"
    medir tiempo de respuesta

Si tiempo_respuesta < threshold → jugador ONLINE
Si timeout → jugador OFFLINE

Resultado: lista completa de jugadores online
```

Útil para:
- Saber cuándo un jugador con items valiosos está online
- Dirigir ataques PvP/social engineering
- Mapear la base de jugadores activos

### Fix

```csharp
if (whisperReceiver != null)
{
    // Enviar mensaje
}
else
{
    // SIEMPRE dar feedback genérico al sender
    await sender.InvokeViewPlugInAsync<IShowChatMessagePlugIn>(
        p => p.ShowChatMessageAsync("", "Player is not online or doesn't exist."));
}
```

---

## MEDIA 9: Sin rate limiting en stat increase

**Archivo:** `src/GameServer/MessageHandler/Character/CharacterStatIncreasePacketHandlerPlugIn.cs`

Un bot puede enviar 1000 paquetes de `IncreaseStats` por segundo. Aunque cada uno solo sube 1 punto y requiere tener puntos disponibles, el procesamiento masivo:
- Genera carga innecesaria en el servidor
- Crea muchas operaciones de BD
- Puede usarse como vector de DoS lento

---

## Tabla resumen

| # | Bug | Tipo | Impacto | Archivo:Línea |
|---|-----|------|---------|---------------|
| 1 | Party kick sin bounds check | Array OOB | **Server crash (DoS)** | Party.cs:94 |
| 2 | Dapr controllers sin auth | Missing Auth | **Shutdown remoto** | GameServerController.cs:34 |
| 3 | TryRemoveMoney negativo | Integer Overflow | Generar dinero | Player.cs:965 |
| 4 | Skill consume sin lock | Race Condition | Skills gratis | Player.cs (consume) |
| 5 | NPC sin distancia | Missing Validation | Comprar remotamente | TalkNpcHandlerPlugInBase.cs:32 |
| 6 | Master skill overflow | Integer Overflow | Re-subir skills | AddMasterPointAction.cs:81 |
| 7 | Warp gate position stale | TOCTOU | Teleport sin gate | WarpGateAction.cs:58 |
| 8 | Whisper enumeration | Info Disclosure | Lista jugadores | ChatMessageWhisperProcessor.cs:17 |
| 9 | Stat flood sin limit | Missing Rate Limit | DoS lento | CharacterStatIncrease...cs |
