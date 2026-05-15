# GV RH - Runbook Operativo Rocky

Este documento describe el procedimiento operativo oficial para administrar GV RH en Rocky Linux con Docker Compose y GitHub Actions.

## Estado actual esperado

El servidor Rocky debe operar con:

- API en ASP.NET Core / .NET 8.
- Web React/Vite servida por Nginx.
- PostgreSQL 17.
- Docker Compose en `/opt/gv-rh-demo`.
- API en ambiente `Production`.
- Swagger apagado en producción.
- Migraciones EF Core controladas manualmente.
- Backups manuales desde GitHub Actions.
- Backups automáticos diarios desde GitHub Actions.
- Healthchecks activos para API, Web y DB.
- Alertas básicas de health cada 15 minutos.
- API directa por puerto `8080` expuesta solo en `127.0.0.1`.
- Web pública por Nginx en puerto `80`.

---

## Servicios Docker

Ruta principal en Rocky:

```bash
cd /opt/gv-rh-demo
```

Ver estado:

```bash
docker compose ps
```

Estado sano esperado:

```txt
gv-rh-api   Up ... (healthy)   127.0.0.1:8080->8080/tcp
gv-rh-db    Up ... (healthy)   5432/tcp
gv-rh-web   Up ... (healthy)   0.0.0.0:80->80/tcp
```

La API no debe estar expuesta a toda la LAN por `8080`.

Desde Rocky debe funcionar:

```bash
curl -i http://localhost:8080/health
```

Desde otro equipo de la LAN debe fallar:

```powershell
curl.exe -i http://192.168.0.3:8080/health
```

La API debe consumirse por Nginx:

```powershell
curl.exe -i http://192.168.0.3/api/health
```

---

## Variables importantes

Archivo:

```bash
/opt/gv-rh-demo/.env
```

Variables clave esperadas:

```env
ASPNETCORE_ENVIRONMENT=Production
Database__ApplyMigrationsOnStartup=false
Swagger__Enabled=false
Security__UseHttpsRedirection=false
DataProtection__ApplicationName=Gv.Rh.Api
DataProtection__KeysPath=/app/dataprotection
```

Variables sensibles que deben vivir en `.env`, no duras en `docker-compose.yml`:

```env
ConnectionStrings__RhDb=...
POSTGRES_PASSWORD=...
Jwt__Key=...
Jwt__Issuer=...
Jwt__Audience=...
MicrosoftGraphMail__TenantId=...
MicrosoftGraphMail__ClientId=...
MicrosoftGraphMail__ClientSecret=...
MicrosoftGraphMail__SenderUserId=...
```

No pegar el contenido completo de `.env` en chats, correos o tickets porque contiene secretos.

Validar variables sin mostrar secretos:

```bash
cd /opt/gv-rh-demo

grep -nE "ConnectionStrings__RhDb|POSTGRES_PASSWORD|Jwt__Key|MicrosoftGraphMail__ClientSecret" .env \
| sed -E 's/Password=[^;]*/Password=***MASKED***/; s/(POSTGRES_PASSWORD=).*/\1***MASKED***/; s/(Jwt__Key=).*/\1***MASKED***/; s/(MicrosoftGraphMail__ClientSecret=).*/\1***MASKED***/'
```

---

## Health endpoints

API directa por Kestrel, solo desde Rocky:

```bash
curl -i http://localhost:8080/health
curl -i http://localhost:8080/api/health
```

API pasando por Nginx:

```bash
curl -i http://localhost/api/health
```

Respuesta esperada:

```txt
HTTP/1.1 200 OK
```

JSON esperado:

```json
{
  "status": "ok",
  "app": "Gv.Rh.Api",
  "environment": "Production",
  "database": {
    "status": "ok",
    "provider": "PostgreSQL"
  }
}
```

---

## Swagger

En producción debe estar apagado:

```env
Swagger__Enabled=false
```

Validación:

```bash
curl -I http://localhost:8080/swagger/index.html
```

Respuesta esperada:

```txt
HTTP/1.1 404 Not Found
```

La raíz de la API debe responder JSON simple:

```bash
curl -i http://localhost:8080/
```

Respuesta esperada:

