using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_DeviceDnsHostName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DnsHostName",
                table: "Devices",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DnsHostName",
                table: "Devices");
        }
    }
}
