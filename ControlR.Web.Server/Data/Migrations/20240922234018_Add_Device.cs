using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControlR.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_Device : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgentVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Alias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CpuUtilization = table.Column<double>(type: "double precision", nullable: false),
                    CurrentUsers = table.Column<string>(type: "text", nullable: false),
                    Drives = table.Column<string>(type: "text", nullable: false),
                    DeviceId = table.Column<int>(type: "integer", nullable: false),
                    Is64Bit = table.Column<bool>(type: "boolean", nullable: false),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MacAddresses = table.Column<string[]>(type: "text[]", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OsArchitecture = table.Column<int>(type: "integer", nullable: false),
                    OsDescription = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Platform = table.Column<int>(type: "integer", nullable: false),
                    ProcessorCount = table.Column<int>(type: "integer", nullable: false),
                    PublicIpV4 = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    PublicIpV6 = table.Column<string>(type: "character varying(39)", maxLength: 39, nullable: false),
                    TotalMemory = table.Column<double>(type: "double precision", nullable: false),
                    TotalStorage = table.Column<double>(type: "double precision", nullable: false),
                    UsedMemory = table.Column<double>(type: "double precision", nullable: false),
                    UsedStorage = table.Column<double>(type: "double precision", nullable: false),
                    Uid = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Uid",
                table: "Devices",
                column: "Uid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Devices");
        }
    }
}
