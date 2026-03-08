# 02 - Endpoints API Admin sin Autenticación

**Severidad:** CRÍTICA
**OWASP:** A01:2021 - Broken Access Control
**CWE:** CWE-306 (Missing Authentication for Critical Function)

---

## Qué es

Access Control (Control de Acceso) significa que tu sistema verifica **quién** está haciendo una petición antes de ejecutarla. Sin autenticación en endpoints administrativos, cualquier persona que conozca la URL puede ejecutar acciones privilegiadas.

---

## Dónde está el problema

**Archivo:** `src/Web/AdminPanel/API/ServerController.cs`

```csharp
[Route("api/")]
public class ServerController : Controller  // Sin [Authorize]
{
    [Route("send/{id=0}")]
    public async Task<IActionResult> SendGlobalMessage(int id, [FromQuery(Name = "msg")] string msg)
    {
        var server = (GameServer)_gameServers.Values.ElementAt(id);
        await server.Context.SendGlobalNotificationAsync(msg);
        return Ok("Done");
    }

    [Route("is-online/{accountName=0}")]
    public async Task<bool> GetIsOnlineAsync(string accountName)
    // ...

    [Route("status")]
    public async Task<IActionResult> GetServerStatus()
    // ...
}
```

**Ningún endpoint tiene `[Authorize]`** y no se encontró middleware de autenticación global para la ruta `/api/`.

---

## Por qué es un problema

### Escenario de ataque 1: Spam/Phishing masivo
```bash
# Cualquiera puede enviar mensajes globales a todos los jugadores:
curl "http://tu-servidor:8080/api/send/0?msg=ADMIN: Server cerrará en 5 min. Guarden items en www.phishing.com"
```
Los jugadores ven un mensaje "oficial" y podrían caer en phishing.

### Escenario de ataque 2: Enumeración de cuentas
```bash
# Verificar si una cuenta existe y está online:
curl "http://tu-servidor:8080/api/is-online/admin"
# true/false

# Script para enumerar todas las cuentas:
for name in $(cat wordlist.txt); do
  result=$(curl -s "http://tu-servidor:8080/api/is-online/$name")
  echo "$name: $result"
done
```
Esto permite a un atacante saber qué cuentas existen y cuáles están activas.

### Escenario de ataque 3: Reconocimiento del servidor
```bash
curl "http://tu-servidor:8080/api/status"
# Devuelve info interna del servidor: versión, estado, jugadores conectados
```

---

## Cómo se soluciona

### Paso 1: Agregar autenticación al controller

```csharp
using Microsoft.AspNetCore.Authorization;

[Route("api/")]
[Authorize(Roles = "Admin")]  // Requiere rol Admin
public class ServerController : Controller
{
    // ...
}
```

### Paso 2: Configurar autenticación en Program.cs/Startup.cs

```csharp
// Opción A: API Key para acceso programático
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

// Opción B: JWT Bearer tokens
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            // ...
        };
    });

// Importante: el orden importa
app.UseAuthentication();
app.UseAuthorization();
```

### Paso 3: Implementar API Key Authentication (ejemplo simple)

```csharp
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKey))
            return Task.FromResult(AuthenticateResult.Fail("Missing API Key"));

        if (apiKey != _options.ValidApiKey)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key"));

        var claims = new[] { new Claim(ClaimTypes.Role, "Admin") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, Scheme.Name)));
    }
}
```

### Paso 4: Rate limiting para el endpoint is-online

Incluso con autenticación, agregar rate limiting previene abuso:

```csharp
// En Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 30;
    });
});

// En el controller
[EnableRateLimiting("api")]
[Authorize(Roles = "Admin")]
public class ServerController : Controller
```

---

## Problema adicional: Input sin sanitizar en SendGlobalMessage

```csharp
public async Task<IActionResult> SendGlobalMessage(int id, [FromQuery(Name = "msg")] string msg)
{
    // 'msg' se envía directamente a todos los jugadores sin ninguna validación
    await server.Context.SendGlobalNotificationAsync(msg);
}
```

Aunque el cliente probablemente renderiza texto plano, deberías:
- Limitar longitud del mensaje
- Filtrar caracteres de control
- Validar que `id` está en rango

```csharp
[Route("send/{id=0}")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> SendGlobalMessage(int id, [FromQuery(Name = "msg")] string msg)
{
    if (string.IsNullOrWhiteSpace(msg) || msg.Length > 200)
        return BadRequest("Message must be 1-200 characters");

    if (id < 0 || id >= _gameServers.Count)
        return BadRequest("Invalid server id");

    var server = (GameServer)_gameServers.Values.ElementAt(id);
    await server.Context.SendGlobalNotificationAsync(msg);
    return Ok("Done");
}
```

---

## Lección clave

**Principio de seguridad: Deny by Default (Denegar por defecto)**

En lugar de agregar `[Authorize]` a cada controller, configura autenticación requerida globalmente y marca excepciones con `[AllowAnonymous]`:

```csharp
// En Program.cs - requiere autenticación para TODO
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

Así, si olvidas agregar `[Authorize]` a un nuevo endpoint, sigue protegido por defecto.

---

## Referencias

- [OWASP: Broken Access Control](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)
- [CWE-306: Missing Authentication](https://cwe.mitre.org/data/definitions/306.html)
- [ASP.NET Core Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/)
- [ASP.NET Core Rate Limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
