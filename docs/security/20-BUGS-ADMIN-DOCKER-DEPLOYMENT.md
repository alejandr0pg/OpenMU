# 20 - Vulnerabilidades de Admin Panel, Docker y Deployment

**Severidad:** CRÍTICA a ALTA
**CWE:** CWE-306 (Missing Authentication), CWE-798 (Hardcoded Credentials), CWE-209 (Information Exposure Through Error Messages)

---

## CRÍTICO 1: Admin Panel — CERO autenticación a nivel de aplicación

**Archivos:**
- `src/Web/AdminPanel/Startup.cs:47-74`
- `src/Web/AdminPanel/WebApplicationExtensions.cs:40-129`

```csharp
// Startup.cs — ConfigureServices:
// NO hay AddAuthentication()
// NO hay AddAuthorization()

// WebApplicationExtensions.cs:
app.UseAntiforgery();     // ← Solo antiforgery
// NO hay app.UseAuthentication()
// NO hay app.UseAuthorization()
```

### El problema

El Admin Panel de OpenMU **no tiene framework de autenticación ni autorización** a nivel de aplicación. Depende **completamente** de nginx basic auth como única barrera.

### Por qué es crítico

```
Internet → nginx (basic auth) → Admin Panel (SIN AUTH)
                                     ↓
                            Acceso total: shutdown, config, accounts

Si nginx se misconfigura, bypasea, o se accede directamente al puerto:
Internet → Admin Panel → Control total del servidor
```

### Verificación: 0 páginas protegidas

**Ninguna** de las páginas Blazor tiene `[Authorize]`:
- `Pages/Accounts.razor` — gestión de cuentas
- `Pages/Servers.razor` — iniciar/detener servidores
- `Pages/Setup.razor` — configuración del sistema
- `Pages/LogFiles.razor` — lectura de logs
- `Pages/Plugins.razor` — gestión de plugins

### Fix

```csharp
// Startup.cs:
services.AddAuthentication("Bearer")
    .AddJwtBearer(options => { /* ... */ });
services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// WebApplicationExtensions.cs:
app.UseAuthentication();
app.UseAuthorization();

// Cada página:
@attribute [Authorize(Roles = "Admin")]
```

---

## CRÍTICO 2: Docker — credenciales hardcoded en todos los deployments

### PostgreSQL

**Archivo:** `deploy/all-in-one/docker-compose.yml:39-42`

```yaml
environment:
  POSTGRES_PASSWORD: admin
  POSTGRES_DB: openmu
  POSTGRES_USER: postgres
```

Idéntico en `deploy/all-in-one-traefik/docker-compose.yml:34-37`.

### Redis sin autenticación

**Archivo:** `deploy/distributed/docker-compose.yml:75-81`

```yaml
redis-state:
  image: redis
  restart: always
  ports:
    - 6379
  environment:
    ALLOW_EMPTY_PASSWORD: "yes"
```

### MinIO con credenciales públicas

**Archivo:** `deploy/distributed/docker-compose.yml:65-69`

```yaml
environment:
  - MINIO_ACCESS_KEY=loki
  - MINIO_SECRET_KEY=supersecret
  - MINIO_PROMETHEUS_AUTH_TYPE=public
```

### Fix

```yaml
# Usar Docker secrets:
secrets:
  db_password:
    file: ./secrets/db_password.txt

services:
  postgres:
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/db_password
    secrets:
      - db_password

  redis-state:
    command: redis-server --requirepass ${REDIS_PASSWORD}
```

---

## CRÍTICO 3: .htpasswd con credenciales por defecto en git

**Archivos:**
- `deploy/all-in-one/.htpasswd`
- `deploy/distributed/.htpasswd`
- `deploy/all-in-one-traefik/.htpasswd`

```
# default is admin:openmu, hashed with bcrypt.
admin:$2y$10$xYL2d/QEukwGmX0uNZubsunL0qcANuTYkpapRVdlu5q3ymCpvOEh.
```

### El problema

Las credenciales `admin:openmu` están:
1. Documentadas en el comentario del archivo
2. Comprometidas en el repositorio git (historial permanente)
3. Idénticas en los tres deployments
4. El hash bcrypt es crackeable con la contraseña conocida

### Fix

```bash
# Generar credenciales únicas fuera del repo:
htpasswd -Bc /etc/nginx/.htpasswd admin
# Nunca commitear .htpasswd al repositorio
echo ".htpasswd" >> .gitignore
```

---

## ALTA 4: DetailedErrors habilitado y AllowedHosts sin restricción

**Archivos:**
- `src/Startup/appsettings.json:2-3`
- `src/Dapr/AdminPanel.Host/appsettings.json:1-3`

```json
{
  "DetailedErrors": true,
  "AllowedHosts": "*"
}
```

### El problema

- **DetailedErrors: true** — Expone stack traces completos, rutas de archivo internas, y detalles de implementación en errores de Blazor
- **AllowedHosts: "*"** — Acepta requests desde cualquier hostname, vulnerable a Host header injection

### Información expuesta por DetailedErrors

```
System.NullReferenceException: Object reference not set...
   at MUnique.OpenMU.GameLogic.Player.TryAddMoney(Int32 value)
     in /app/src/GameLogic/Player.cs:line 971
   at MUnique.OpenMU.GameLogic.PlayerActions.Trade.TradeMoneyAction...
```

