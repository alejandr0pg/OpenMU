# 08 - Información Sensible Expuesta

**Severidad:** MEDIA
**OWASP:** A09:2021 - Security Logging and Monitoring Failures
**CWE:** CWE-532 (Information Exposure Through Log Files), CWE-200 (Information Exposure)

---

## Qué es

La exposición de información ocurre cuando el sistema revela datos internos (estructuras, versiones, errores detallados, credenciales) a usuarios no autorizados. Esto facilita ataques al dar al atacante un "mapa" del sistema.

---

## Problema 1: Logging de información sensible

**Archivo:** `src/GameLogic/PlayerActions/OAuthLoginAction.cs:22`

```csharp
player.Logger.LogInformation($"OAuth Login Attempt: Provider={provider}");
```

### Por qué es un problema

Aunque este log específico es inofensivo, el patrón de logging en la aplicación no tiene una política clara. Riesgos comunes:

```csharp
// MALO: loguear tokens
logger.LogInformation($"Token received: {token}");

// MALO: loguear passwords (incluso por error)
logger.LogDebug($"Login attempt: user={username}, pass={password}");

// MALO: loguear connection strings
logger.LogInformation($"Connecting to: {connectionString}");
```

Si los logs se almacenan en texto plano, un atacante que acceda al servidor de logs obtiene credenciales.

### Cómo se soluciona

```csharp
// BIEN: loguear solo lo necesario, sin datos sensibles
logger.LogInformation("OAuth login attempt for provider {Provider}", provider);

// BIEN: enmascarar datos parcialmente
logger.LogInformation("Login attempt for user: {User}",
    username.Length > 3 ? username[..3] + "***" : "***");

// BIEN: usar structured logging sin interpolar
logger.LogInformation("Login from {IP}", connection.RemoteEndPoint);
// Y configurar el sink para NO almacenar ciertos campos

// Configurar en appsettings.json qué niveles van a producción:
{
    "Logging": {
        "LogLevel": {
            "Default": "Warning",           // Solo warnings y errores
            "Microsoft": "Warning",
            "MUnique.OpenMU": "Information"  // Tu código: info+
        }
    }
}
```

---

## Problema 2: Errores detallados expuestos al cliente

### Escenario

Si el servidor envía mensajes de error detallados al cliente:

```
Error: NpgsqlException: 42P01: relation "data.Account" does not exist
at Npgsql.NpgsqlConnector.ReadMessageLong(...)
```

Esto revela:
- Base de datos: PostgreSQL (Npgsql)
- Esquema: `data`
- Tabla: `Account`
- Stack trace con versiones de librerías

### Cómo se soluciona

```csharp
// El cliente solo debe recibir códigos genéricos:
catch (Exception ex)
{
    logger.LogError(ex, "Database error during login");  // Log completo server-side

    // Al cliente: solo un código genérico
    await player.InvokeViewPlugInAsync<IShowLoginResultPlugIn>(
        p => p.ShowLoginResultAsync(LoginResult.ConnectionError));
    // NO enviar: ex.Message, ex.StackTrace, etc.
}
```

---

## Problema 3: Admin panel expone estado interno del servidor

**Archivo:** `src/Web/AdminPanel/API/ServerController.cs`

```csharp
[Route("status")]
public async Task<IActionResult> GetServerStatus()
```

### Qué podría exponer

- Número de jugadores online
- Estado de cada game server
- Versión del software
- Uso de memoria/CPU
- Configuración del servidor

### Cómo se soluciona

1. **Autenticación** (ver documento 02)
2. **Limitar información al mínimo necesario:**

```csharp
// Endpoint público (si es necesario para launcher):
[AllowAnonymous]
[Route("ping")]
public IActionResult Ping() => Ok(new { status = "online" });

// Endpoint admin (protegido):
[Authorize(Roles = "Admin")]
[Route("status")]
public IActionResult GetStatus() => Ok(new
{
    players = _gameServers.Sum(s => s.Value.OnlineCount),
    servers = _gameServers.Select(s => new
    {
        id = s.Key,
        online = s.Value.IsOnline,
        players = s.Value.OnlineCount
    })
    // NO incluir: versión, memoria, CPU, IPs internas, etc.
});
```

---

## Problema 4: Archivo de claves Apple en el repositorio

**Archivo:** `/muonline/keys/AuthKey_7UWSKR447S.p8`

### Por qué es un problema

Un archivo `.p8` es una clave privada de Apple. Si está commiteado:
- Cualquiera con acceso al repo puede firmar requests como tu aplicación
- Puede crear tokens de Sign in with Apple para cualquier usuario
- Apple podría revocar tu clave si detecta exposición

### Cómo se soluciona

```bash
# 1. Agregar a .gitignore inmediatamente
echo "keys/" >> .gitignore
echo "*.p8" >> .gitignore

# 2. Remover del historial de git
git filter-branch --force --index-filter \
  'git rm -rf --cached --ignore-unmatch keys/' HEAD

# 3. Revocar la clave en Apple Developer Portal y generar una nueva

# 4. Almacenar la nueva clave fuera del repositorio
# En producción: variable de entorno o secret manager
# En desarrollo: archivo local no commiteado
```

---

## Problema 5: Client Serial expuesto

**Archivo:** `/muonline/appsettings.json`

```json
{
    "ClientSerial": "k1Pk2jcET48mxL3b",
    "ClientVersion": "2.04d"
}
```

### Por qué importa

El ClientSerial se usa para verificar que el cliente es "legítimo". Si es público:
- Bots pueden usarlo para conectarse al servidor
- Otros clientes modificados pueden usarlo
- Pierde toda utilidad como mecanismo de control

### Perspectiva realista

En MU Online, el ClientSerial nunca fue un mecanismo de seguridad real — es fácilmente extraíble del ejecutable original. Pero si lo usas como una capa adicional:

```csharp
// Server-side: no depender solo del serial
// Agregar verificaciones adicionales:
// - Rate limiting por IP
// - Captcha para registro
// - Monitoreo de comportamiento (detección de bots)
```

---

## Checklist de información a proteger

| Dato | Dónde NO debe aparecer |
|---|---|
| Passwords/tokens | Logs, mensajes de error, URLs |
| Connection strings | Código fuente, logs, archivos commiteados |
| Stack traces | Respuestas al cliente |
| Versiones de software | Headers HTTP, respuestas públicas |
| IPs internas | Logs accesibles, errores al cliente |
| Claves privadas (.p8, .pem) | Repositorios de código |
| Estructura de BD | Mensajes de error al cliente |

---

## Configuración recomendada para producción

```csharp
// En Program.cs / Startup.cs
if (app.Environment.IsProduction())
{
    // No mostrar página de errores detallada
    app.UseExceptionHandler("/error");

    // Headers de seguridad
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Remove("Server");        // Ocultar servidor
        context.Response.Headers.Remove("X-Powered-By");  // Ocultar framework
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        await next();
    });
}
else
{
    // Solo en desarrollo: mostrar errores detallados
    app.UseDeveloperExceptionPage();
}
```

---

## Referencias

- [OWASP: Logging Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html)
- [OWASP: Error Handling](https://cheatsheetseries.owasp.org/cheatsheets/Error_Handling_Cheat_Sheet.html)
- [CWE-532: Log File Information Exposure](https://cwe.mitre.org/data/definitions/532.html)
- [CWE-200: Information Exposure](https://cwe.mitre.org/data/definitions/200.html)
