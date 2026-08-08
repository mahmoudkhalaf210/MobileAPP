using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Snap.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCancelReasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CancelReasonId",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CancelReasons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TextEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TextAr = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CancelReasons", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CancelReasons");

            migrationBuilder.DropColumn(
                name: "CancelReasonId",
                table: "Orders");
        }
    }
}
