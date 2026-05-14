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
- Healthchecks activos para API, Web y DB.

---

## Servicios Docker

Ruta principal en Rocky:

```bash
cd /opt/gv-rh-demo