using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReservaSalas.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class VersaoDaReserva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Versao",
                table: "Reservas",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Reservas");
        }
    }
}
