# 15 - Vulnerabilidades del Cliente: Packet Forging y Server Trust

**Severidad:** CRÍTICA a ALTA
**CWE:** CWE-602 (Client-Side Enforcement of Server-Side Security), CWE-345 (Insufficient Verification)

---

## Concepto fundamental: El cliente es el enemigo

En seguridad de juegos online, **TODO** lo que viene del cliente es potencialmente falso. El cliente es solo una interfaz visual; un atacante puede reemplazarlo completamente con un bot que envía paquetes arbitrarios.

```
Regla de oro: El servidor NUNCA debe confiar en datos del cliente.
El servidor debe RECALCULAR y REVALIDAR todo.
```

---

## CRÍTICO 1: El cliente envía ITEM DATA completa al mover items

**Archivo cliente:** `Client.Main/Networking/PacketHandling/PacketBuilder.cs:295-314`

```csharp
public static int BuildItemMoveRequestPacket(
    IBufferWriter<byte> writer,
    ItemStorageKind fromStorage, byte fromSlot,
    ReadOnlySpan<byte> itemData,      // ← 12 bytes de datos del item COMPLETOS
    ItemStorageKind toStorage, byte toSlot)
{
    var dest = packet.ItemData;
    itemData.Slice(0, Math.Min(dest.Length, itemData.Length)).CopyTo(dest);
}
```

**Archivo cliente:** `Client.Main/Networking/Services/CharacterService.cs:1436-1438`

```csharp
PacketBuilder.BuildItemMoveRequestPacket(
    _connectionManager.Connection.Output,
    store, fromSlot,
    itemData,       // ← Raw bytes del item
    store, toSlot);
```

### Por qué es el bug más peligroso

El paquete de "mover item" incluye los **12 bytes completos de datos del item**:

```
Bytes del item (Season 6, 12 bytes):
[0]    Item ID (group + number)
[1]    Options flags (level, skill, luck, option)
[2]    Durability
[3]    Serial (byte 1)
[4]    Serial (byte 2)
[5]    Serial (byte 3)
[6]    Serial (byte 4)
[7]    Excellent options + ancient
[8-9]  Socket options
[10]   Harmony option
[11]   Additional flags
```

Un atacante puede modificar CUALQUIERA de estos bytes:

```
Original: Espada +0, sin luck, sin excellent
Bytes:    [00] [00] [FF] [xx] [xx] [xx] [xx] [00] [00] [00] [00] [00]

Modificado: Espada +15, luck, excellent, ancient
Bytes:    [00] [7F] [FF] [xx] [xx] [xx] [xx] [FF] [FF] [FF] [FF] [FF]
```

### Cómo explotar

```python
# Pseudo-código de un bot/proxy:

# 1. Interceptar paquete de "mover item" del cliente
packet = intercept_packet()

# 2. Modificar los bytes del item
item_data = packet[offset_item_data:offset_item_data+12]
item_data[1] = 0x7F  # Level 15 + Skill + Luck + Option
item_data[7] = 0xFF  # Todos los excellent options
item_data[2] = 0xFF  # Durabilidad máxima

# 3. Reenviar al servidor
send_to_server(packet)
```

### Qué debe hacer el servidor

El servidor **NO debe usar el itemData del paquete** para nada que no sea identificar qué item mover. Debe:

```csharp
// CORRECTO: Ignorar itemData del cliente, usar solo slots
public async ValueTask HandleItemMove(Player player, byte fromSlot, byte toSlot,
    ItemStorageKind fromStorage, ItemStorageKind toStorage)
{
    // Buscar el item REAL desde el estado del servidor
    var item = GetStorageForKind(player, fromStorage).GetItem(fromSlot);
    if (item == null) return;

    // Usar el item real del servidor, NO los datos del paquete
    MoveItem(item, fromSlot, toSlot, fromStorage, toStorage);
}
```

---

## CRÍTICO 2: El cliente especifica TARGET IDs en ataques de área

**Archivo cliente:** `Client.Main/Networking/PacketHandling/PacketBuilder.cs:359-387`

```csharp
public static int BuildAreaSkillHitPacket(
    IBufferWriter<byte> writer,
    ushort skillId,
    byte targetX, byte targetY,
    byte hitCounter,
    IReadOnlyList<ushort> targetIds,     // ← CLIENTE DECIDE A QUIÉN DAÑAR
    byte animationCounter)
{
    for (int i = 0; i < hitCounter; i++)
    {
        var target = packet[i];
        target.TargetId = targetIds[i];  // ← ID de cada objetivo
    }
}
```

