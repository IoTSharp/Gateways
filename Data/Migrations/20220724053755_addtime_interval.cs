using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSharp.Gateway.Modbus.Migrations
{
    public partial class addtime_interval : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Cycles",
                table: "PointMappings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "TimeInterval",
                table: "ModbusSlaves",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cycles",
                table: "PointMappings");

            migrationBuilder.DropColumn(
                name: "TimeInterval",
                table: "ModbusSlaves");
        }
    }
}
