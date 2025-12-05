using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

  /// <inheritdoc />
  public partial class Installer_Key_Update : Migration
  {
      /// <inheritdoc />
      protected override void Up(MigrationBuilder migrationBuilder)
      {
          migrationBuilder.AddColumn<string>(
              name: "RemoteIpAddress",
              table: "AgentInstallerKeyUsages",
              type: "text",
              nullable: true);
      }

      /// <inheritdoc />
      protected override void Down(MigrationBuilder migrationBuilder)
      {
          migrationBuilder.DropColumn(
              name: "RemoteIpAddress",
              table: "AgentInstallerKeyUsages");
      }
  }
