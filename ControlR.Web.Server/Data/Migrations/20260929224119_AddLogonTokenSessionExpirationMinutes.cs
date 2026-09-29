using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

/// <inheritdoc />
public partial class AddLogonTokenSessionExpirationMinutes : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<int>(
        name: "SessionExpirationMinutes",
        table: "LogonTokens",
        type: "integer",
        nullable: false,
        defaultValue: 480);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropColumn(
        name: "SessionExpirationMinutes",
        table: "LogonTokens");
  }
}
