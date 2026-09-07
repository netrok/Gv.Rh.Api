# Mejoras aplicadas — Gv.Rh.Api

Registro de las mejoras de seguridad, mantenimiento y calidad de código aplicadas al proyecto, derivadas de un análisis inicial del repositorio.

## Resumen

| # | Mejora | Tipo | PR |
|---|--------|------|----|
| 1 | Requerir autenticación en el endpoint de foto de empleado | Seguridad | [#1](../../pull/1) |
| 2 | Rate limiting en login (5 intentos por minuto) | Seguridad | [#2](../../pull/2) |
| 3 | Actualización de paquetes NuGet | Mantenimiento | [#3](../../pull/3) |
| 4 | Usuario no-root en el Dockerfile | Seguridad | [#4](../../pull/4) |
| 5 | Manejador global de excepciones | Calidad de código | [#5](../../pull/5) |

---

## 1. Fix: autenticación requerida en foto de empleado

**Problema:** `GET /api/empleados/{id}/foto` tenía `[AllowAnonymous]` mientras el resto del controller exigía `[Authorize]`, permitiendo que cualquiera sin sesión descargara la foto de cualquier empleado iterando IDs.

**Cambio:** se elimina el atributo `[AllowAnonymous]`. El endpoint queda protegido por el `[Authorize]` que ya tenía el controller a nivel de clase.

**Archivo:** `Gv.Rh.Api/Controllers/EmpleadosController.cs`

---

## 2. Rate limiting en login

**Problema:** el endpoint `POST /api/auth/login` no tenía ningún límite de intentos, dejando la puerta abierta a ataques de fuerza bruta sobre credenciales (aunque las contraseñas ya estaban bien hasheadas con BCrypt).

**Cambio:** se agrega rate limiting nativo de .NET 8 (`Microsoft.AspNetCore.RateLimiting`), configurado como ventana fija: máximo 5 intentos por minuto por política, respondiendo `429 Too Many Requests` al excederse.

**Archivos:** `Gv.Rh.Api/Program.cs`, `Gv.Rh.Api/Controllers/AuthController.cs`

---

## 3. Actualización de paquetes NuGet

**Problema:** varios paquetes fijados en versiones antiguas, sin actualizaciones de seguridad ni bugfixes.

**Cambio:** se actualizan a su última versión estable compatible los paquetes que no dependen de la versión mayor de .NET:

- `ClosedXML` 0.105.0 → 0.105.1
- `BCrypt.Net-Next` 4.1.0 → 4.2.0
- `ExcelDataReader` 3.8.0 → 3.9.0
- `Microsoft.Identity.Client` 4.83.3 → 4.88.0
- `QuestPDF` 2026.2.3 → 2026.8.0
- `System.IdentityModel.Tokens.Jwt` 8.0.0 → 8.22.0
- `Microsoft.Extensions.Http` 10.0.6 → 10.0.11
- `System.Text.Encoding.CodePages` 10.0.7 → 10.0.11

**Pendiente (decisión aparte, no incluida en esta ronda):** `Microsoft.EntityFrameworkCore*`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.Extensions.Hosting`, `EFCore.NamingConventions` y `Swashbuckle.AspNetCore` se dejaron en su versión actual porque su "más reciente" en NuGet ya corresponde a .NET 10. Actualizarlos implicaría migrar todo el proyecto de .NET 8 a .NET 10 — una decisión de mayor alcance que merece su propia planeación y pruebas, no un simple bump de paquete.

**Archivos:** `Gv.Rh.Api/Gv.Rh.Api.csproj`, `Gv.Rh.Infrastructure/Gv.Rh.Infrastructure.csproj`

---

## 4. Usuario no-root en el Dockerfile

**Problema:** el contenedor corría el proceso como `root` por defecto — cualquier compromiso de la aplicación heredaría privilegios innecesarios dentro del contenedor.

**Cambio:** se agrega un usuario sin privilegios (`gvrh`, UID/GID fijo en `1000`) en la etapa final del `Dockerfile`, y se cambia el dueño de `/app` antes de aplicar `USER gvrh`.

**⚠️ Importante para el despliegue en Rocky:** el volumen de host `/opt/gv-rh-demo/data/dataprotection` (ver `RUNBOOK.md`) pertenece actualmente a `root`. Antes de desplegar esta imagen en staging/producción, correr en el servidor:

\`\`\`bash
sudo chown -R 1000:1000 /opt/gv-rh-demo/data/dataprotection
\`\`\`

De lo contrario, la aplicación no podrá escribir las llaves de `DataProtection` tras el despliegue.

**Archivo:** `Dockerfile`

---

## 5. Manejador global de excepciones

**Contexto:** el análisis inicial detectó 18 bloques `catch (Exception)` en el proyecto. Al revisarlos uno por uno, la mayoría resultaron ser patrones correctos y deliberados (background workers, notificaciones best-effort, health checks) que **no** requerían cambios. La única excepción real: `VacacionesController.cs` repetía el mismo bloque `try/catch` idéntico en 9 métodos distintos.

**Cambio:** se agrega `GlobalExceptionHandler` (usando `IExceptionHandler`, nativo de .NET 8) que traduce excepciones comunes a respuestas HTTP consistentes en formato `application/problem+json`:

- `KeyNotFoundException` → `404 Not Found`
- `InvalidOperationException` → `400 Bad Request`
- Cualquier otra excepción → `500 Internal Server Error`

Se eliminan los 9 bloques `try/catch` duplicados de `VacacionesController.cs`, que ahora delega en el manejador global.

**Archivos:** `Gv.Rh.Api/Middelwares/GlobalExceptionHandler.cs` (nuevo), `Gv.Rh.Api/Program.cs`, `Gv.Rh.Api/Controllers/VacacionesController.cs`

---

## Pendientes identificados (no incluidos en esta ronda)

- **Sin proyecto de pruebas** en la solución — el gap más importante a mediano plazo, especialmente para la lógica de cálculo de vacaciones/kárdex.
- Migración mayor de EF Core / JwtBearer / Npgsql a .NET 10 (ver sección 3).
- Auditoría de lectura (quién *vio* qué, no solo quién modificó) para datos sensibles de expediente.