# 12 - Bugs de Red, Protocolo y Arquitectura

**Severidad:** CRÍTICA a MEDIA
**CWE:** CWE-400 (Resource Exhaustion), CWE-294 (Replay Attack), CWE-306 (Missing Auth)

---

## CRÍTICO 1: ConnectServer — Registrar servidor falso para MITM

**Archivo:** `src/Dapr/ConnectServer.Host/ConnectionServerController.cs`

```csharp
[HttpPost("GameServerHeartbeat")]
[Topic("pubsub", "GameServerHeartbeat")]
public async Task GameServerHeartbeatAsync([FromBody] GameServerHeartbeatArguments data)
{
    await this._registry.UpdateRegistrationAsync(data.ServerInfo, IPEndPoint.Parse(data.PublicEndPoint));
}
```

### El bug

Sin autenticación, cualquiera que alcance el ConnectServer puede registrar un "game server" falso.

### Cómo explotar — Ataque MITM completo

```
Paso 1: Registrar servidor falso
curl -X POST http://connectserver:3500/GameServerHeartbeat \
  -d '{"ServerInfo":{"Id":1,"Description":"Server 1 (100 players)"},
       "PublicEndPoint":"ip-atacante:55901"}'

Paso 2: Jugadores seleccionan "Server 1" en el launcher
        → Se conectan a ip-atacante:55901

Paso 3: El atacante corre un proxy:
        [Jugador] ←→ [Proxy atacante] ←→ [Servidor real]

Paso 4: El proxy descifra todo el tráfico (claves son públicas)
        → Captura passwords, items, chats
        → Puede modificar paquetes en tránsito
```

### Impacto

- **Robo masivo de credenciales** de todos los jugadores que se conecten
- **Manipulación de trades** en tiempo real
- **Inyección de paquetes** arbitrarios

### Fix

```csharp
[HttpPost("GameServerHeartbeat")]
[Authorize(Policy = "InternalService")]
public async Task GameServerHeartbeatAsync([FromBody] GameServerHeartbeatArguments data)
{
    // Validar que el PublicEndPoint es una IP permitida
    var endpoint = IPEndPoint.Parse(data.PublicEndPoint);
    if (!_allowedServerIps.Contains(endpoint.Address))
    {
        _logger.LogWarning("Rejected heartbeat from unauthorized IP: {IP}", endpoint);
        return;
    }

    await this._registry.UpdateRegistrationAsync(data.ServerInfo, endpoint);
}
```

---

## ALTA 2: Packet size sin límite — Memory exhaustion DoS

**Archivo:** `src/Network/ArrayExtensions.cs:176-189`

```csharp
public static int GetPacketSize(this Span<byte> packet)
{
    switch (packet[0])
    {
        case 0xC2:
        case 0xC4:
            return packet[1] << 8 | packet[2];  // Hasta 65,535 bytes
    }
}
```

### El bug

Un paquete C2/C4 puede declarar un tamaño de hasta 65,535 bytes. El servidor intenta leer esa cantidad de datos del socket. Un atacante puede:

```
1. Conectar al servidor
2. Enviar: [C2] [FF] [FF] [datos basura...]
3. Servidor aloca 65KB y espera llenar el buffer
4. Repetir con 1000 conexiones simultáneas
5. Servidor consume 65MB solo en buffers de paquetes incompletos
6. Con 10,000 conexiones: 650MB → Out of Memory
```

### Fix

```csharp
private const int MaxAllowedPacketSize = 4096; // Ajustar según necesidad real

public static int GetPacketSize(this Span<byte> packet)
{
    int size = packet[0] switch
    {
        0xC1 or 0xC3 => packet[1],
        0xC2 or 0xC4 => packet[1] << 8 | packet[2],
        _ => 0
    };

    if (size > MaxAllowedPacketSize)
    {
        throw new InvalidPacketHeaderException(
            $"Packet size {size} exceeds maximum {MaxAllowedPacketSize}");
    }

    return size;
}
```

---

## ALTA 3: Replay de paquetes — Counter con vulnerabilidad de timing

**Archivo:** `src/Network/SimpleModulus/PipelinedSimpleModulusDecryptor.cs:139-142`

```csharp
if (this.Counter != null && sizeCounter == 0 && outputBlock[0] != this.Counter.Count)
{
    throw new InvalidPacketCounterException(outputBlock[0], (byte)this.Counter.Count);
}
```