```json
{
  "status": "ok",
  "app": "Gv.Rh.Api",
  "environment": "Production"
}
```

---

## Flujo normal de deploy API

Usar cuando hay cambios normales de código sin migraciones.

En GitHub:

```txt
Gv.Rh.Api → Actions → Deploy API to Rocky → Run workflow
```

El workflow debe:

```txt
1. Actualizar repo API en Rocky.
2. Reconstruir imagen API.
3. Recrear contenedor API.
4. Ejecutar smoke test post-deploy.
5. Validar API/Web/DB healthy.
6. Validar /api/health.
7. Validar Production.
8. Validar Swagger apagado.
9. Validar migraciones automáticas apagadas.
```

Después validar en Rocky:

```bash
cd /opt/gv-rh-demo
docker compose ps
docker compose logs --tail=120 api
curl -i http://localhost/api/health
```

Validar que aparezca:

```txt
Application started.
Hosting environment: Production
Migraciones EF Core omitidas al iniciar. Database:ApplyMigrationsOnStartup=false.
```

---

## Flujo normal de deploy Web

Usar cuando hay cambios de frontend.

En GitHub:

```txt
gv-rh-web → Actions → Deploy Web to Rocky → Run workflow
```

El workflow debe:

```txt
1. Actualizar repo Web en Rocky.
2. Reconstruir imagen Web.
3. Recrear contenedor Web.
4. Ejecutar smoke test post-deploy.
5. Validar Web/API/DB healthy.
6. Validar http://localhost/.
7. Validar /api/health.
8. Validar Production.
```

Después validar en Rocky:

```bash
cd /opt/gv-rh-demo
docker compose ps
curl -i http://localhost/
curl -i http://localhost/api/health
```

---

## Flujo de deploy API con migraciones EF Core

Usar cuando el backend incluye una migración nueva de EF Core.

Orden obligatorio:

```txt
1. Backup PostgreSQL Rocky
2. Apply EF Core Migrations Rocky
3. Deploy API to Rocky
```

### 1. Backup PostgreSQL Rocky

En GitHub:

```txt
Gv.Rh.Api → Actions → Backup PostgreSQL Rocky → Run workflow
```

Motivo sugerido:

```txt
Backup antes de migración EF Core
```

Validar en Rocky:

```bash
cd /opt/gv-rh-demo

LATEST_BACKUP="$(ls -t backups/gv_rh_manual_*.backup | head -n 1)"
echo "$LATEST_BACKUP"
ls -lh "$LATEST_BACKUP"
docker compose exec -T db pg_restore -l < "$LATEST_BACKUP" | head -n 25
```

### 2. Apply EF Core Migrations Rocky

En GitHub:

```txt
Gv.Rh.Api → Actions → Apply EF Core Migrations Rocky → Run workflow
```

El workflow debe validar backup reciente y ejecutar un contenedor temporal de API con:

```env
Database__ApplyMigrationsOnStartup=true
Database__ExitAfterStartupMaintenance=true
```

El contenedor temporal debe terminar sin levantar servidor HTTP.

Mensaje esperado:

```txt
Mantenimiento de arranque completado. Database:ExitAfterStartupMaintenance=true.
```

Validar historial de migraciones:

```bash
cd /opt/gv-rh-demo

docker compose exec -T db psql -U postgres -d gv_rh -c 'SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 10;'
```

### 3. Deploy API to Rocky

En GitHub:

```txt
Gv.Rh.Api → Actions → Deploy API to Rocky → Run workflow
```

Después validar:

```bash
cd /opt/gv-rh-demo
docker compose ps
curl -i http://localhost/api/health
```

---

## Backup manual PostgreSQL

Los backups manuales se guardan en:

```bash
/opt/gv-rh-demo/backups
```

Patrón:

```txt
gv_rh_manual_YYYYMMDD_HHMMSS.backup
```

Crear backup manual:

```txt
Gv.Rh.Api → Actions → Backup PostgreSQL Rocky → Run workflow
```

Listar backups manuales:

```bash
cd /opt/gv-rh-demo
ls -lh backups/gv_rh_manual_*.backup 2>/dev/null || true
```

Validar último backup:

