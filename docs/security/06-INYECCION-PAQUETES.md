# 06 - Manipulación e Inyección de Paquetes de Red

**Severidad:** ALTA
**OWASP:** A04:2021 - Insecure Design
**CWE:** CWE-20 (Improper Input Validation), CWE-345 (Insufficient Verification of Data Authenticity)

---

## Qué es

En un juego cliente-servidor, el cliente envía paquetes de red que representan acciones del jugador (moverse, atacar, comprar, tradear). Si el servidor confía ciegamente en lo que el cliente envía sin validar, un atacante puede crear paquetes falsos para hacer cosas imposibles: duplicar items, teletransportarse, modificar trades.

**Regla de oro del desarrollo de juegos:** El cliente es solo una interfaz visual. El servidor es la autoridad. NUNCA confíes en datos del cliente.

---

## Superficie de ataque: Protocolo de paquetes MU Online

### Estructura de paquetes

```
Tipo C1 (pequeño):
[C1] [Length] [MainCode] [SubCode] [Data...]

Tipo C2 (grande):
[C2] [Length-Hi] [Length-Lo] [MainCode] [SubCode] [Data...]

Tipo C3/C4: como C1/C2 pero encriptados con SimpleModulus
```

Dado que la encriptación es reversible (ver documento 04), un atacante puede:
1. Interceptar paquetes reales
2. Descifrarlos
3. Modificarlos
4. Re-cifrarlos y enviarlos

O directamente crear paquetes desde cero con un bot/proxy.

---

## Problema 1: Trade - Potencial de Race Condition

**Archivos:**
- `src/GameServer/MessageHandler/Trade/TradeAcceptHandlerPlugIn.cs`
- `src/GameLogic/PlayerActions/Trade/TradeAcceptAction.cs`

### Escenario de ataque: Speed hack en trade

```
Jugador A ofrece: Item valioso
Jugador B ofrece: Zen

Flujo normal:
1. Ambos ponen items en ventana de trade
2. Ambos presionan "Accept"
3. Servidor intercambia items

Ataque (requiere timing preciso):
1. A y B ponen items
2. B presiona Accept
3. A envía paquete para QUITAR su item de la ventana de trade
4. Inmediatamente A envía Accept
5. Si el servidor no tiene lock adecuado:
   - B pierde su zen
   - A conserva su item Y recibe el zen de B
```

### Qué verificar en el servidor

```csharp
// El servidor DEBE:
// 1. Usar lock/semáforo durante la resolución del trade
// 2. Re-verificar que los items siguen en la ventana al momento de ejecutar
// 3. No permitir modificar la ventana después de aceptar

// Ejemplo de protección:
public async Task AcceptTradeAsync(Player player)
{
    await using var tradeLock = await _tradeSemaphore.WaitAsync();

    // Re-validar estado DENTRO del lock
    if (player.TradeState != TradeState.Accepted)
        return;
    if (tradePartner.TradeState != TradeState.Accepted)
        return;

    // Verificar que los items aún existen y están en la ventana
    foreach (var item in player.TradeWindow.Items)
    {
        if (!player.Inventory.Contains(item))
        {
            CancelTrade(player, tradePartner);
            return;
        }
    }

    // Ejecutar intercambio atómicamente
    ExecuteTradeAtomically(player, tradePartner);
}
```

---

## Problema 2: Item Move - Duplicación potencial

**Archivo:** `src/GameServer/MessageHandler/Items/ItemMoveHandlerPlugIn.cs`

### Escenario de ataque: Dupe via rapid slot swap

```
Inventario:
Slot 0: [Item Valioso]
Slot 1: [Vacío]

Ataque (envío rápido de paquetes):
1. Enviar: Mover item de Slot 0 → Slot 1
2. Enviar: Mover item de Slot 0 → Slot 2  (antes de que el servidor procese #1)

Si no hay lock por personaje:
- Paquete 1: Lee Slot 0 (item existe), mueve a Slot 1 ✓
- Paquete 2: Lee Slot 0 (item aún no borrado), mueve a Slot 2 ✓
- Resultado: Item duplicado en Slot 1 y Slot 2
```

### Cómo debe protegerse

```csharp
// Cada operación de inventario debe ser secuencial POR PERSONAJE
// Usar un SemaphoreSlim por jugador:

public class InventoryGuard
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IDisposable> LockAsync()
    {
        await _semaphore.WaitAsync();
        return new SemaphoreReleaser(_semaphore);
    }
}

// En el handler:
public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
{
    await using var guard = await player.InventoryGuard.LockAsync();

    // Ahora es seguro: solo un paquete de inventario a la vez por jugador
    var fromSlot = packet.Span[4];
    var toSlot = packet.Span[5];

    var item = player.Inventory.GetItem(fromSlot);
    if (item == null) return;  // Ya no existe

    if (!player.Inventory.TryMove(item, fromSlot, toSlot))
        return;  // Destino ocupado o inválido
}
```

---

## Problema 3: Drop/Pickup - Items fuera de rango

**Archivos:**
- `src/GameServer/MessageHandler/Items/PickupItemHandlerPlugIn.cs`
- `src/GameServer/MessageHandler/Items/DropItemHandlerPlugIn.cs`

