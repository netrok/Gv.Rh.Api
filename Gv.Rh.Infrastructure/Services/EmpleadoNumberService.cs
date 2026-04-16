using Gv.Rh.Application.Interfaces;
using Gv.Rh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Gv.Rh.Infrastructure.Services;

public sealed class EmpleadoNumberService : IEmpleadoNumberService
{
    private readonly RhDbContext _db;

    public EmpleadoNumberService(RhDbContext db)
    {
        _db = db;
    }

    public async Task<string> GenerateNextAsync(CancellationToken cancellationToken = default)
    {
        var conn = _db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;

        if (shouldClose)
            await conn.OpenAsync(cancellationToken);

        try
        {
            await EnsureEmpleadoSequenceAsync(conn, cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT nextval('public.empleados_num_seq');";

            var result = await cmd.ExecuteScalarAsync(cancellationToken);

            if (result is null || result == DBNull.Value)
                throw new InvalidOperationException("No se pudo generar el siguiente número de empleado.");

            var nextValue = Convert.ToInt64(result);
            return nextValue.ToString("D6");
        }
        finally
        {
            if (shouldClose)
                await conn.CloseAsync();
        }
    }

    public async Task<string> PeekNextAsync(CancellationToken cancellationToken = default)
    {
        var conn = _db.Database.GetDbConnection();
        var shouldClose = conn.State != ConnectionState.Open;

        if (shouldClose)
            await conn.OpenAsync(cancellationToken);

        try
        {
            await EnsureEmpleadoSequenceAsync(conn, cancellationToken);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT CASE
    WHEN is_called THEN last_value + 1
    ELSE last_value
END
FROM public.empleados_num_seq;";

            var result = await cmd.ExecuteScalarAsync(cancellationToken);

            if (result is null || result == DBNull.Value)
                throw new InvalidOperationException("No se pudo obtener el siguiente número sugerido de empleado.");

            var nextValue = Convert.ToInt64(result);
            return nextValue.ToString("D6");
        }
        finally
        {
            if (shouldClose)
                await conn.CloseAsync();
        }
    }

    private static async Task EnsureEmpleadoSequenceAsync(
        DbConnection conn,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
DO $$
DECLARE
    v_max_num bigint;
    v_last_value bigint;
    v_is_called boolean;
    v_next_value bigint;
BEGIN
    CREATE SEQUENCE IF NOT EXISTS public.empleados_num_seq
        AS bigint
        START WITH 1
        INCREMENT BY 1
        NO MINVALUE
        NO MAXVALUE
        NO CYCLE;

    SELECT COALESCE(
        MAX(NULLIF(regexp_replace(""NumEmpleado"", '\D', '', 'g'), '')::bigint),
        0
    )
    INTO v_max_num
    FROM empleados;

    SELECT last_value, is_called
    INTO v_last_value, v_is_called
    FROM public.empleados_num_seq;

    v_next_value := GREATEST(
        v_max_num + 1,
        CASE
            WHEN v_is_called THEN v_last_value + 1
            ELSE v_last_value
        END
    );

    PERFORM setval('public.empleados_num_seq', v_next_value, false);
END $$;
";

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
