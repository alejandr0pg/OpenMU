# 09 - Resumen Ejecutivo: Auditoría de Seguridad

---

## Matriz de Riesgo

| # | Vulnerabilidad | Severidad | Esfuerzo Fix | Impacto | Probabilidad |
|---|---|---|---|---|---|
| 1 | Apple JWT sin verificar firma | **CRÍTICA** | Medio | Acceso a cualquier cuenta | Alta |
| 2 | API admin sin autenticación | **CRÍTICA** | Bajo | Control total del servidor | Alta |
| 3 | Google OAuth sin validar audience | **ALTA** | Bajo | Impersonación de usuarios | Media |
| 4 | Security code "123456" por defecto | **ALTA** | Bajo | Bypass de operaciones protegidas | Alta |
| 5 | Credenciales de BD hardcoded | **ALTA** | Bajo | Acceso total a la BD | Media |
| 6 | XOR-3 para passwords | **ALTA** | Alto | Robo de credenciales en red | Media |
| 7 | SimpleModulus con claves públicas | **ALTA** | Alto | Sniffing de todo el tráfico | Media |
| 8 | Credenciales cliente AES hardcoded | **ALTA** | Medio | Robo de passwords locales | Baja |
| 9 | Sin rate limiting en paquetes | **MEDIA** | Medio | DoS, item duplication | Media |
| 10 | Chat sin sanitización | **MEDIA** | Bajo | XSS en admin panel | Baja |
| 11 | Info sensible en logs/errores | **MEDIA** | Bajo | Reconocimiento | Baja |
| 12 | Clave Apple .p8 en repo | **MEDIA** | Bajo | Impersonar app | Baja |

---

## Prioridad de remediación (ordenado por impacto/esfuerzo)

### Sprint 1: Fixes rápidos (1-2 días)

1. **Agregar `[Authorize]` al ServerController** (Doc 02)
   - Esfuerzo: 30 minutos
   - Impacto: Elimina acceso no autorizado a la API admin

2. **Validar audience en Google OAuth** (Doc 01)
   - Esfuerzo: 15 minutos
   - Impacto: Previene impersonación via tokens de otras apps

3. **Generar security code aleatorio en OAuth** (Doc 01)
   - Esfuerzo: 15 minutos
   - Impacto: Elimina código predecible

4. **Mover secrets.json a variables de entorno** (Doc 03)
   - Esfuerzo: 1 hora
   - Impacto: Credenciales fuera del repositorio

5. **Agregar .p8 y secrets a .gitignore** (Doc 08)
   - Esfuerzo: 5 minutos
   - Impacto: Previene futuros leaks

### Sprint 2: Fixes moderados (3-5 días)

6. **Implementar verificación JWT de Apple** (Doc 01)
   - Esfuerzo: 4 horas
   - Impacto: Elimina bypass de autenticación crítico

7. **Usar DPAPI/Keychain en cliente** (Doc 05)
   - Esfuerzo: 1 día
   - Impacto: Credenciales locales protegidas correctamente

8. **Agregar rate limiting a packet handlers** (Doc 06)
   - Esfuerzo: 1 día
   - Impacto: Previene flooding y exploits de timing

9. **Sanitización de chat y validación de nombres** (Doc 07)
   - Esfuerzo: 4 horas
   - Impacto: Previene XSS y abuso

### Sprint 3: Mejoras estructurales (1-2 semanas)

10. **Agregar TLS al transporte de red** (Doc 04)
    - Esfuerzo: 1 semana
    - Impacto: Protege todo el tráfico (passwords, datos de juego)

11. **Implementar locks por jugador en inventario/trade** (Doc 06)
    - Esfuerzo: 3 días
    - Impacto: Previene duplicación de items

12. **Hardening de logging y error handling** (Doc 08)
    - Esfuerzo: 1 día
    - Impacto: Reduce superficie de reconocimiento

---

## Fortalezas encontradas

El proyecto ya tiene buenas prácticas en varias áreas:

| Área | Estado | Detalle |
|---|---|---|
| Password hashing | Bien | BCrypt con salt automático |
| SQL injection | Protegido | Entity Framework con LINQ parametrizado |
| State machine auth | Bien | PlayerState machine previene acciones fuera de orden |
| GM commands | Bien | MinCharacterStatusRequirement verificado |
| Trade backup | Parcial | Existe backup de inventario pre-trade |

---

## Diagrama de flujo de datos y puntos de ataque

```
                        INTERNET
                           │
         ┌─────────────────┼─────────────────┐
         │                 │                  │
    [Puerto TCP]    [Puerto HTTP]      [Puerto DB]
    Game Server     Admin Panel        PostgreSQL
         │                 │                  │
    ┌────┴────┐      ┌────┴────┐       ┌────┴────┐
    │ ATAQUES │      │ ATAQUES │       │ ATAQUES │
    │         │      │         │       │         │
    │ •Sniff  │      │ •Sin    │       │ •Creds  │
    │  XOR-3  │      │  Auth   │       │  débiles│
    │ •Packet │      │ •Send   │       │ •Puerto │
    │  inject │      │  spam   │       │  expuest│
    │ •MITM   │      │ •Enum   │       │ •SQL via│
    │ •Replay │      │  cuentas│       │  psql   │
    │ •Flood  │      │ •Info   │       │         │
    └─────────┘      │  leak   │       └─────────┘
                     └─────────┘

    [Cliente Local]
         │
    ┌────┴────┐
    │ ATAQUES │
    │         │
    │ •Robo   │
    │  creds  │
    │  (.dat) │
    │ •Reverse│
    │  engine │
    │ •Memory │
    │  edit   │
    └─────────┘
```

---

## Conceptos de seguridad cubiertos

Estos documentos cubren los siguientes temas de OWASP Top 10 (2021):

- **A01: Broken Access Control** → Doc 02 (API sin auth)
- **A02: Cryptographic Failures** → Docs 03, 04, 05 (crypto débil, secretos expuestos)
- **A03: Injection** → Doc 07 (validación de input, XSS)
- **A04: Insecure Design** → Doc 06 (confianza en cliente, race conditions)
- **A07: Auth Failures** → Doc 01 (OAuth bypass)
- **A09: Logging Failures** → Doc 08 (info expuesta)

---

## Recursos de aprendizaje recomendados

### Cursos gratuitos
- [OWASP Top 10](https://owasp.org/www-project-top-ten/) - Los 10 riesgos más comunes
- [PortSwigger Web Security Academy](https://portswigger.net/web-security) - Labs prácticos
- [Hack The Box](https://www.hackthebox.com/) - CTF y máquinas vulnerables

### Herramientas para testing
- **Wireshark** - Captura y análisis de tráfico de red (verificar encriptación)
- **Burp Suite** - Proxy para interceptar peticiones HTTP (testing de API)
- **OWASP ZAP** - Scanner de vulnerabilidades web (admin panel)
- **dotnet-counters** - Monitoreo de rendimiento .NET (detectar DoS)

### Libros
- "The Web Application Hacker's Handbook" - Stuttard & Pinto
- "Secure by Design" - Johnsson, Deogun, Sawano

---

## Siguiente paso

Revisar cada documento en orden de prioridad (Sprint 1 primero) y aplicar los fixes. Cada documento tiene el código de solución listo para implementar.