Un atacante obtiene: rutas de código fuente, stack traces, nombres de métodos internos, versiones de framework.

### Fix

```json
{
  "DetailedErrors": false,
  "AllowedHosts": "yourdomain.com;admin.yourdomain.com"
}
```

---

## ALTA 5: Nginx — sin security headers en ningún deployment

**Archivos:**
- `deploy/all-in-one/nginx/nginx.dev.conf`
- `deploy/all-in-one/nginx/nginx.prod443.conf`
- `deploy/distributed/nginx.dev.conf`
- `deploy/distributed/nginx.prod443.conf`

### Headers faltantes

| Header | Propósito | Estado |
|--------|-----------|--------|
| `X-Frame-Options` | Prevenir clickjacking | **FALTA** |
| `Content-Security-Policy` | Prevenir XSS | **FALTA** |
| `X-Content-Type-Options` | Prevenir MIME sniffing | **FALTA** |
| `Strict-Transport-Security` | Forzar HTTPS | **FALTA** |
| `X-XSS-Protection` | Filtro XSS del browser | **FALTA** |
| `Referrer-Policy` | Controlar información referrer | **FALTA** |

### Fix para nginx

```nginx
server {
    # Security headers
    add_header X-Frame-Options "DENY" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header Content-Security-Policy "default-src 'self'; script-src 'self'" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;
}
```

---

## ALTA 6: Plugin system — carga de assemblies sin verificación de firma

**Archivo:** `src/PlugIns/PlugInManager.cs:412`

```csharp
var assembly = Assembly.LoadFile("plugins\\" + configuration.ExternalAssemblyName);
this.DiscoverAndRegisterPlugIns(assembly);
```

**Líneas 421-426:**
```csharp
else if (!string.IsNullOrEmpty(configuration.CustomPlugInSource))
{
    this._logger.LogWarning($"Custom plugin source found...");
    /* TODO: Implement code signing, if we really need this feature.
    Assembly customPlugInAssembly = this.CompileCustomPlugInAssembly(configuration);
    this.DiscoverAndRegisterPlugIns(customPlugInAssembly);*/
}
```

### El problema

1. `Assembly.LoadFile()` carga cualquier DLL sin verificar firma digital
2. El path es relativo — vulnerable a path traversal si `ExternalAssemblyName` contiene `..`
3. Custom plugin source (compilación dinámica) está comentado pero existe la infraestructura
4. El TODO reconoce que code signing no está implementado

### Escenario de ataque

```
1. Atacante obtiene acceso al directorio plugins/ (ej: via admin panel)
2. Sube malicious.dll
3. Configura un plugin con ExternalAssemblyName = "malicious.dll"
4. El servidor carga el assembly → ejecución de código arbitrario
```

### Path traversal

```
ExternalAssemblyName = "..\\..\\..\\tmp\\evil.dll"
→ Assembly.LoadFile("plugins\\..\\..\\..\\tmp\\evil.dll")
→ Carga /tmp/evil.dll
```

### Fix

```csharp
private void ReadConfiguration(PlugInConfiguration config, HashSet<string> loaded)
{
    if (!string.IsNullOrEmpty(config.ExternalAssemblyName))
    {
        // Sanitizar path
        var safeName = Path.GetFileName(config.ExternalAssemblyName);
        if (safeName != config.ExternalAssemblyName)
        {
            _logger.LogError("Plugin path traversal attempt: {name}", config.ExternalAssemblyName);
            return;
        }

        var fullPath = Path.GetFullPath(Path.Combine("plugins", safeName));
        var assembly = Assembly.LoadFile(fullPath);

        // Verificar firma
        var name = assembly.GetName();
        if (name.GetPublicKeyToken()?.Length == 0)
        {
            _logger.LogError("Plugin {name} is not signed", safeName);
            return;
        }

        this.DiscoverAndRegisterPlugIns(assembly);
    }
}
```

---

## MEDIA 7: Grafana con credenciales por defecto

**Archivo:** `deploy/distributed/grafana.ini`

Grafana usa credenciales por defecto `admin:admin`. Aunque está detrás de nginx basic auth, si un atacante bypasea nginx tiene acceso directo a Grafana con credenciales conocidas.

---

## Tabla resumen

| # | Bug | Tipo | Impacto | Ubicación |
|---|-----|------|---------|-----------|
| 1 | Admin sin auth | Missing Auth | **Control total** | AdminPanel/Startup.cs |
| 2 | Credenciales Docker hardcoded | Hardcoded Creds | **Acceso BD/Redis** | deploy/docker-compose.yml |
| 3 | .htpasswd default en git | Default Creds | **Bypass auth** | deploy/.htpasswd |
| 4 | DetailedErrors + AllowedHosts | Info Exposure | Reconocimiento | appsettings.json |
| 5 | Nginx sin security headers | Missing Headers | XSS/Clickjacking | nginx/*.conf |
| 6 | Plugins sin firma | Code Execution | **RCE** | PlugInManager.cs:412 |
| 7 | Grafana default creds | Default Creds | Acceso monitoring | grafana.ini |
