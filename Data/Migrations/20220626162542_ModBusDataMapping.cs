using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IoTSharp.Gateway.Modbus.Migrations
{
    public partial class ModBusDataMapping : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModbusSlaves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Slave = table.Column<string>(type: "TEXT", nullable: false),
                    TimeOut = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceName = table.Column<string>(type: "TEXT", nullable: false),
                    DeviceNameFormat = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModbusSlaves", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PointMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DataName = table.Column<string>(type: "TEXT", nullable: false),
                    DataType = table.Column<int>(type: "INTEGER", nullable: false),
                    DataCatalog = table.Column<int>(type: "INTEGER", nullable: false),
                    FunCode = table.Column<int>(type: "INTEGER", nullable: false),
                    Address = table.Column<int>(type: "INTEGER", nullable: false),
                    Length = table.Column<int>(type: "INTEGER", nullable: false),
                    DateTimeFormat = table.Column<string>(type: "TEXT", nullable: true),
                    CodePage = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointMappings_ModbusSlaves_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "ModbusSlaves",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PointMappings_OwnerId",
                table: "PointMappings",
                column: "OwnerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PointMappings");

            migrationBuilder.DropTable(
                name: "ModbusSlaves");
        }
    }
}
