# 04 - Criptografía Débil en el Protocolo de Red

**Severidad:** ALTA
**OWASP:** A02:2021 - Cryptographic Failures
**CWE:** CWE-327 (Use of Broken Crypto Algorithm), CWE-328 (Reversible One-Way Hash)

---

## Qué es

La criptografía protege los datos en tránsito (entre cliente y servidor) para que un atacante que intercepte el tráfico no pueda leer ni modificar los datos. Un algoritmo criptográfico "débil" es uno que puede romperse con técnicas conocidas.

---

## Problema 1: XOR-3 para credenciales de login

**Archivo cliente:** `Client.Main/Networking/PacketHandling/PacketBuilder.cs:395-401`
**Archivo servidor:** `src/Network/Xor/Xor3Encryptor.cs`

```csharp
// Cliente - envío de username y password
private static void EncryptXor3(Span<byte> data, byte[] xor3Keys)
{
    for (int i = 0; i < data.Length; i++)
    {
        data[i] ^= xor3Keys[i % 3];  // Solo 3 bytes de clave, rotativos
    }
}
```

### Por qué es un problema

XOR con clave conocida **NO es encriptación**. Es ofuscación trivial.

**Demostración de cómo romperlo:**

```
Clave XOR-3: [K0, K1, K2]  (solo 3 bytes)

Texto original:  "admin\0\0\0\0\0"  (username padded)
                  0x61 0x64 0x6D 0x69 0x6E 0x00 0x00 0x00

Encriptado:       0x61^K0  0x64^K1  0x6D^K2  0x69^K0  0x6E^K1  ...

Para romperlo:
1. Las claves XOR están en el código fuente (son públicas)
2. Incluso sin las claves: XOR(encriptado, texto_conocido) = clave
3. Si capturas UN paquete de login, puedes extraer la clave
```

**Impacto real:**
Un atacante con acceso a la red (misma WiFi, ISP, proxy) puede leer TODOS los passwords en texto plano con un simple sniffer como Wireshark.

```python
# Script de Wireshark/Scapy para extraer passwords:
xor_keys = [0xFC, 0xCF, 0xAB]  # Ejemplo, las reales están en el código
def decrypt_xor3(data):
    return bytes([b ^ xor_keys[i % 3] for i, b in enumerate(data)])

# Capturar paquete de login (tipo C1, subcode F1 01)
# Offset del username y password en el paquete son conocidos
username = decrypt_xor3(packet[offset_user:offset_user+10])
password = decrypt_xor3(packet[offset_pass:offset_pass+20])
```

### Escala del problema

| Propiedad criptográfica | XOR-3 | AES-256 | TLS 1.3 |
|---|---|---|---|
| Longitud de clave | 24 bits | 256 bits | 256+ bits |
| Tiempo para romper | Instantáneo | Miles de millones de años | Imposible (conocido) |
| Protege contra sniffing | No | Sí | Sí |
| Protege contra replay | No | Depende del modo | Sí |
| Protege contra MITM | No | Depende | Sí (con certificados) |

---

## Problema 2: SimpleModulus - Algoritmo propietario débil

**Archivo:** `src/Network/SimpleModulus/PipelinedSimpleModulusEncryptor.cs`

### Qué hace SimpleModulus

Es un algoritmo de encriptación propietario creado por Webzen (desarrollador original de MU Online) circa 2003. Usa operaciones de módulo y desplazamiento de bits con claves estáticas.

### Por qué es débil

1. **Claves estáticas públicas:** Las claves están en el código fuente (ver documento 03)
2. **Sin intercambio de claves:** No hay handshake criptográfico; ambos lados usan las mismas claves fijas
3. **Sin autenticación de mensajes:** No hay MAC (Message Authentication Code), así que un atacante puede modificar paquetes en tránsito
4. **Algoritmo conocido y documentado:** Hay implementaciones en Python, C, y otros lenguajes disponibles públicamente
5. **No hay Perfect Forward Secrecy:** Si se compromete la clave, TODO el tráfico pasado y futuro es legible

### Escenario de ataque: Man-in-the-Middle (MITM)

```
[Cliente] ←→ [Atacante] ←→ [Servidor]

1. El atacante intercepta la conexión TCP (ARP spoofing, DNS hijack, etc.)
2. Descifra los paquetes usando las claves públicas
3. Lee/modifica el contenido (ej: cambiar destinatario de trade)
4. Re-encripta y reenvía al destino

El cliente y servidor no pueden detectar la manipulación
porque no hay verificación de integridad.
```

