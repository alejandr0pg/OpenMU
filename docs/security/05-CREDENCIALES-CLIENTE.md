# 05 - Almacenamiento Inseguro de Credenciales en Cliente

**Severidad:** ALTA
**OWASP:** A02:2021 - Cryptographic Failures
**CWE:** CWE-257 (Storing Passwords in Recoverable Format), CWE-321 (Use of Hard-coded Cryptographic Key)

---

## Qué es

Cuando un cliente guarda credenciales (username/password) para "recordar login", debe hacerlo de forma que un atacante no pueda recuperar el password original, incluso si tiene acceso al archivo cifrado.

---

## Dónde está el problema

**Archivo:** `Client.Main/Core/Utilities/CredentialsManager.cs`

```csharp
public static class CredentialsManager
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MuOnlineClientSalt");

    private static byte[] GetKey()
    {
        // Clave derivada de un string HARDCODED en el código fuente
        using var derive = new Rfc2898DeriveBytes(
            "MuOnlineSecret",        // ← Hardcoded, público en GitHub
            Entropy,                  // ← Hardcoded, público en GitHub
            1000,                     // ← Iteraciones bajas
            HashAlgorithmName.SHA256);
        return derive.GetBytes(32);
    }

    private static byte[] GetIV()
    {
        // MISMO derive que GetKey() pero tomando 16 bytes
        // Esto significa que el IV es DETERMINÍSTICO
        using var derive = new Rfc2898DeriveBytes(
            "MuOnlineSecret", Entropy, 1000, HashAlgorithmName.SHA256);
        return derive.GetBytes(16);
    }
}
```

Las credenciales se guardan en: `%APPDATA%/MuOnline/credentials.dat`

---

## Por qué es un problema

### Problema 1: Clave de encriptación hardcoded

La "clave" AES se deriva de `"MuOnlineSecret"` + `"MuOnlineClientSalt"`. Ambos valores están en el código fuente. Esto significa que la "encriptación" es reversible por cualquiera que lea el código:

```csharp
// Un atacante puede crear este script:
static void Main()
{
    var salt = Encoding.UTF8.GetBytes("MuOnlineClientSalt");
    using var derive = new Rfc2898DeriveBytes("MuOnlineSecret", salt, 1000, HashAlgorithmName.SHA256);
    var key = derive.GetBytes(32);
    var iv = derive.GetBytes(16);  // BUG: esto no da el mismo IV (ver abajo)

    // Pero el bug es que GetKey() y GetIV() crean instancias SEPARADAS
    // así que el IV = primeros 16 bytes del stream PBKDF2 de otra instancia
    // = exactamente los primeros 16 bytes de la key derivation
    // Ambos son determinísticos, así que son calculables.

    var encrypted = File.ReadAllBytes(
        Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData),
        "MuOnline", "credentials.dat"));

    using var aes = Aes.Create();
    aes.Key = key;
    aes.IV = GetIV(); // Calculable desde el código fuente
    // ... descifrar y obtener username + password en texto plano
}
```

**Impacto:** Malware, otro programa, o alguien con acceso al PC puede extraer las credenciales en segundos.

### Problema 2: IV determinístico

```csharp
// GetKey() crea una instancia de Rfc2898DeriveBytes y saca 32 bytes
// GetIV()  crea OTRA instancia con los mismos parámetros y saca 16 bytes
```

El IV (Initialization Vector) de AES **debe ser aleatorio y único por cada encriptación**. Un IV determinístico significa:
- Si el usuario cambia su password y guarda de nuevo, un atacante puede comparar los dos archivos cifrados para obtener información
- Se viola la propiedad IND-CPA (Indistinguishability under Chosen-Plaintext Attack) de AES-CBC

### Problema 3: Solo 1000 iteraciones de PBKDF2

OWASP recomienda **mínimo 600,000 iteraciones** para SHA-256 (2023). 1000 iteraciones se rompen en microsegundos con hardware moderno.

---

## Cómo se soluciona

### Opción 1 (Mejor): Usar DPAPI en Windows / Keychain en macOS

Los sistemas operativos ya tienen mecanismos seguros para almacenar credenciales:

```csharp
public static class CredentialsManager
{
    public static void SaveCredentials(string username, string password)
    {
        var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new { username, password }));

        if (OperatingSystem.IsWindows())
        {
            // DPAPI: encripta con la clave del usuario de Windows
            // Solo ese usuario en ese PC puede descifrar
            var encrypted = ProtectedData.Protect(
                data, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(CredentialsPath, encrypted);
        }
        else if (OperatingSystem.IsMacOS())
        {
            // Usar macOS Keychain via Security framework
            SaveToKeychain("MuOnline", username, password);
        }
        else
        {
            // Linux: usar libsecret o archivo con permisos 600
            SaveToSecretService("MuOnline", username, password);
        }
    }
}
```

**Ventajas de DPAPI:**
- La clave de encriptación está ligada al usuario del SO
- No hay clave hardcoded en el código
- Protegido por el login del sistema operativo

### Opción 2: Si debes usar AES, hacerlo correctamente

```csharp
public static void SaveCredentials(string username, string password)
{
    var data = Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { username, password }));

    using var aes = Aes.Create();
    aes.GenerateIV();  // IV ALEATORIO cada vez

    // Derivar clave de algo único del usuario, no hardcoded
    // Por ejemplo, un hash del hardware ID + username
    var machineKey = GetMachineSpecificKey();
    using var derive = new Rfc2898DeriveBytes(
        machineKey,
        aes.IV,        // Usar el IV como salt
        600_000,       // 600K iteraciones (OWASP 2023)
        HashAlgorithmName.SHA256);
    aes.Key = derive.GetBytes(32);

    using var ms = new MemoryStream();
    ms.Write(aes.IV);  // Prepend IV (no es secreto)
    using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
    cs.Write(data);
    cs.FlushFinalBlock();

    File.WriteAllBytes(CredentialsPath, ms.ToArray());
}

private static byte[] GetMachineSpecificKey()
{
    // Combinar identificadores únicos de la máquina
    var machineId = Environment.MachineName + Environment.UserName;
    return SHA256.HashData(Encoding.UTF8.GetBytes(machineId));
}
```

### Opción 3 (Mínimo viable): No guardar el password

La opción más segura es simplemente no almacenar el password:

```csharp
// Solo guardar el username, pedir password cada vez
public static void SaveUsername(string username)
{
    File.WriteAllText(CredentialsPath, username);
}

// O usar un token de sesión temporal en lugar del password
public static void SaveSession(string username, string sessionToken, DateTime expiry)
{
    // El session token expira, así que si se roba hay ventana limitada
}
```

---

## Concepto clave: No inventes tu propia criptografía

**Regla de oro:** Usa mecanismos de seguridad del SO o librerías auditadas.

| Lo que haces | Lo que deberías usar |
|---|---|
| AES con clave hardcoded | DPAPI (Windows) / Keychain (macOS) |
| MD5/SHA1 para passwords | BCrypt / Argon2id |
| XOR para "encriptar" | TLS / AES-GCM |
| JWT sin verificar firma | Librería de validación JWT |
| Tu propio protocolo crypto | TLS 1.3 |

---

## Referencias

- [OWASP: Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
- [CWE-257: Recoverable Password Storage](https://cwe.mitre.org/data/definitions/257.html)
- [Microsoft DPAPI](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection)
- [OWASP: PBKDF2 Iterations (2023)](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html#pbkdf2)
