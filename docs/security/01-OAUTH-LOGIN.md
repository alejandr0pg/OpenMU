# 01 - Bypass de Autenticación OAuth

**Severidad:** CRÍTICA
**OWASP:** A07:2021 - Identification and Authentication Failures
**CWE:** CWE-287 (Improper Authentication), CWE-345 (Insufficient Verification of Data Authenticity)

---

## Qué es

OAuth es un protocolo de autorización que permite que un usuario se autentique mediante un proveedor externo (Google, Facebook, Apple) sin compartir su contraseña directamente con tu servidor. El flujo seguro es:

1. Cliente redirige al usuario al proveedor (Google, etc.)
2. El usuario se autentica con el proveedor
3. El proveedor devuelve un **token firmado criptográficamente**
4. Tu servidor **verifica la firma** del token contra las claves públicas del proveedor
5. Solo después de verificar, extrae el email y autentica al usuario

Si omites el paso 4, cualquiera puede fabricar un token falso con el email que quiera.

---

## Problema 1: Apple JWT sin verificación de firma

**Archivo:** `src/GameLogic/PlayerActions/OAuthLoginAction.cs:219-245`

```csharp
private string? ValidateAppleToken(string token)
{
    // Simple JWT payload decoding (no signature verification)
    var parts = token.Split('.');
    if (parts.Length < 2) return null;
    var payload = parts[1];
    // ... decode base64 ...
    // extrae email directamente del payload
}
```

### Por qué es un problema

Un JWT tiene 3 partes: `header.payload.signature`. El payload es simplemente Base64, **NO está cifrado**. Cualquiera puede crear un JWT con el email que quiera:

```bash
# Un atacante puede crear este "token" en 5 segundos:
echo -n '{"alg":"RS256"}' | base64    # header
echo -n '{"email":"admin@tuservidor.com"}' | base64  # payload
# firma = cualquier cosa, total no se verifica

# Token falso: eyJhbGciOiJSUzI1NiJ9.eyJlbWFpbCI6ImFkbWluQHR1c2Vydmlkb3IuY29tIn0.fake
```

Con este token, el atacante se autentica como `apple:admin@tuservidor.com` y obtiene acceso total.

### Cómo se soluciona

```csharp
// Usar la librería Microsoft.IdentityModel.Tokens
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

private async Task<string?> ValidateAppleToken(string token)
{
    var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
        "https://appleid.apple.com/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever());

    var config = await configManager.GetConfigurationAsync();

    var validationParams = new TokenValidationParameters
    {
        ValidIssuer = "https://appleid.apple.com",
        ValidAudience = "tu.bundle.id",  // Desde configuración, no hardcoded
        IssuerSigningKeys = config.SigningKeys,
        ValidateIssuerSigningKey = true,  // CRÍTICO: verifica la firma
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,          // Rechaza tokens expirados
    };

    var handler = new JwtSecurityTokenHandler();
    var principal = handler.ValidateToken(token, validationParams, out _);
    return principal.FindFirst("email")?.Value;
}
```

**Puntos clave:**
- `ValidateIssuerSigningKey = true` → Verifica que Apple firmó el token
- `ValidateIssuer = true` → Solo acepta tokens de `https://appleid.apple.com`
- `ValidateAudience = true` → Solo acepta tokens emitidos para tu app
- `ValidateLifetime = true` → Rechaza tokens expirados

---

## Problema 2: Google - No se valida el audience del token

**Archivo:** `src/GameLogic/PlayerActions/OAuthLoginAction.cs:152-187`

```csharp
var response = await _httpClient.GetAsync(
    $"https://oauth2.googleapis.com/tokeninfo?id_token={idToken}");
// Se extrae el email, pero NO se valida que el token
// fue emitido para TU client_id
```

### Por qué es un problema

Un token de Google válido emitido para **otra aplicación** también pasa esta validación. Un atacante podría:
1. Tener su propia app registrada en Google
2. Obtener un token legítimo de Google para esa app
3. Enviar ese token a tu servidor
4. Tu servidor lo acepta porque solo verifica que es un token de Google válido

### Cómo se soluciona

