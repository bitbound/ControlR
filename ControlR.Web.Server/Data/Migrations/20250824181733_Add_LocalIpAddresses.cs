using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_LocalIpAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocalIpV4",
                table: "Devices",
                type: "character varying(15)",
                maxLength: 15,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LocalIpV6",
                table: "Devices",
                type: "character varying(39)",
                maxLength: 39,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalIpV4",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LocalIpV6",
                table: "Devices");
        }
    }
}
