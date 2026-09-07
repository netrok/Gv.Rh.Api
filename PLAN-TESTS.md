# Plan: proyecto de pruebas (Gv.Rh.Tests)

> Para retomar en una conversación nueva. Comparte este archivo o pega su contenido al iniciar.

## Contexto
Proyecto `Gv.Rh.Api` (.NET 8, PostgreSQL, EF Core). Ya se completaron 5 mejoras de seguridad/mantenimiento (ver `MEJORAS.md` en el repo). El siguiente pendiente prioritario es crear un proyecto de pruebas — actualmente la solución no tiene ninguno.

## Estructura propuesta

```
Gv.Rh.Tests/
├── Gv.Rh.Tests.csproj
├── Vacaciones/
│   ├── VacacionesServiceTests.cs      ← empezar aquí
│   └── ResolverDiasVacacionesTests.cs
├── Auth/
│   ├── TokenServiceTests.cs
│   └── AuthControllerTests.cs
└── TestHelpers/
    └── InMemoryDbContextFactory.cs
```

## Paquetes necesarios
- xunit
- xunit.runner.visualstudio
- Microsoft.NET.Test.Sdk
- Moq (o NSubstitute)
- Microsoft.EntityFrameworkCore.InMemory

## Primeros casos de prueba (VacacionesService), en orden de prioridad
1. Cálculo de antigüedad — empleado que ingresa hoy vs. hace 1 año vs. medio año
2. Corte de aniversario — 1 día antes de cumplir el año no debe contar el año completo
3. Tabla de política configurable — `ResolverDiasVacaciones` devuelve días correctos según `VacacionPolitica.Detalles`
4. Caso borde: 0 años de servicio → nunca negativo (`Math.Max(0, ...)`)
5. Generar periodo duplicado — comportamiento esperado si ya existe periodo para ese año

## Patrón de cada test (Arrange-Act-Assert)
```csharp
[Fact]
public void ResolverDiasVacaciones_EmpleadoConUnAnioExacto_DevuelveDiasCorrectos()
{
    // Arrange: crea una VacacionPolitica de prueba con detalles conocidos
    // Act: llama al método con anioServicio = 1
    // Assert: Assert.Equal(diasEsperados, resultado)
}
```

## Después de VacacionesService
- TokenService (rotación de refresh tokens, expiración)
- Con tests como red de seguridad: planear migración de EF Core/JwtBearer/Npgsql a .NET 10

## Cómo retomar
Al abrir la conversación nueva, decir algo como:
*"Quiero crear el proyecto Gv.Rh.Tests para Gv.Rh.Api, empezando por VacacionesService. Aquí está el plan que armamos"* + pegar este archivo.