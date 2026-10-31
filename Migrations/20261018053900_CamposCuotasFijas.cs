using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrestamosApi.Migrations
{
    /// <inheritdoc />
    public partial class CamposCuotasFijas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Periodos_PrestamoId",
                table: "Periodos");

            migrationBuilder.AddColumn<int>(
                name: "PrestamoAnteriorId",
                table: "Prestamos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TasaMenor",
                table: "Prestamos",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CapitalPagado",
                table: "Periodos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Diferencia",
                table: "Periodos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InteresMenor",
                table: "Periodos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Numero",
                table: "Periodos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ADiferencia",
                table: "Pagos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AInteresMenor",
                table: "Pagos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PeriodoId",
                table: "Pagos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Periodos_PrestamoId_Numero",
                table: "Periodos",
                columns: new[] { "PrestamoId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Periodos_PrestamoId_Numero",
                table: "Periodos");

            migrationBuilder.DropColumn(
                name: "PrestamoAnteriorId",
                table: "Prestamos");

            migrationBuilder.DropColumn(
                name: "TasaMenor",
                table: "Prestamos");

            migrationBuilder.DropColumn(
                name: "CapitalPagado",
                table: "Periodos");

            migrationBuilder.DropColumn(
                name: "Diferencia",
                table: "Periodos");

            migrationBuilder.DropColumn(
                name: "InteresMenor",
                table: "Periodos");

            migrationBuilder.DropColumn(
                name: "Numero",
                table: "Periodos");

            migrationBuilder.DropColumn(
                name: "ADiferencia",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "AInteresMenor",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "PeriodoId",
                table: "Pagos");

            migrationBuilder.CreateIndex(
                name: "IX_Periodos_PrestamoId",
                table: "Periodos",
                column: "PrestamoId");
        }
    }
}
