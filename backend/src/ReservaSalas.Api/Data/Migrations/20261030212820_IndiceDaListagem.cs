using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservaSalas.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class IndiceDaListagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Reservas_Inicio_Id",
                table: "Reservas",
                columns: new[] { "Inicio", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservas_Inicio_Id",
                table: "Reservas");
        }
    }
}
