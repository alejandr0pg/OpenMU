# 03 - Credenciales y Secretos Hardcoded

**Severidad:** ALTA
**OWASP:** A02:2021 - Cryptographic Failures (exposure of sensitive data)
**CWE:** CWE-798 (Use of Hard-coded Credentials), CWE-259 (Use of Hard-coded Password)

---

## Qué es

"Hardcoded credentials" significa que contraseñas, claves API, o secretos están escritos directamente en el código fuente o archivos de configuración que se commitean al repositorio. Cualquiera con acceso al repo (o a un backup, o a una imagen Docker) puede extraerlos.

---

## Problema 1: Credenciales de base de datos en secrets.json

**Archivo:** `deploy/distributed/dapr-components/secrets.json`

```json
{
  "connectionStrings": {
    "EntityDataContext": "Server=database;Port=5432;User Id=postgres;Password=admin;...",
    "ConfigurationContext": "Server=database;Port=5432;User Id=config;Password=config;...",
    "AccountContext": "Server=database;Port=5432;User Id=account;Password=account;...",
    "FriendContext": "Server=database;Port=5432;User Id=friend;Password=friend;...",
    "GuildContext": "Server=database;Port=5432;User Id=guild;Password=guild;..."
  }
}
```

### Por qué es un problema

1. **Contraseñas triviales:** `admin`, `config`, `account`, `friend`, `guild` — son literalmente el nombre del usuario
2. **Usuario postgres con password `admin`:** Es el superusuario de PostgreSQL. Con esto se puede:
   - Leer TODAS las tablas (incluyendo hashes de passwords)
   - Modificar datos (dar items, zen, nivel)
   - Borrar toda la base de datos
   - Ejecutar comandos del sistema operativo (via `COPY FROM PROGRAM`)
3. **Archivo commiteado en git:** Aunque lo borres después, permanece en el historial

### Escenario de ataque

```bash
# Si el puerto 5432 está expuesto (common en Docker mal configurado):
psql -h tu-servidor -U postgres -W  # Password: admin

# Listar todas las cuentas:
SELECT "LoginName", "PasswordHash", "EMail" FROM data."Account";

# Dar items a cualquier personaje:
INSERT INTO data."Item" ...

# Borrar toda la base de datos:
DROP DATABASE openmu;
```

### Cómo se soluciona

**Paso 1:** Usar variables de entorno (nunca archivos en el repo)

```yaml
# docker-compose.yml
services:
  database:
    environment:
      POSTGRES_PASSWORD: ${DB_POSTGRES_PASSWORD}  # Desde .env (no commiteado)

  gameserver:
    environment:
      DB_CONNECTION: ${DB_CONNECTION_STRING}
```

**Paso 2:** Crear un `.env` file (agregar a `.gitignore`):

```bash
# .env (NUNCA commitear)
DB_POSTGRES_PASSWORD=unPasswordSeguro123!@#
DB_CONNECTION_STRING=Server=database;Port=5432;User Id=app;Password=otroPasswordSeguro;Database=openmu
```

**Paso 3:** Usar contraseñas fuertes y usuarios con privilegios mínimos:

```sql
-- Crear usuarios con acceso mínimo (principio de least privilege):
CREATE USER app_readonly WITH PASSWORD 'pwd_fuerte_1';
GRANT SELECT ON ALL TABLES IN SCHEMA data TO app_readonly;

CREATE USER app_account WITH PASSWORD 'pwd_fuerte_2';
GRANT SELECT, INSERT, UPDATE ON data."Account" TO app_account;
-- Solo las tablas que necesita

-- NUNCA usar postgres para la aplicación
```

**Paso 4:** Si ya commiteaste secretos, rotarlos inmediatamente:

```bash
# 1. Cambiar TODAS las contraseñas en la base de datos
ALTER USER postgres WITH PASSWORD 'nuevo_password_seguro';

# 2. Limpiar del historial de git (opcional pero recomendado):
git filter-branch --force --index-filter \
  'git rm --cached --ignore-unmatch deploy/distributed/dapr-components/secrets.json' \
  HEAD
```

