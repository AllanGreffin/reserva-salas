using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ReservaSalas.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Seed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Salas",
                columns: new[] { "Id", "Capacidade", "Nome" },
                values: new object[,]
                {
                    { 1, 4, "Sala Azul" },
                    { 2, 8, "Sala Verde" },
                    { 3, 30, "Auditório" }
                });

            migrationBuilder.InsertData(
                table: "Usuarios",
                columns: new[] { "Id", "Login", "Nome", "Perfil", "SenhaHash" },
                values: new object[,]
                {
                    { 1, "ana", "Ana Souza", "Comum", "AQAAAAIAAYagAAAAEKMUfEqWOuU/5iOKb2xd/QzK/KQy5xKRsCSDuKcgVL1gnjjaIM7qCV9rzVVCmTyOBw==" },
                    { 2, "bruno", "Bruno Lima", "Comum", "AQAAAAIAAYagAAAAEItBrlv/fcG7Y5yo/b28LYTklG0WS9W6/LQDKOIRKxxeGH5n5CSilz/u0mWxSBtj/Q==" },
                    { 3, "admin", "Administrador", "Administrador", "AQAAAAIAAYagAAAAEAQgD+atyoJ8S7E5Fr70nMvomqhIEc6MGOgyUzKIxIny6Mty2Y/Ey7GmfsJ/q3md5w==" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Salas",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Salas",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Salas",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 3);
        }
    }
}
