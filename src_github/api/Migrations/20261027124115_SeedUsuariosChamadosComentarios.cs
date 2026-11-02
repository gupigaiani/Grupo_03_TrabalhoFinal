using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace UsuariosApi.Migrations
{
    /// <inheritdoc />
    public partial class SeedUsuariosChamadosComentarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Usuarios",
                columns: new[] { "Id", "DataCriacao", "Email", "Nome", "Senha", "Tipo" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 10, 27, 9, 31, 35, 292, DateTimeKind.Unspecified).AddTicks(9494), "usuario@gmail.com", "João da Silva", "Senha123", "Usuario" },
                    { 2, new DateTime(2026, 10, 27, 9, 32, 2, 671, DateTimeKind.Unspecified).AddTicks(1620), "tecnico@gmail.com", "Gabriel Costa", "Senha123", "Tecnico" },
                    { 3, new DateTime(2026, 10, 27, 9, 32, 31, 667, DateTimeKind.Unspecified).AddTicks(1228), "gestor@gmail.com", "Luiz Silveira", "Senha123", "Admin" }
                });

            migrationBuilder.InsertData(
                table: "Chamados",
                columns: new[] { "Id", "CategoriaId", "DataAbertura", "DataFechamento", "Descricao", "PrioridadeId", "StatusId", "TecnicoId", "Titulo", "UsuarioId" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 10, 27, 9, 34, 13, 309, DateTimeKind.Unspecified).AddTicks(7614), new DateTime(2026, 10, 27, 9, 38, 34, 156, DateTimeKind.Unspecified).AddTicks(1336), "A impressora não está funcionando desde ontem.", 1, 3, 2, "Impressora não funciona", 1 },
                    { 2, 2, new DateTime(2026, 10, 27, 9, 36, 26, 667, DateTimeKind.Unspecified).AddTicks(9442), null, "Estou tentando acessar o portal da empresa e dá acesso negado.", 2, 2, 2, "Portal da empresa está fora", 1 },
                    { 3, 3, new DateTime(2026, 10, 27, 9, 37, 16, 917, DateTimeKind.Unspecified).AddTicks(6033), null, "Já tem duas horas que a internet não funciona.", 3, 1, null, "Wi-Fi não funciona", 1 }
                });

            migrationBuilder.InsertData(
                table: "Comentarios",
                columns: new[] { "Id", "ChamadoId", "DataEnvio", "Mensagem", "UsuarioId" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 10, 27, 9, 38, 19, 763, DateTimeKind.Unspecified).AddTicks(1189), "Olá João!", 2 },
                    { 2, 1, new DateTime(2026, 10, 27, 9, 38, 54, 367, DateTimeKind.Unspecified).AddTicks(1343), "Olá Gabriel!", 1 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Chamados",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Chamados",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Comentarios",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Comentarios",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Chamados",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Usuarios",
                keyColumn: "Id",
                keyValue: 2);
        }
    }
}
