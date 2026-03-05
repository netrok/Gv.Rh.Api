using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gv.Rh.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Empleados_NumEmpleado_Sequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_class WHERE relkind = 'S' AND relname = 'empleados_num_seq') THEN
        CREATE SEQUENCE empleados_num_seq START WITH 1 INCREMENT BY 1 MINVALUE 1;
    END IF;
END $$;
");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP SEQUENCE IF EXISTS empleados_num_seq;");

        }
    }
}
