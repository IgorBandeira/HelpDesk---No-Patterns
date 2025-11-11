using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpDesk.Migrations
{
    public partial class SeedInitialCategoriesAndUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // pais
            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Name", "ParentId" },
                values: new object[,]
                {
                    { 1, "Infraestrutura", null },
                    { 2, "Aplicações", null },
                    { 3, "Redes", null },
                    { 4, "Segurança", null },
                    { 5, "Suporte", null }
                });

            // users
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Email", "Name", "Role" },
                values: new object[,]
                {
                    { 1, "alice.johnson@acme.com", "Alice Johnson", "Requester" },
                    { 2, "bob.miller@acme.com", "Bob Miller", "Agent" },
                    { 3, "clara.thompson@acme.com", "Clara Thompson", "Manager" },
                    { 4, "david.anderson@acme.com", "David Anderson", "Requester" },
                    { 5, "emily.carter@acme.com", "Emily Carter", "Agent" },
                    { 6, "frank.harris@acme.com", "Frank Harris", "Manager" },
                    { 7, "grace.lewis@acme.com", "Grace Lewis", "Requester" },
                    { 8, "henry.clark@acme.com", "Henry Clark", "Agent" },
                    { 9, "isabella.scott@acme.com", "Isabella Scott", "Manager" },
                    { 10, "jack.wilson@acme.com", "Jack Wilson", "Requester" }
                });

            // filhos
            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Name", "ParentId" },
                values: new object[,]
                {
                    { 6, "Serviços em Nuvem", 1 },
                    { 7, "Bancos de Dados", 2 },
                    { 8, "Firewall", 4 },
                    { 9, "Central de Ajuda", 5 },
                    { 10, "LAN/WAN", 3 }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // remove filhos primeiro (FK)
            migrationBuilder.DeleteData(table: "Categories", keyColumn: "Id", keyValue: 6);
            migrationBuilder.DeleteData(table: "Categories", keyColumn: "Id", keyValue: 7);
            migrationBuilder.DeleteData(table: "Categories", keyColumn: "Id", keyValue: 8);
            migrationBuilder.DeleteData(table: "Categories", keyColumn: "Id", keyValue: 9);
            migrationBuilder.DeleteData(table: "Categories", keyColumn: "Id", keyValue: 10);

            // remove pais
            migrationBuilder.DeleteData(table: "Categories", keyColumn: "Id", keyValue: 1);
            migrationBuilder.DeleteData(table: "Categories", keyColumn: "Id", keyValue: 2);
            migrationBuilder.DeleteData(table: "Categories", keyColumn: "Id", keyValue: 3);
            migrationBuilder.DeleteData(table: "Categories", keyColumn: "Id", keyValue: 4);
            migrationBuilder.DeleteData(table: "Categories", keyColumn: "Id", keyValue: 5);

            // remove users
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: 1);
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: 2);
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: 3);
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: 4);
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: 5);
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: 6);
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: 7);
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: 8);
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: 9);
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValue: 10);
        }
    }
}