### Escenario de ataque

```
Jugador está en posición (100, 100)
Item está en el suelo en posición (200, 200)

Ataque: Enviar paquete de pickup para el item en (200, 200)
Si el servidor no valida distancia → el jugador recoge items a distancia
```

### Qué validar

```csharp
public async ValueTask HandlePickupAsync(Player player, ushort itemId)
{
    var droppedItem = player.CurrentMap.GetDroppedItem(itemId);
    if (droppedItem == null) return;

    // VALIDAR DISTANCIA
    var distance = player.Position.DistanceTo(droppedItem.Position);
    if (distance > MaxPickupDistance)  // ej: 3 tiles
    {
        player.Logger.LogWarning("Pickup attempt too far: {0} tiles", distance);
        return;  // Ignorar silenciosamente (no dar info al atacante)
    }

    // VALIDAR OWNERSHIP (si el item tiene dueño temporal)
    if (droppedItem.Owner != null
        && droppedItem.Owner != player
        && !droppedItem.OwnershipExpired)
    {
        return;
    }

    // Proceder con pickup
}
```

---

## Problema 4: Chat Commands - Inyección de comandos

**Archivo:** `src/GameLogic/PlayerActions/Chat/ChatMessageCommandProcessor.cs`

### Escenario de ataque

Si el parser de comandos no es estricto, un jugador podría intentar:
```
/setstat Strength 999999
/addstat 65535
/disconnect OtroJugador
```

### Qué validar

```csharp
// 1. Verificar permisos ANTES de parsear el comando
if (command.MinCharacterStatusRequirement > player.SelectedCharacter.CharacterStatus)
{
    // No ejecutar NI dar feedback de que el comando existe
    return;
}

// 2. Validar rangos de valores
public void HandleAddStat(Player player, string[] args)
{
    if (!int.TryParse(args[0], out var amount)) return;

    // Limitar a rangos razonables
    amount = Math.Clamp(amount, 1, player.AvailableStatPoints);

    // Verificar que tiene puntos disponibles
    if (player.AvailableStatPoints < amount) return;
}
```

---

## Problema 5: Paquetes con tamaño inválido

### Escenario de ataque: Buffer overflow / DoS

```
Paquete normal: [C1] [0x0A] [F1] [01] [data x 6]  (10 bytes)
Paquete malicioso: [C1] [0xFF] [F1] [01] [data x 251]  (255 bytes, pero handler espera 10)
Otro malicioso: [C1] [0x04] [F1] [01]  (4 bytes, muy corto, handler lee más allá)
```

### Cómo protegerse

```csharp
// Cada handler debe verificar el tamaño mínimo del paquete:
public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
{
    if (packet.Length < ExpectedMinimumSize)
    {
        player.Logger.LogWarning("Packet too short: {0} bytes, expected {1}",
            packet.Length, ExpectedMinimumSize);
        return;
    }

    // También limitar tamaño máximo
    if (packet.Length > ExpectedMaximumSize)
    {
        player.Logger.LogWarning("Packet too large: {0} bytes", packet.Length);
        return;
    }
}
```

---

## Patrón general de defensa: Server-Side Authority

```
                    ┌─────────────────────────┐
                    │     SERVIDOR (verdad)    │
                    │                         │
Paquete del  ───►  │  1. Validar tamaño      │
cliente             │  2. Validar permisos    │
                    │  3. Validar estado      │
                    │  4. Validar rangos      │
                    │  5. Validar distancia   │
                    │  6. Lock/sincronización │
                    │  7. Ejecutar acción     │
                    │  8. Persistir resultado │
                    │  9. Notificar clientes  │
                    └─────────────────────────┘
```

**Cada paquete del cliente debe pasar por todas estas validaciones.** El cliente solo es una sugerencia; el servidor decide.

---

## Herramienta útil: Packet Logger para testing

```csharp
// Agregar un middleware de logging para detectar patrones sospechosos
public class PacketRateLimiter
{
    private readonly Dictionary<Player, Queue<DateTime>> _packetHistory = new();

    public bool IsFlooding(Player player)
    {
        if (!_packetHistory.TryGetValue(player, out var history))
            history = _packetHistory[player] = new Queue<DateTime>();

        history.Enqueue(DateTime.UtcNow);

        // Limpiar registros viejos (más de 1 segundo)
        while (history.Count > 0 && (DateTime.UtcNow - history.Peek()).TotalSeconds > 1)
            history.Dequeue();

        // Más de 50 paquetes por segundo = sospechoso
        if (history.Count > 50)
        {
            player.Logger.LogWarning("Packet flooding detected: {0} packets/sec", history.Count);
            return true;
        }
        return false;
    }
}
```

---

## Referencias

- [OWASP: Insecure Design](https://owasp.org/Top10/A04_2021-Insecure_Design/)
- [CWE-20: Improper Input Validation](https://cwe.mitre.org/data/definitions/20.html)
- [Game Security: Server Authority Pattern](https://gafferongames.com/post/introduction_to_networked_physics/)
- [Race Conditions in Game Servers](https://www.gamedeveloper.com/programming/preventing-exploits-in-online-games)