### Por qué es peligroso

El **cliente decide** qué entidades reciben daño de un ataque de área. Esto permite:

1. **Hit a través de paredes:** Incluir IDs de jugadores detrás de obstáculos
2. **Hit a distancia infinita:** Incluir IDs de jugadores en otra parte del mapa
3. **Hit múltiple al mismo target:** Enviar el mismo targetId varias veces en hitCounter
4. **Hit a entidades protegidas:** Incluir IDs de NPCs, GMs, o jugadores en safezone

### Ejemplo de ataque

```
Situación real:
  - Jugador A está en posición (50, 50)
  - Jugador B está en posición (200, 200) — al otro lado del mapa
  - Jugador C está en safezone

Paquete legítimo:
  targetIds = [mob1, mob2, mob3]  (los que están en rango)

Paquete malicioso:
  targetIds = [playerB_id, playerB_id, playerB_id, playerC_id]
  → Daña a B tres veces y a C una vez, aunque ninguno está en rango
```

### Qué debe validar el servidor

```csharp
public async ValueTask HandleAreaSkillHit(Player player, ushort skillId,
    Point targetPos, ushort[] clientTargetIds)
{
    var skill = player.SkillList.GetSkill(skillId);
    if (skill == null) return;

    // 1. Verificar que el jugador tiene mana para el skill
    if (!await player.TryConsumeForSkillAsync(skill)) return;

    // 2. Calcular targets REALES desde el servidor (ignorar clientTargetIds)
    var realTargets = player.CurrentMap
        .GetAttackablesInRange(targetPos, skill.Range)
        .Where(t => !t.IsAtSafezone())
        .Where(t => player.IsInRange(t.Position, skill.Range + 2))
        .Where(t => HasLineOfSight(player.Position, t.Position))
        .Take(skill.MaxTargets);

    // 3. Aplicar daño solo a targets válidos
    foreach (var target in realTargets)
    {
        var damage = CalculateDamage(player, target, skill);  // Server-side
        await target.ApplyDamageAsync(damage, player);
    }
}
```

---

## CRÍTICO 3: Posición de inicio del walk es client-specified

**Archivo cliente:** `Client.Main/Networking/PacketHandling/PacketBuilder.cs:187-216`

```csharp
public static int BuildWalkRequestPacket(
    IBufferWriter<byte> writer,
    byte startX, byte startY,        // ← Cliente dice dónde "está"
    byte[] path,                      // ← Cliente dice qué camino toma
    byte targetRotation)
```

### Por qué es peligroso

El cliente le dice al servidor "estoy en (startX, startY) y quiero caminar por este path". Si el servidor confía en la posición inicial:

```
Posición real del servidor: (50, 50)
Cliente envía: startX=100, startY=100, path=[norte, norte, norte]

Si el servidor acepta: jugador se teletransporta a (100, 103)
→ Speed hack / teleport instantáneo
```

### Qué debe validar el servidor

```csharp
public async ValueTask HandleWalk(Player player, byte claimedStartX, byte claimedStartY, byte[] path)
{
    // IGNORAR la posición del cliente
    var serverPosition = player.Position;

    // Validar que la posición del cliente no difiere mucho
    var distance = Math.Abs(serverPosition.X - claimedStartX) + Math.Abs(serverPosition.Y - claimedStartY);
    if (distance > MaxAllowedPositionDrift)  // ej: 3 tiles
    {
        // Corregir al cliente
        await player.SendPositionCorrectionAsync(serverPosition);
        return;
    }

    // Validar que el path es walkable
    var currentPos = serverPosition;
    foreach (var step in path)
    {
        var nextPos = ApplyDirection(currentPos, step);
        if (!player.CurrentMap.Terrain.WalkMap[nextPos.X, nextPos.Y])
        {
            // Path inválido, rechazar
            return;
        }
        currentPos = nextPos;
    }

    // Proceder con walk
    await player.WalkAsync(serverPosition, path);
}
```

---

## ALTA 4: Attack speed calculado client-side

**Archivo cliente:** `Client.Main/Core/Client/CharacterState.Equipment.cs:21-91`

```csharp
private ushort CalculateAttackSpeed()
{
    var agi = TotalAgility;
    int baseSpeed = Class switch { ... agi / 15 ... };
    return (ushort)Math.Max(0, baseSpeed + CalculateEquipmentAttackSpeedBonus());
}

public int CalculateEquipmentAttackSpeedBonus()
{
    // Lee de _inventoryItems y calcula bonuses
}
```

### Por qué es peligroso

