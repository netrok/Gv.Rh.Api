using System.Text;
using System.Text.Json.Serialization;
using Gv.Rh.Api.Middlewares;
using Gv.Rh.Api.Services;
using Gv.Rh.Application.Abstractions.Reports;
using Gv.Rh.Infrastructure.Persistence;
using Gv.Rh.Infrastructure.Reports;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;

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
        Description = "Escribe: Bearer {tu_token}"
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

// CORS (React/Vite)
const string CorsPolicyName = "WebRh";
builder.Services.AddCors(opt =>
{
    opt.AddPolicy(CorsPolicyName, p =>
        p.WithOrigins("http://localhost:5173", "http://localhost:3000")
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials()
    );
});

// JWT Auth
var jwt = builder.Configuration.GetSection("Jwt");

var key = jwt["Key"] ?? throw new InvalidOperationException(
    "Falta Jwt:Key. Configúralo con User Secrets (DEV) o variables de entorno (PROD). No lo pongas en el repo."
);

var issuer = jwt["Issuer"] ?? throw new InvalidOperationException("Falta Jwt:Issuer.");
var audience = jwt["Audience"] ?? throw new InvalidOperationException("Falta Jwt:Audience.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.FromSeconds(30)
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
builder.Services.AddScoped<IEmpleadoDocumentoStorageService, EmpleadoDocumentoStorageService>();

// DbContext (PostgreSQL) + interceptor
builder.Services.AddDbContext<RhDbContext>((sp, opt) =>
{
    opt.UseNpgsql(builder.Configuration.GetConnectionString("RhDb"));

    // Si instalaste EFCore.NamingConventions:
    // opt.UseSnakeCaseNamingConvention();

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

// Seed + cleanup (antes de arrancar)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RhDbContext>();
    await DbSeeder.SeedAsync(db);

    var tokens = scope.ServiceProvider.GetRequiredService<TokenService>();
    var deleted = await tokens.CleanupExpiredTokensAsync();
    Console.WriteLine($"[Auth] Refresh tokens expirados eliminados: {deleted}");
}

app.Run();