```bash
cd /opt/gv-rh-demo

LATEST_BACKUP="$(ls -t backups/gv_rh_manual_*.backup | head -n 1)"
echo "$LATEST_BACKUP"
ls -lh "$LATEST_BACKUP"
docker compose exec -T db pg_restore -l < "$LATEST_BACKUP" | head -n 25
```

---

## Backup automático PostgreSQL

Workflow:

```txt
Gv.Rh.Api → Actions → Backup PostgreSQL Rocky
```

Debe correr:

```txt
Diario a las 23:17 America/Mexico_City
```

Patrón de backup automático:

```txt
gv_rh_auto_YYYYMMDD_HHMMSS.backup
```

Validar backups automáticos:

```bash
cd /opt/gv-rh-demo

ls -lh backups/gv_rh_auto_*.backup 2>/dev/null || true
du -sh backups
df -h .
```

Validar último backup automático:

```bash
cd /opt/gv-rh-demo

LATEST_AUTO="$(ls -t backups/gv_rh_auto_*.backup | head -n 1)"
echo "$LATEST_AUTO"
ls -lh "$LATEST_AUTO"
docker compose exec -T db pg_restore -l < "$LATEST_AUTO" | head -n 25
```

El workflow automático debe conservar los últimos 30 backups automáticos.

---

## Retención de backups manuales

Workflow:

```txt
Gv.Rh.Api → Actions → Prune PostgreSQL Backups Rocky → Run workflow
```

Configuración recomendada:

```txt
keep_latest: 30
dry_run: true
```

Cuando ya se validó y haya más de 30 backups manuales:

```txt
keep_latest: 30
dry_run: false
```

Este workflow solo debe tocar:

```txt
gv_rh_manual_*.backup
```

No debe tocar:

```txt
gv_rh_auto_*.backup
gv_rh_before_*.backup
gv_rh_pre_*.backup
docker-compose.*
env.*
deploy-*
```

---

## DataProtection

Las llaves deben persistir en Rocky:

```bash
/opt/gv-rh-demo/data/dataprotection
```

Validar:

```bash
cd /opt/gv-rh-demo
ls -lah data/dataprotection
```

Debe existir un archivo parecido a:

```txt
key-xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.xml
```

---

## Healthcheck Docker API

Validar API:

```bash
cd /opt/gv-rh-demo
docker inspect --format='{{json .State.Health.Status}}' gv-rh-api
```

Resultado esperado:

```txt
"healthy"
```

Ver logs del healthcheck:

```bash
docker inspect --format='{{range .State.Health.Log}}{{.End}} {{.ExitCode}} {{.Output}}{{println}}{{end}}' gv-rh-api
```

Los últimos checks deben tener:

```txt
ExitCode 0
```

---

## Healthcheck Docker Web

Validar Web/Nginx:

```bash
cd /opt/gv-rh-demo
docker inspect --format='{{json .State.Health.Status}}' gv-rh-web
```

Resultado esperado:

```txt
"healthy"
```

Ver logs del healthcheck:

```bash
docker inspect --format='{{range .State.Health.Log}}{{.End}} {{.ExitCode}} {{.Output}}{{println}}{{end}}' gv-rh-web
```

---

## Smoke Test Rocky

Workflow manual:

```txt
Gv.Rh.Api → Actions → Smoke Test Rocky → Run workflow
```

Debe validar:

```txt
API healthy
Web healthy
DB healthy
/api/health responde OK
Environment Production
Swagger apagado
Migraciones automáticas apagadas
```

Debe terminar en verde.

---

## Smoke Test Web Rocky

Workflow manual:

```txt
gv-rh-web → Actions → Smoke Test Web Rocky → Run workflow
```

Debe validar:

```txt
Web healthy
API healthy
DB healthy
http://localhost/ responde 200
/api/health responde OK
Environment Production
Swagger apagado
```

Debe terminar en verde.

---

## Health Alert Rocky

Workflow:

```txt
Gv.Rh.Api → Actions → Health Alert Rocky
```

Corre:

```txt
Cada 15 minutos
```

También puede ejecutarse manualmente.

Modo sano:

```txt
force_fail: false
```

Debe quedar verde.

