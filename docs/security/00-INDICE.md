# Auditoría de Seguridad - OpenMU + Cliente MuOnline

**Fecha:** 2026-03-07
**Alcance:** Servidor OpenMU (`/OpenMU`) + Cliente (`/muonline`)

## Índice de Documentos

| # | Documento | Severidad | Tema |
|---|-----------|-----------|------|
| 1 | [01-OAUTH-LOGIN.md](./01-OAUTH-LOGIN.md) | CRÍTICA | Bypass de autenticación OAuth (Apple JWT, security code) |
| 2 | [02-API-SIN-AUTH.md](./02-API-SIN-AUTH.md) | CRÍTICA | Endpoints API admin sin autenticación |
| 3 | [03-SECRETOS-HARDCODED.md](./03-SECRETOS-HARDCODED.md) | ALTA | Credenciales de BD y claves en código fuente |
| 4 | [04-CRIPTOGRAFIA-DEBIL.md](./04-CRIPTOGRAFIA-DEBIL.md) | ALTA | XOR-3, SimpleModulus, claves estáticas |
| 5 | [05-CREDENCIALES-CLIENTE.md](./05-CREDENCIALES-CLIENTE.md) | ALTA | Almacenamiento inseguro de credenciales en cliente |
| 6 | [06-INYECCION-PAQUETES.md](./06-INYECCION-PAQUETES.md) | ALTA | Manipulación de paquetes de red |
| 7 | [07-VALIDACION-INPUT.md](./07-VALIDACION-INPUT.md) | MEDIA | Falta de validación/sanitización de entrada |
| 8 | [08-INFORMACION-EXPUESTA.md](./08-INFORMACION-EXPUESTA.md) | MEDIA | Información sensible en logs y configs |
| 9 | [09-RESUMEN-EJECUTIVO.md](./09-RESUMEN-EJECUTIVO.md) | - | Resumen con matriz de riesgo y prioridades |
| 10 | [10-BUGS-LOGICA-NEGOCIO.md](./10-BUGS-LOGICA-NEGOCIO.md) | CRÍTICA | Dupe items, integer overflow en trades, race conditions |
| 11 | [11-BUGS-COMBAT-STATS-MOVEMENT.md](./11-BUGS-COMBAT-STATS-MOVEMENT.md) | CRÍTICA | Party crash, Dapr sin auth, skill exploits, NPC remoto |
| 12 | [12-BUGS-RED-PROTOCOLO.md](./12-BUGS-RED-PROTOCOLO.md) | CRÍTICA | Servidor falso MITM, DoS memoria, replay attacks |
| 13 | [13-BUGS-CRAFTING-ITEMS-VAULT.md](./13-BUGS-CRAFTING-ITEMS-VAULT.md) | CRÍTICA | Byte underflow level 254, craft exploits, stack overflow |
| 14 | [14-BUGS-GUILD-EVENTS-PERSISTENCE.md](./14-BUGS-GUILD-EVENTS-PERSISTENCE.md) | CRÍTICA | Rollback dupe, guild sin requisitos, security code leaks |
| 15 | [15-BUGS-CLIENTE-PACKET-FORGE.md](./15-BUGS-CLIENTE-PACKET-FORGE.md) | CRÍTICA | Item forge, hit a distancia, teleport hack, speed hack |
| 16 | [16-BUGS-SOCIAL-MESSENGER-CONSUME.md](./16-BUGS-SOCIAL-MESSENGER-CONSUME.md) | ALTA | Off-by-one cartas, friend exploit, town portal escape |
| 17 | [17-BUGS-DAMAGE-DEATH-EXPERIENCE.md](./17-BUGS-DAMAGE-DEATH-EXPERIENCE.md) | CRÍTICA | Invulnerabilidad, mana negativa, curar enemigos, exp recursion |
| 18 | [18-BUGS-CONCURRENCIA-RACE-CONDITIONS.md](./18-BUGS-CONCURRENCIA-RACE-CONDITIONS.md) | CRÍTICA | Dupe dinero sistémico, party crash, 14 async void, vault race |
| 19 | [19-BUGS-QUEST-PET-SOCKET-CLASS.md](./19-BUGS-QUEST-PET-SOCKET-CLASS.md) | CRÍTICA | Pet durability bypass, evolución sin validar, mini-game gratis |
| 20 | [20-BUGS-ADMIN-DOCKER-DEPLOYMENT.md](./20-BUGS-ADMIN-DOCKER-DEPLOYMENT.md) | CRÍTICA | Admin sin auth, Docker creds hardcoded, plugins sin firma, nginx headers |
| 21 | [21-BUGS-DUEL-MINIGAMES-SERIAL-BAN.md](./21-BUGS-DUEL-MINIGAMES-SERIAL-BAN.md) | CRÍTICA | Duel crash off-by-one, Blood Castle exploit, ban inefectivo, sin item serials |

## Cobertura del proyecto

| Sistema | Documentos | Estado |
|---------|-----------|--------|
| Autenticación/OAuth | 01, 02, 03, 05 | ✅ Completo |
| Criptografía/Protocolo | 04, 06, 12 | ✅ Completo |
| Validación/Info Exposure | 07, 08 | ✅ Completo |
| Lógica de negocio (trade/shop) | 10 | ✅ Completo |
| Combate/Stats/Movement | 11, 17 | ✅ Completo |
| Crafting/Items/Vault | 13 | ✅ Completo |
| Guild/Eventos/Persistencia | 14 | ✅ Completo |
| Cliente/Packet Forge | 15 | ✅ Completo |
| Social/Messenger/Consumibles | 16 | ✅ Completo |
| Concurrencia/Race Conditions | 18 | ✅ Completo |
| Quests/Pets/Sockets/Clase | 19 | ✅ Completo |
| Admin Panel/Docker/Deploy | 20 | ✅ Completo |
| Duelos/Mini-games/Ban/Serials | 21 | ✅ Completo |

## Cómo leer estos documentos

Cada documento sigue la estructura:
1. **Qué es** - Explicación del concepto de seguridad
2. **Dónde está el problema** - Archivo y línea exacta
3. **Por qué es un problema** - Escenario de ataque real
4. **Cómo se soluciona** - Código o patrón correcto
5. **Referencias** - OWASP, CWE, recursos de aprendizaje
