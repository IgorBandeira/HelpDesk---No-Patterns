using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpDesk.Migrations
{
    public partial class MakeRequesterAssigneeSetNull : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop FKs antigos (qualquer regra anterior)
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_RequesterId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_AssigneeId",
                table: "Tickets");

            // Tornar as colunas NULLABLE (se ainda não estiverem)
            migrationBuilder.AlterColumn<int>(
                name: "RequesterId",
                table: "Tickets",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "AssigneeId",
                table: "Tickets",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // Recriar FKs com ON DELETE SET NULL
            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_RequesterId",
                table: "Tickets",
                column: "RequesterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_AssigneeId",
                table: "Tickets",
                column: "AssigneeId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverter FKs
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_RequesterId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Users_AssigneeId",
                table: "Tickets");

            // Voltar colunas para NOT NULL (se precisar)
            migrationBuilder.AlterColumn<int>(
                name: "RequesterId",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AssigneeId",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Recriar FKs com RESTRICT (padrão anterior)
            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_RequesterId",
                table: "Tickets",
                column: "RequesterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Users_AssigneeId",
                table: "Tickets",
                column: "AssigneeId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
