using Gv.Rh.Api.Middlewares;
using Gv.Rh.Api.Services;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// Swagger + Bearer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Gv.Rh.Api", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Escribe: Bearer {tu_token}"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

// Services
builder.Services.AddScoped<TokenService>();

// Auditoría (interceptor)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

// DbContext (PostgreSQL) + interceptor
builder.Services.AddDbContext<RhDbContext>((sp, opt) =>
{
    opt.UseNpgsql(builder.Configuration.GetConnectionString("RhDb"));

    // ✅ SOLO si instalaste EFCore.NamingConventions 8.0.3
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

// ✅ Bloquea el sistema hasta cambiar password si MustChangePassword=true
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