Si el servidor usa el attack speed reportado por el cliente (o no valida la frecuencia de ataques):
- Cliente modifica `CalculateAttackSpeed()` para retornar 999
- Envía paquetes de ataque 10x más rápido de lo normal
- Speed hack sin modificar binarios — solo timing de paquetes

### Qué debe validar el servidor

```csharp
// Rate limiter por jugador para ataques
private readonly Dictionary<Player, DateTime> _lastAttackTime = new();

public bool CanPlayerAttack(Player player)
{
    var now = DateTime.UtcNow;
    if (_lastAttackTime.TryGetValue(player, out var lastAttack))
    {
        var expectedDelay = CalculateAttackDelay(player);  // Server-side
        if (now - lastAttack < expectedDelay)
        {
            return false;  // Demasiado rápido
        }
    }
    _lastAttackTime[player] = now;
    return true;
}
```

---

## ALTA 5: Hit request con target ID arbitrario

**Archivo cliente:** `Client.Main/Networking/Services/CharacterService.cs:832-854`

```csharp
SendHitRequestAsync(ushort targetId, byte attackAnimation, byte lookingDirection)
```

### El problema

El cliente especifica a QUIÉN atacar con un `targetId`. Sin validación server-side:
- Atacar jugadores invisibles (GMs)
- Atacar jugadores en otra parte del mapa
- Atacar NPCs que no deberían ser atacables

### Validaciones necesarias server-side

```csharp
// 1. Target existe y está en el mismo mapa
var target = player.CurrentMap.GetObject(targetId) as IAttackable;
if (target == null) return;

// 2. Target está en rango de ataque
if (!player.IsInRange(target.Position, player.AttackRange))
    return;

// 3. Target no está en safezone
if (target.IsAtSafezone()) return;

// 4. Target está vivo
if (target.IsDead) return;

// 5. PvP rules (no atacar mismo guild, etc.)
if (!CanAttack(player, target)) return;

// 6. Calcular daño SERVER-SIDE
var damage = DamageCalculation.Calculate(player, target);
```

---

## Resumen: Qué confía el servidor del cliente vs. qué debería

| Dato del paquete | ¿Cliente lo controla? | ¿Servidor debe confiar? | Riesgo si confía |
|---|---|---|---|
| Target position (walk) | Sí | **NO** — usar posición del servidor | Teleport hack |
| Target IDs (area skill) | Sí | **NO** — recalcular en servidor | Hit a distancia |
| Item data (12 bytes) | Sí | **NO** — usar item del servidor | Dupe/forge items |
| Skill ID | Sí | Validar que el jugador lo tiene | Usar skills ajenos |
| Attack animation | Sí | **NO** — solo visual | Speed hack |
| Hit counter | Sí | **NO** — servidor calcula targets | Daño multiplicado |
| Money amount (trade) | Sí | Validar vs. money real | Money hack |
| Slot numbers | Sí | Validar rango y contenido | Acceso fuera de bounds |
| Gate number | Sí | Validar que gate existe en mapa | Teleport a mapas ilegales |

---

## Herramienta de testing: Proxy de paquetes

Para verificar estas vulnerabilidades, puedes crear un proxy TCP simple:

```csharp
// Proxy entre cliente y servidor para inspeccionar/modificar paquetes
class PacketProxy
{
    async Task RelayAsync(NetworkStream source, NetworkStream dest, string label)
    {
        var buffer = new byte[65536];
        while (true)
        {
            var read = await source.ReadAsync(buffer);
            if (read == 0) break;

            // Log del paquete
            Console.WriteLine($"[{label}] {BitConverter.ToString(buffer, 0, read)}");

            // Aquí puedes modificar buffer antes de reenviar
            // Ej: cambiar targetId, modificar itemData, etc.

            await dest.WriteAsync(buffer, 0, read);
        }
    }
}
```

Con las claves de encriptación públicas (ver Doc 04), puedes descifrar, modificar y re-cifrar paquetes en tiempo real.

---

## Tabla resumen

| # | Vulnerabilidad | Impacto | Dificultad de exploit |
|---|---|---|---|
| 1 | Item data en paquete de mover | **Forge items, cambiar stats** | Fácil — modificar 12 bytes |
| 2 | Target IDs en area skill | **Hit a distancia, a través de paredes** | Fácil — forge packet |
| 3 | Posición de walk client-specified | **Teleport/speed hack** | Fácil — cambiar startX/Y |
| 4 | Attack speed client-side | **Speed hack** | Media — timing de paquetes |
| 5 | Hit request con target arbitrario | **PK sin restricciones** | Fácil — cambiar targetId |