---

## Problema 3: Sin negociación de claves (Key Exchange)

### Cómo funciona actualmente

```
Cliente                    Servidor
  |                           |
  |--- TCP Connect ---------->|
  |<-- Welcome packet --------|
  |                           |
  |--- Login (XOR-3) -------->|  ← Mismas claves siempre
  |--- Game packets (SM) ---->|  ← Mismas claves siempre
```

### Cómo debería funcionar (con TLS)

```
Cliente                    Servidor
  |                           |
  |--- TCP Connect ---------->|
  |--- TLS ClientHello ------>|
  |<-- TLS ServerHello -------|  ← Certificado del servidor
  |--- Key Exchange --------->|  ← Claves únicas por sesión (Diffie-Hellman)
  |<-- Finished --------------|
  |                           |
  |=== Túnel encriptado ======|  ← Todo el tráfico protegido
  |--- Login --------------->|
  |--- Game packets -------->|
```

---

## Cómo se soluciona

### Opción 1: Agregar TLS como capa de transporte (recomendado)

```csharp
// En el servidor - Listener.cs
public async Task<Connection> AcceptAsync()
{
    var tcpClient = await _listener.AcceptTcpClientAsync();
    var sslStream = new SslStream(tcpClient.GetStream(), false);

    await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
    {
        ServerCertificate = _certificate,
        ClientCertificateRequired = false,
        EnabledSslProtocols = SslProtocols.Tls13,  // Solo TLS 1.3
    });

    return new Connection(sslStream, ...);
}

// En el cliente - ConnectionManager.cs
public async Task ConnectAsync(string host, int port)
{
    var tcp = new TcpClient();
    await tcp.ConnectAsync(host, port);

    var sslStream = new SslStream(tcp.GetStream(), false,
        (sender, cert, chain, errors) => ValidateCertificate(cert));

    await sslStream.AuthenticateAsClientAsync(host);
    // Ahora todo el tráfico sobre sslStream está protegido
}
```

**Ventajas de TLS:**
- Claves únicas por sesión (Perfect Forward Secrecy)
- Verificación de integridad (no se pueden modificar paquetes)
- Autenticación del servidor (el cliente verifica que habla con el servidor real)
- Industria estándar, auditado por miles de criptógrafos

### Opción 2: Mantener SimpleModulus pero agregar HMAC

Si TLS no es viable (por compatibilidad), al menos agregar verificación de integridad:

```csharp
// Agregar HMAC-SHA256 a cada paquete
using var hmac = new HMACSHA256(sessionKey);
var mac = hmac.ComputeHash(packetData);
// Agregar mac al final del paquete

// El receptor verifica el MAC antes de procesar
var expectedMac = hmac.ComputeHash(receivedPacketData);
if (!CryptographicOperations.FixedTimeEquals(receivedMac, expectedMac))
    throw new SecurityException("Packet integrity check failed");
```

### Opción 3: Mínimo viable - Hashing del password

Si no puedes cambiar el protocolo de red, al menos no envíes el password:

```csharp
// Cliente: enviar hash en lugar de password
var passwordHash = SHA256.HashData(Encoding.UTF8.GetBytes(password + serverChallenge));
// Enviar passwordHash en lugar de password

// Servidor: verificar contra hash almacenado
// Esto requiere un challenge-response para evitar replay attacks
```

---

## Concepto clave: Defense in Depth (Defensa en Profundidad)

No dependas de una sola capa de seguridad:

```
Capa 1: TLS (encripta todo el tráfico)
  └─ Capa 2: SimpleModulus (encriptación de aplicación, compatibilidad)
       └─ Capa 3: Hashing de passwords (BCrypt - ya implementado ✓)
            └─ Capa 4: Validación server-side (no confiar en el cliente)
```

Si una capa falla, las otras siguen protegiendo.

---

## Referencias

- [OWASP: Cryptographic Failures](https://owasp.org/Top10/A02_2021-Cryptographic_Failures/)
- [CWE-327: Broken Crypto](https://cwe.mitre.org/data/definitions/327.html)
- [Why XOR Encryption is Dangerous](https://crypto.stackexchange.com/questions/56281)
- [TLS 1.3 RFC 8446](https://www.rfc-editor.org/rfc/rfc8446)
- [Microsoft: SslStream Class](https://learn.microsoft.com/en-us/dotnet/api/system.net.security.sslstream)
