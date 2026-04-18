using Gv.Rh.Api.Middlewares;
using Gv.Rh.Api.Services;
using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Application.Interfaces;
using Gv.Rh.Application.Interfaces.Reclutamiento;
using Gv.Rh.Infrastructure.Persistence;
using Gv.Rh.Infrastructure.Reports;
using Gv.Rh.Infrastructure.Services;
using Gv.Rh.Infrastructure.Services.Reclutamiento;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF license
QuestPDF.Settings.License = LicenseType.Community;

// Controllers + JSON
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Swagger + Bearer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Gv.Rh.Api",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pega únicamente el access token. No pegues JSON ni refresh token."
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Helper: permite localhost y hosts LAN privados típicos para Vite/React
static bool IsAllowedFrontendOrigin(string? origin)
{
    if (string.IsNullOrWhiteSpace(origin))
        return false;

    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        return false;

    if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        return false;

    if (uri.Port != 5173 && uri.Port != 3000 && uri.Port != 4173)
        return false;

    var host = uri.Host;

    if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1")
        return true;

    if (!IPAddress.TryParse(host, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
        return false;

    var bytes = ip.GetAddressBytes();

    if (bytes[0] == 10)
        return true;

    if (bytes[0] == 192 && bytes[1] == 168)
        return true;

    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        return true;

    return false;
}

// CORS
const string CorsPolicyName = "WebRh";
builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsPolicyName, p =>
        p.SetIsOriginAllowed(IsAllowedFrontendOrigin)
         .AllowAnyHeader()
         .AllowAnyMethod());
});

// JWT Auth
var jwt = builder.Configuration.GetSection("Jwt");

var key = jwt["Key"] ?? throw new InvalidOperationException(
    "Falta Jwt:Key. Configúralo con User Secrets (DEV) o variables de entorno (PROD)."
);

var issuer = jwt["Issuer"] ?? throw new InvalidOperationException("Falta Jwt:Issuer.");
var audience = jwt["Audience"] ?? throw new InvalidOperationException("Falta Jwt:Audience.");

if (key.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres.");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.RequireHttpsMetadata = false;
        opt.SaveToken = true;

        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,

            ValidateAudience = true,
            ValidAudience = audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Infra para auditoría
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

// Services de app
builder.Services.AddScoped<AuditLogger>();
builder.Services.AddScoped<TokenService>();

builder.Services.AddScoped<IIncidenciasReportService, IncidenciasReportService>();
builder.Services.AddScoped<IEmpleadosReportService, EmpleadosReportService>();
builder.Services.AddScoped<IEmpleadoFichaReportService, EmpleadoFichaReportService>();
builder.Services.AddScoped<IAuditReportService, AuditReportService>();
builder.Services.AddScoped<IDepartamentosReportService, DepartamentosReportService>();
builder.Services.AddScoped<IPuestosReportService, PuestosReportService>();
builder.Services.AddScoped<ISucursalesReportService, SucursalesReportService>();

builder.Services.AddScoped<IEmpleadoDocumentoStorageService, EmpleadoDocumentoStorageService>();
builder.Services.AddScoped<IReclutamientoReporteService, ReclutamientoReporteService>();
builder.Services.AddScoped<IEmpleadoNumberService, EmpleadoNumberService>();
builder.Services.AddScoped<IEmpleadoImportService, EmpleadoImportService>();

// DbContext (PostgreSQL) + interceptor
builder.Services.AddDbContext<RhDbContext>((sp, opt) =>
{
    opt.UseNpgsql(builder.Configuration.GetConnectionString("RhDb"));
    opt.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
});

var app = builder.Build();

// Redirect raíz a Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors(CorsPolicyName);

app.UseAuthentication();

// Bloquea el sistema hasta cambiar password si MustChangePassword = true
app.UseMiddleware<ForcePasswordChangeMiddleware>();

app.UseAuthorization();

app.MapControllers();

// Migraciones + seed + cleanup (antes de arrancar)
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");

    var db = scope.ServiceProvider.GetRequiredService<RhDbContext>();

    logger.LogInformation("Aplicando migraciones pendientes de EF Core...");
    await db.Database.MigrateAsync();
    logger.LogInformation("Migraciones EF Core aplicadas correctamente.");

    logger.LogInformation("Ejecutando seeding inicial...");
    await DbSeeder.SeedAsync(db);
    logger.LogInformation("Seeding completado.");

    var tokens = scope.ServiceProvider.GetRequiredService<TokenService>();
    var deleted = await tokens.CleanupExpiredTokensAsync();

    logger.LogInformation("Refresh tokens expirados eliminados: {DeletedCount}", deleted);
    Console.WriteLine($"[Auth] Refresh tokens expirados eliminados: {deleted}");
}

app.Run();