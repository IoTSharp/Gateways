using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSharp.Gateways.Migrations
{
    public partial class renamefield : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cycles",
                table: "PointMappings");

            migrationBuilder.RenameColumn(
                name: "DateTimeFormat",
                table: "PointMappings",
                newName: "DataFormat");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DataFormat",
                table: "PointMappings",
                newName: "DateTimeFormat");

            migrationBuilder.AddColumn<int>(
                name: "Cycles",
                table: "PointMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
