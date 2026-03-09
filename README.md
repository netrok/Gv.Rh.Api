\# GV RH API (.NET 8) — Backend



API de Recursos Humanos para GV, construida con ASP.NET Core (.NET 8), PostgreSQL y EF Core. Incluye JWT (access + refresh con rotación), roles y Swagger con autorización Bearer.



Nota de supervivencia (clásica y efectiva): los secretos NO van en el repo. En DEV van en User Secrets y en PROD en variables de entorno. Tu futuro “yo” te lo va a agradecer (y tu auditor también).



Requisitos:

\- .NET SDK 8.x

\- PostgreSQL 14+ (recomendado)

\- (Opcional) dotnet-ef para migraciones desde terminal

\- (Opcional) psql para diagnosticar



Verifica instalación:

&nbsp; dotnet --version

&nbsp; psql --version



Instala dotnet-ef (si no lo tienes):

&nbsp; dotnet tool install --global dotnet-ef



Clonar repo:

&nbsp; git clone <TU\_REPO\_URL>

&nbsp; cd Gv.Rh.Api



Configurar secretos (DEV) con User Secrets:

1\) Genera una JWT Key segura (HS256 requiere mínimo 16 bytes; recomendado 32 bytes). En PowerShell:

&nbsp; $bytes = New-Object byte\[] 32

&nbsp; \[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)

&nbsp; \[Convert]::ToBase64String($bytes)



2\) Setea User Secrets en el proyecto API (usa 127.0.0.1 para evitar broncas raras con “localhost”):

&nbsp; cd .\\Gv.Rh.Api

&nbsp; dotnet user-secrets init



&nbsp; # PostgreSQL (ajusta TU\_PASSWORD)

&nbsp; dotnet user-secrets set "ConnectionStrings:RhDb" "Host=127.0.0.1;Port=5432;Database=gv\_rh;Username=postgres;Password=TU\_PASSWORD"



&nbsp; # JWT

&nbsp; dotnet user-secrets set "Jwt:Key" "PEGA\_AQUI\_TU\_KEY\_BASE64\_LARGA"

&nbsp; dotnet user-secrets set "Jwt:Issuer" "gv-rh"

&nbsp; dotnet user-secrets set "Jwt:Audience" "gv-rh"



&nbsp; # Verifica

&nbsp; dotnet user-secrets list

&nbsp; cd ..



Migraciones (EF Core):

Aplicar migraciones (crear/actualizar esquema). Si las migraciones viven en Gv.Rh.Infrastructure:

&nbsp; dotnet ef database update `

&nbsp;   --project .\\Gv.Rh.Infrastructure\\Gv.Rh.Infrastructure.csproj `

&nbsp;   --startup-project .\\Gv.Rh.Api\\Gv.Rh.Api.csproj



Reset completo (DROP + UPDATE) — SOLO DEV (esto borra todo):

&nbsp; dotnet ef database drop --force `

&nbsp;   --project .\\Gv.Rh.Infrastructure\\Gv.Rh.Infrastructure.csproj `

&nbsp;   --startup-project .\\Gv.Rh.Api\\Gv.Rh.Api.csproj



&nbsp; dotnet ef database update `

&nbsp;   --project .\\Gv.Rh.Infrastructure\\Gv.Rh.Infrastructure.csproj `

&nbsp;   --startup-project .\\Gv.Rh.Api\\Gv.Rh.Api.csproj



Ejecutar la API:

&nbsp; dotnet run --project .\\Gv.Rh.Api\\Gv.Rh.Api.csproj



Swagger:

&nbsp; http://localhost:<PUERTO>/swagger



Swagger: Login + Bearer Token

1\) POST /api/auth/login

2\) Copia el accessToken

3\) En Swagger: Authorize → Bearer <accessToken>



Usuarios iniciales (Seeder):

En DEV, el seeder crea usuarios base (si está habilitado), por ejemplo:

\- admin@rh.local

\- rrhh@rh.local



Troubleshooting rápido:

\- “Host desconocido” (SocketException): el Host= no se puede resolver (muy típico si alguien puso “db” tipo Docker). Usa Host=127.0.0.1 y verifica que no te estén pisando config con User Secrets / env vars / launchSettings. Puedes correr sin perfiles:

&nbsp; dotnet run --project .\\Gv.Rh.Api\\Gv.Rh.Api.csproj --no-launch-profile



\- “HS256 requires key size…”: tu Jwt:Key es muy corta. Genera una de 32 bytes y guárdala en User Secrets.



\- “no existe la columna …”: la BD está atrasada vs el modelo. Corre:

&nbsp; dotnet ef database update

&nbsp; Si es DEV y no hay datos importantes: reset (drop + update).



Seguridad (obligatorio):

\- Nunca subas passwords ni Jwt:Key al repo.

\- appsettings.json debe ir sanitizado (sin secretos reales).

\- DEV: User Secrets.

\- PROD: Variables de entorno:

&nbsp; ConnectionStrings\_\_RhDb

&nbsp; Jwt\_\_Key

&nbsp; Jwt\_\_Issuer

&nbsp; Jwt\_\_Audience

