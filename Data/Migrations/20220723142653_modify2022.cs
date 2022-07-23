using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSharp.Gateway.Modbus.Migrations
{
    public partial class modify2022 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "SlaveCode",
                table: "PointMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: (byte)0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlaveCode",
                table: "PointMappings");
        }
    }
}