Modo prueba de alerta:

```txt
force_fail: true
```

Debe quedar rojo a propósito y mandar correo con asunto:

```txt
ALERTA GV RH - Health check falló en Rocky
```

El workflow ejecuta este script local en Rocky:

```bash
/usr/local/bin/gv-rh-health-alert.sh
```

Validar permisos:

```bash
ls -lh /usr/local/bin/gv-rh-health-alert.sh
```

Debe tener ejecución para el runner, por ejemplo:

```txt
-rwxr-xr-x
```

Probar manualmente en Rocky:

```bash
/usr/local/bin/gv-rh-health-alert.sh false
```

Debe terminar con:

```txt
HEALTH_OK: GV RH está sano.
```

Probar alerta forzada:

```bash
/usr/local/bin/gv-rh-health-alert.sh true
```

Debe mandar correo y terminar con error a propósito.

---

## Microsoft Graph Mail

Variables esperadas en `.env`:

```env
MicrosoftGraphMail__TenantId=...
MicrosoftGraphMail__ClientId=...
MicrosoftGraphMail__ClientSecret=...
MicrosoftGraphMail__SenderUserId=...
```

Validar variables sin mostrar secreto:

```bash
cd /opt/gv-rh-demo

docker compose exec api sh -lc 'printenv | grep "^MicrosoftGraphMail__" | sed -E "s/(MicrosoftGraphMail__ClientSecret=).*/\1***MASKED***/; s/(MicrosoftGraphMail__TenantId=).*/\1***MASKED***/; s/(MicrosoftGraphMail__ClientId=).*/\1***MASKED***/; s/(MicrosoftGraphMail__SenderUserId=).*/\1***MASKED***/"'
```

El secreto actual validado debe ser el nuevo creado en Microsoft Entra:

```txt
GV RH Mail Secret - Rocky - 2026-05-14
```

No debe quedar activo el secreto viejo.

---

## Seguridad de red

Estado esperado:

```txt
Nginx/Web: puerto 80 expuesto a LAN
API/Kestrel: puerto 8080 solo en 127.0.0.1
PostgreSQL: sin puerto publicado a LAN
```

Validar en Rocky:

```bash
cd /opt/gv-rh-demo
docker compose ps
```

API debe aparecer así:

```txt
127.0.0.1:8080->8080/tcp
```

Desde Windows:

```powershell
curl.exe -i http://192.168.0.3:8080/health
```

Debe fallar.

Desde Windows:

```powershell
curl.exe -i http://192.168.0.3/api/health
```

Debe responder:

```txt
HTTP/1.1 200 OK
```

---

## Logs frecuentes

API:

```bash
cd /opt/gv-rh-demo
docker compose logs --tail=120 api
```

Web:

```bash
docker compose logs --tail=120 web
```

DB:

```bash
docker compose logs --tail=120 db
```

Seguir logs en vivo:

```bash
docker compose logs -f api
```

Filtrar errores API:

```bash
docker compose logs --tail=120 api | grep -Ei "fail|error|exception|Npgsql|authentication|password|Application started|Hosting environment" || true
```

---

## Reinicios controlados

Reiniciar solo API:

```bash
cd /opt/gv-rh-demo
docker compose restart api
```

Recrear solo API:

```bash
docker compose up -d --no-deps --force-recreate api
```

Recrear solo Web:

```bash
docker compose up -d --no-deps --force-recreate web
```

Ver estado después:

```bash
docker compose ps
```

---

## Validación rápida post-deploy

Después de cualquier deploy:

```bash
cd /opt/gv-rh-demo

docker compose ps
curl -i http://localhost/api/health
docker compose logs --tail=80 api
```

Checklist:

```txt
API healthy
DB healthy
Web healthy
/api/health responde 200 OK
Application started
Hosting environment: Production
No hay errores críticos
```

---

## Restore probado y controlado

La restauración de backups debe tratarse como operación delicada. Antes de tocar producción, se debe probar el backup en una base temporal.

Base de producción:

```txt
gv_rh
```

Nunca restaurar directamente sobre `gv_rh` sin autorización explícita y sin validar primero el backup en una base temporal.

---

### Restore test no destructivo