```csharp
// Después de obtener el tokeninfo, validar el audience:
if (doc.RootElement.TryGetProperty("aud", out var audProp))
{
    var audience = audProp.GetString();
    if (audience != _configuration.GoogleClientId)
    {
        _logger.LogWarning("Token audience mismatch: {0}", audience);
        return null;
    }
}

// Y validar que el email está verificado:
if (doc.RootElement.TryGetProperty("email_verified", out var verifiedProp))
{
    if (verifiedProp.GetString() != "true")
        return null;
}
```

---

## Problema 3: Security Code por defecto "123456"

**Archivo:** `src/GameLogic/PlayerActions/OAuthLoginAction.cs:75`

```csharp
newAccount.SecurityCode = "123456"; // Default
```

### Por qué es un problema

El Security Code en MU Online se usa para operaciones sensibles como:
- Resetear personaje
- Mover items del vault
- Operaciones del warehouse

Si todos los accounts OAuth tienen el mismo código "123456", un atacante que conozca este patrón puede realizar estas operaciones en cualquier cuenta OAuth.

### Cómo se soluciona

```csharp
// Opción 1: Generar código aleatorio y enviarlo por email
var random = new Random(Guid.NewGuid().GetHashCode());
newAccount.SecurityCode = random.Next(100000, 999999).ToString();
// Enviar por email al usuario

// Opción 2: Forzar al usuario a establecerlo en primer login
newAccount.SecurityCode = null; // Null = debe configurarlo
newAccount.RequiresSecurityCodeSetup = true;
```

---

## Problema 4: Credenciales OAuth hardcoded como placeholders

**Archivo:** `src/GameLogic/PlayerActions/OAuthLoginAction.cs:160-161, 194`

```csharp
{ "client_id", "YOUR_GOOGLE_CLIENT_ID" },
{ "client_secret", "YOUR_GOOGLE_CLIENT_SECRET" },
// ...
$"client_id=YOUR_FACEBOOK_CLIENT_ID&...client_secret=YOUR_FACEBOOK_CLIENT_SECRET"
```

### Por qué es un problema

- `client_secret` NUNCA debe estar en código fuente (ni siquiera como placeholder)
- Si alguien commitea el secret real, queda en el historial de git para siempre
- El `client_secret` en la URL de Facebook es vulnerable a quedar en logs de proxy/servidor

### Cómo se soluciona

```csharp
// Inyectar configuración desde variables de entorno o secret manager:
public class OAuthSettings
{
    public string GoogleClientId { get; set; }
    public string GoogleClientSecret { get; set; }
    public string FacebookClientId { get; set; }
    public string FacebookClientSecret { get; set; }
    public string AppleBundleId { get; set; }
}

// Registrar en DI:
services.Configure<OAuthSettings>(configuration.GetSection("OAuth"));

// Usar en la clase:
private readonly OAuthSettings _settings;
// ...
{ "client_id", _settings.GoogleClientId },
```

Y en `appsettings.json` (o mejor, en variables de entorno):
```json
{
  "OAuth": {
    "GoogleClientId": "${GOOGLE_CLIENT_ID}",
    "GoogleClientSecret": "${GOOGLE_CLIENT_SECRET}"
  }
}
```

---

## Problema 5: Excepciones silenciadas

**Archivo:** `src/GameLogic/PlayerActions/OAuthLoginAction.cs:185, 215`

```csharp
catch { }
return null;
```

### Por qué es un problema

Si la validación del token falla por cualquier razón (red, formato, etc.), se silencia completamente. Esto:
- Oculta ataques en progreso
- Dificulta debugging
- Puede enmascarar vulnerabilidades

### Cómo se soluciona

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Error validating Google OAuth token");
    return null;
}
```

---

## Referencias

- [OWASP: Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [CWE-287: Improper Authentication](https://cwe.mitre.org/data/definitions/287.html)
- [Apple Sign In - Server to Server](https://developer.apple.com/documentation/sign_in_with_apple/sign_in_with_apple_rest_api/verifying_a_user)
- [Google OAuth ID Token Verification](https://developers.google.com/identity/protocols/oauth2/openid-connect#validatinganidtoken)
- [JWT.io - JSON Web Tokens Introduction](https://jwt.io/introduction)
