using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

/// <inheritdoc />
public partial class Add_TenantNameLength : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AlterColumn<string>(
        name: "Name",
        table: "Tenants",
        type: "character varying(100)",
        maxLength: 100,
        nullable: true,
        oldClrType: typeof(string),
        oldType: "text",
        oldNullable: true);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AlterColumn<string>(
        name: "Name",
        table: "Tenants",
        type: "text",
        nullable: true,
        oldClrType: typeof(string),
        oldType: "character varying(100)",
        oldMaxLength: 100,
        oldNullable: true);
  }
}