Este procedimiento valida que el backup realmente puede restaurarse sin tocar producción.

Entrar al servidor:

```bash
cd /opt/gv-rh-demo
```

Validar estado actual:

```bash
docker compose ps
curl -i http://localhost/api/health
```

Estado sano esperado:

```txt
gv-rh-api   Up ... (healthy)
gv-rh-db    Up ... (healthy)
gv-rh-web   Up ... (healthy)
HTTP/1.1 200 OK
```

Seleccionar el backup más reciente, manual o automático:

```bash
LATEST_BACKUP="$(ls -t backups/gv_rh_manual_*.backup backups/gv_rh_auto_*.backup 2>/dev/null | head -n 1)"

echo "$LATEST_BACKUP"
ls -lh "$LATEST_BACKUP"
```

Validar catálogo del backup:

```bash
docker compose exec -T db pg_restore -l < "$LATEST_BACKUP" | head -n 25
```

Debe mostrar algo parecido a:

```txt
Format: CUSTOM
Compression: gzip
TOC Entries: ...
TABLE public __EFMigrationsHistory
TABLE public ...
```

Crear nombre de base temporal:

```bash
TEST_DB="gv_rh_restore_test_$(date +%Y%m%d_%H%M%S)"
echo "$TEST_DB"
```

Crear base temporal:

```bash
docker compose exec -T db createdb -U postgres "$TEST_DB"
```

Validar que existe:

```bash
docker compose exec -T db psql -U postgres -d postgres -c "SELECT datname FROM pg_database WHERE datname = '$TEST_DB';"
```

Restaurar backup en la base temporal:

```bash
docker compose exec -T db pg_restore \
  -U postgres \
  -d "$TEST_DB" \
  --no-owner \
  --no-privileges \
  --exit-on-error \
  < "$LATEST_BACKUP"
```

Si termina sin errores, el backup restauró correctamente en la base temporal.

Validar migraciones:

```bash
docker compose exec -T db psql -U postgres -d "$TEST_DB" -c 'SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" DESC LIMIT 10;'
```

Validar número de tablas públicas:

```bash
docker compose exec -T db psql -U postgres -d "$TEST_DB" -c "SELECT COUNT(*) AS tablas_publicas FROM information_schema.tables WHERE table_schema = 'public';"
```

Listar tablas restauradas:

```bash
docker compose exec -T db psql -U postgres -d "$TEST_DB" -c '\dt public.*'
```

Validar conteos básicos:

```bash
docker compose exec -T db psql -U postgres -d "$TEST_DB" -c '
SELECT
  (SELECT COUNT(*) FROM users) AS users,
  (SELECT COUNT(*) FROM departamentos) AS departamentos,
  (SELECT COUNT(*) FROM empleados) AS empleados,
  (SELECT COUNT(*) FROM "__EFMigrationsHistory") AS migrations;
'
```

Confirmar que producción sigue sana:

```bash
curl -i http://localhost/api/health
docker compose ps
```

Borrar base temporal:

```bash
docker compose exec -T db dropdb -U postgres --if-exists "$TEST_DB"
```

Confirmar que ya no existe:

```bash
docker compose exec -T db psql -U postgres -d postgres -c "SELECT datname FROM pg_database WHERE datname = '$TEST_DB';"
```

Debe regresar:

```txt
(0 rows)
```

Validación final:

```bash
docker compose ps
curl -i http://localhost/api/health
df -h .
du -sh backups
```

Resultado esperado:

```txt
Backup válido
Restore temporal exitoso
Producción intacta
Base temporal eliminada
Servicios healthy
```

---

### Prueba real ya validada

Última prueba controlada realizada:

```txt
Backup usado: backups/gv_rh_manual_20260514_182219.backup
Tamaño: 143K
Formato: CUSTOM gzip
PostgreSQL: 17.9
TOC Entries: 248
Base temporal: gv_rh_restore_test_20260514_185920
Migraciones EF Core: 10
Tablas públicas: 21
users: 7
departamentos: 25
empleados: 118
Producción: intacta
Base temporal: eliminada
Disco root: 51% usado, 10G libres
Backups: 2.6M
```

Estado final de la prueba:

```txt
gv-rh-api   Up ... (healthy)
gv-rh-db    Up ... (healthy)
gv-rh-web   Up ... (healthy)
/api/health HTTP/1.1 200 OK
```

---

### Restore destructivo de producción

No ejecutar salvo emergencia real y con autorización.

Este procedimiento reemplaza los datos actuales de producción.

Orden general:

```txt
1. Confirmar incidente.
2. Confirmar backup a restaurar.
3. Validar backup con pg_restore -l.
4. Probar restore en base temporal.
5. Detener API y Web.
6. Restaurar sobre gv_rh.
7. Levantar servicios.
8. Validar health.
9. Validar datos críticos.
```

Seleccionar backup:

```bash
cd /opt/gv-rh-demo

BACKUP_FILE="backups/NOMBRE_DEL_BACKUP.backup"
echo "$BACKUP_FILE"
ls -lh "$BACKUP_FILE"
```

Validar catálogo:

```bash
docker compose exec -T db pg_restore -l < "$BACKUP_FILE" | head -n 25
```

Detener API y Web:

```bash
docker compose stop api web
```

Recrear base de producción:

```bash
docker compose exec -T db dropdb -U postgres gv_rh
docker compose exec -T db createdb -U postgres gv_rh
```

Restaurar backup:

```bash
docker compose exec -T db pg_restore \
  -U postgres \
  -d gv_rh \
  --no-owner \
  --no-privileges \
  --exit-on-error \
  < "$BACKUP_FILE"
```

Levantar servicios:

```bash
docker compose up -d api web
```

Validar:

```bash
docker compose ps
curl -i http://localhost/api/health
```

Validar conteos básicos:

```bash
docker compose exec -T db psql -U postgres -d gv_rh -c '
SELECT
  (SELECT COUNT(*) FROM users) AS users,
  (SELECT COUNT(*) FROM departamentos) AS departamentos,
  (SELECT COUNT(*) FROM empleados) AS empleados,
  (SELECT COUNT(*) FROM "__EFMigrationsHistory") AS migrations;
'
```

Checklist final:

```txt
API healthy
DB healthy
Web healthy
/api/health 200 OK
environment Production
database.status ok
Datos básicos presentes
```

---

## Rotación de secretos

Secretos rotados en Fase 10:

```txt
Jwt__Key
MicrosoftGraphMail__ClientSecret
POSTGRES_PASSWORD / ConnectionStrings__RhDb
```

Validaciones esperadas:

```bash
cd /opt/gv-rh-demo

docker compose ps
curl -i http://localhost/api/health
docker compose exec -T db psql -U postgres -d gv_rh -c 'SELECT now();'
```

No imprimir secretos en consola.

No pegar:

```txt
.env completo
docker compose config completo
ConnectionStrings__RhDb sin máscara
Jwt__Key
MicrosoftGraphMail__ClientSecret
POSTGRES_PASSWORD
```

---

## Comandos de emergencia

Ver disco:

```bash
df -h .
du -sh backups
```

Ver contenedores:

```bash
docker compose ps
docker ps -a
```

Ver consumo Docker:

```bash
docker system df
```

Ver últimos backups:

```bash
ls -lh backups | tail -n 20
```

Ver puertos publicados:

```bash
docker compose ps
ss -tulpn | grep -E ':80|:8080|:5432' || true
```

Validar API por Nginx:

```bash
curl -i http://localhost/api/health
```

Validar API directa local:

```bash
curl -i http://localhost:8080/health
```

---

## Reglas de oro

1. No pegar `.env` completo en chats, correos o tickets.
2. No pegar `docker compose config` completo porque resuelve secretos.
3. Antes de migraciones: backup primero.
4. No aplicar migraciones desde el arranque normal de API.
5. No restaurar base sin validar backup.
6. Probar restore primero en base temporal.
7. No correr `dry_run=false` en limpieza de backups sin revisar primero.
8. No cambiar HTTPS/SSL sin revisar Nginx y certificados.
9. No exponer Swagger en Production.
10. No exponer API directa a la LAN por puerto `8080`.
11. Si algo falla, revisar logs antes de reiniciar todo.
12. Si se expone un secreto, se rota.