### El bug

La validación del counter solo ocurre:
1. Si `Counter != null` (puede ser null en ciertas configuraciones)
2. Si `sizeCounter == 0` (solo para el primer bloque del paquete)
3. **DESPUÉS de descifrar** — si la descifración produce basura, el counter puede coincidir accidentalmente

### Escenario de replay

```
Jugador legítimo envía:
  Paquete #1: [counter=5] "Comprar item X"
  Paquete #2: [counter=6] "Moverse a posición Y"

Atacante captura paquete #1 y lo reenvía:
  Replay:     [counter=5] "Comprar item X"

Si el servidor ya procesó counter=6 y espera counter=7:
  → counter=5 != 7 → RECHAZADO ✓

PERO: si el atacante reenvía RÁPIDAMENTE antes de counter=6:
  → counter=5 == 5 → ACEPTADO ✗ (timing attack)
```

### Fix

```csharp
// Usar window de counters ya vistos:
private readonly HashSet<byte> _recentCounters = new();

if (this.Counter != null && sizeCounter == 0)
{
    byte packetCounter = outputBlock[0];

    if (packetCounter != this.Counter.Count || _recentCounters.Contains(packetCounter))
    {
        throw new InvalidPacketCounterException(packetCounter, (byte)this.Counter.Count);
    }

    _recentCounters.Add(packetCounter);
    if (_recentCounters.Count > 10) _recentCounters.Clear();

    this.Counter.Increase();
}
```

---

## ALTA 4: OAuth tokens sin encriptar en tránsito

**Archivo:** `src/GameServer/MessageHandler/Login/OAuthLoginHandlerPlugIn.cs`

```csharp
public bool IsEncryptionExpected => false;  // ← PLAINTEXT

public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
{
    var span = packet.Span;
    byte provider = span[4];
    string token = Encoding.UTF8.GetString(span.Slice(5));  // Token en texto plano
    await _loginAction.LoginAsync(player, provider, token);
}
```

### El bug

Los tokens OAuth (Google, Facebook, Apple) se envían **sin encriptar** por la red. Combinado con que SimpleModulus es reversible (claves públicas), un atacante en la red puede:

1. Capturar el token OAuth
2. Usarlo para autenticarse como ese jugador
3. Los tokens OAuth suelen tener validez de 1 hora o más

### Fix

```csharp
public bool IsEncryptionExpected => true;  // Forzar encriptación

// O mejor: implementar TLS a nivel de transporte (ver documento 04)
```

---

## ALTA 5: Connection flooding sin protección adecuada

**Archivo:** `src/Network/Listener.cs:62-65`

```csharp
public void Start(int backlog = (int)SocketOptionName.MaxConnections)
{
    this._clientListener = new TcpListener(IPAddress.Any, this._port);
    this._clientListener.Start(backlog);
}
```

### El bug

El backlog se configura al máximo del sistema operativo. Aunque existe `CheckMaximumConnectionsPlugin`, este se ejecuta **después** de aceptar la conexión TCP. Esto significa:

```
Ataque Slowloris adaptado:
1. Abrir 10,000 conexiones TCP
2. Enviar 1 byte cada 30 segundos (mantener vivas)
3. No completar ningún handshake de protocolo
4. CheckMaximumConnections limita conexiones "activas"
   pero estas conexiones están en limbo: aceptadas pero no "activas"
5. Nuevos jugadores no pueden conectarse
```

### Fix

```csharp
// 1. Timeout agresivo para conexiones que no completan handshake:
var connection = await AcceptAsync();
_ = Task.Run(async () =>
{
    await Task.Delay(TimeSpan.FromSeconds(10));
    if (!connection.IsAuthenticated)
    {
        await connection.DisconnectAsync();
    }
});

// 2. Rate limiting por IP:
private readonly ConcurrentDictionary<IPAddress, int> _connectionsPerIp = new();

private bool CanAcceptFromIp(IPAddress ip)
{
    var count = _connectionsPerIp.AddOrUpdate(ip, 1, (_, c) => c + 1);
    return count <= MaxConnectionsPerIp;  // ej: 3
}
```

---

## MEDIA 6: Packet handler sin validación de estado

### El patrón vulnerable

Muchos packet handlers no verifican el `PlayerState` antes de procesar:

```csharp
// Ejemplo: handler de compra
public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
{
    // ¿El jugador está autenticado?
    // ¿El jugador tiene un personaje seleccionado?
    // ¿El jugador está en el mundo del juego?
    // → No se verifica explícitamente en muchos handlers

    BuyItemFromNpcRequest message = packet;
    await this._buyAction.BuyItemAsync(player, message.ItemSlot);
}
```

### El exploit

1. Conectar al servidor
2. NO hacer login
3. Enviar paquetes de juego directamente
4. Si el handler no verifica estado → procesa acciones sin autenticación

### Fix

```csharp
// Middleware de validación de estado:
public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
{
    if (player.PlayerState.CurrentState < PlayerState.EnteredWorld)
    {
        player.Logger.LogWarning("Action attempted in invalid state: {0}",
            player.PlayerState.CurrentState);
        return;
    }

    await ProcessAsync(player, packet);
}
```

---

## MEDIA 7: Información de versión expuesta en Connect Server

**Archivo:** `src/ConnectServer/` — El protocolo envía información del servidor al cliente durante la conexión inicial, incluyendo descripciones de servidores y capacidad.

### Cómo explotar

Un atacante puede conectarse al Connect Server sin autenticarse y obtener:
- Lista de game servers con IPs/puertos
- Capacidad y carga de cada servidor
- Descripciones (que pueden revelar versiones)

Esto facilita el reconocimiento para ataques dirigidos.

---

## Patrón de ataque combinado: Cadena de exploits

Los bugs individuales se vuelven más peligrosos cuando se combinan:

```
CADENA 1: Robo masivo de cuentas
┌─────────────────────────────────────────────────────┐
│ 1. Registrar servidor falso via ConnectServer       │
│    (Bug: Dapr sin auth)                             │
│ 2. Jugadores se conectan al proxy del atacante      │
│ 3. Capturar tokens OAuth en plaintext               │
│    (Bug: IsEncryptionExpected = false)              │
│ 4. Usar tokens para crear cuentas OAuth             │
│    (Bug: Apple JWT sin verificar firma)             │
│ 5. Acceder a todas las cuentas con security "123456"│
│    (Bug: Security code hardcoded)                   │
│                                                     │
│ Resultado: Acceso total a todas las cuentas         │
└─────────────────────────────────────────────────────┘

CADENA 2: Dupe masivo de items
┌─────────────────────────────────────────────────────┐
│ 1. Usar NPC interaction a distancia                 │
│    (Bug: sin validar distancia a NPC)               │
│ 2. Abrir trade con el partner                       │
│ 3. Explotar integer overflow en trade money          │
│    (Bug: uint→int overflow)                         │
│ 4. Explotar race condition en cancel trade           │
│    (Bug: BackupInventory sin lock)                  │
│ 5. Items duplicados + zen infinito                  │
│                                                     │
│ Resultado: Economía del juego destruida             │
└─────────────────────────────────────────────────────┘

CADENA 3: DoS del servidor
┌─────────────────────────────────────────────────────┐
│ 1. Connection flood (10,000 conexiones)             │
│    (Bug: sin rate limit por IP)                     │
│ 2. Enviar paquetes C2 con tamaño 65535              │
│    (Bug: sin max packet size)                       │
│ 3. Desde una conexión: party kick con index 255     │
│    (Bug: array out of bounds crash)                 │
│                                                     │
│ Resultado: Servidor inaccesible                     │
└─────────────────────────────────────────────────────┘
```

---

## Tabla resumen

| # | Bug | Tipo | Impacto | Archivo |
|---|-----|------|---------|---------|
| 1 | Registrar servidor falso | Missing Auth | **MITM masivo** | ConnectionServerController.cs |
| 2 | Packet size 65KB | Resource Exhaustion | **DoS memoria** | ArrayExtensions.cs:176 |
| 3 | Counter replay window | Replay Attack | Acciones duplicadas | PipelinedSimpleModulusDecryptor.cs:139 |
| 4 | OAuth token plaintext | Missing Encryption | Robo de tokens | OAuthLoginHandlerPlugIn.cs |
| 5 | Connection flooding | DoS | Servidor inaccesible | Listener.cs:62 |
| 6 | Handlers sin validar estado | Missing Auth | Acciones sin login | Múltiples handlers |
| 7 | Info de servidores expuesta | Info Disclosure | Reconocimiento | ConnectServer |