---

## Problema 2: Claves de encriptación de red en código fuente

**Archivo:** `src/Network/SimpleModulus/PipelinedSimpleModulusEncryptor.cs:21-26`

```csharp
public static readonly SimpleModulusKeys DefaultServerKey =
    SimpleModulusKeys.CreateEncryptionKeys(new uint[] {
        73326, 109989, 98843, 171058, 13169, 19036, 35482, 29587, 62004, 64409, 35374, 64599
    });

public static readonly SimpleModulusKeys DefaultClientKey =
    SimpleModulusKeys.CreateEncryptionKeys(new uint[] {
        128079, 164742, 70235, 106898, 23489, 11911, 19816, 13647, 48413, 46165, 15171, 37433
    });
```

### Por qué es un problema

Estas claves son públicas (están en el código fuente abierto de OpenMU). Cualquiera puede:
1. Descargar el código
2. Usar estas claves para descifrar todo el tráfico de red
3. Leer passwords, chat, items, posiciones de jugadores

**Nota:** Esto es una limitación conocida del protocolo MU Online original. El protocolo fue diseñado en ~2003 y no fue pensado para seguridad moderna. Ver documento 04 para más detalles.

### Cómo se soluciona (si es viable)

Para un servidor privado donde controlas el cliente:

```csharp
// Opción 1: Generar claves únicas por servidor
public static SimpleModulusKeys GenerateRandomKeys()
{
    using var rng = RandomNumberGenerator.Create();
    var values = new uint[12];
    for (int i = 0; i < 12; i++)
    {
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        values[i] = BitConverter.ToUInt32(bytes) % 200000; // Rango similar
    }
    return SimpleModulusKeys.CreateEncryptionKeys(values);
}

// Opción 2 (mejor): Agregar TLS como capa adicional
// Envolver la conexión TCP en un SslStream
```

---

## Problema 3: IP del servidor hardcoded en cliente

**Archivo:** `/muonline/appsettings.json`

```json
{
  "ConnectServerHost": "168.220.89.83",
  "ConnectServerPort": 44405,
  "ClientSerial": "k1Pk2jcET48mxL3b"
}
```

### Por qué es un problema

- La IP expone tu servidor a ataques dirigidos (DDoS, port scanning)
- El ClientSerial es un "shared secret" que identifica versiones del cliente — si es público, pierde su utilidad como control de acceso

### Cómo se soluciona

- Usar un dominio DNS en lugar de IP directa (permite cambiar la IP sin redistribuir el cliente)
- El ClientSerial debería ser validado server-side pero NO tratado como un mecanismo de seguridad real

---

## Regla general: Gestión de secretos

| Tipo de secreto | Dónde guardarlo | Ejemplo |
|---|---|---|
| DB passwords | Variables de entorno / Secret manager | `$DB_PASSWORD` |
| API keys (OAuth) | Variables de entorno / Secret manager | `$GOOGLE_CLIENT_SECRET` |
| Claves criptográficas | Archivos fuera del repo / HSM | `/etc/secrets/keys.json` |
| Certificados TLS | Montaje de volumen / cert-manager | `/certs/server.pem` |
| Tokens de sesión | Generados en runtime | `Guid.NewGuid()` |

**NUNCA** commitear: `.env`, `secrets.json`, `*.pem`, `*.p8`, `*.pfx`

Tu `.gitignore` debe incluir:
```
*.env
secrets.json
*.pem
*.p8
*.pfx
appsettings.*.local.json
```

---

## Referencias

- [OWASP: Credential Management Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Credential_Management_Cheat_Sheet.html)
- [CWE-798: Hard-coded Credentials](https://cwe.mitre.org/data/definitions/798.html)
- [12 Factor App: Config](https://12factor.net/config)
- [ASP.NET Core: Secret Manager](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
