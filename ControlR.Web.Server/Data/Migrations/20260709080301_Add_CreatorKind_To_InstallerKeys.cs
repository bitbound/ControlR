using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

/// <inheritdoc />
public partial class Add_CreatorKind_To_InstallerKeys : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<int>(
        name: "CreatorKind",
        table: "AgentInstallerKeys",
        type: "integer",
        nullable: false,
        defaultValue: 0);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropColumn(
        name: "CreatorKind",
        table: "AgentInstallerKeys");
  }
}
