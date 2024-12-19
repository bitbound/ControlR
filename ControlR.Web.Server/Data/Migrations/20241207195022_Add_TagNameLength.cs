using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

/// <inheritdoc />
public partial class Add_TagNameLength : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AlterColumn<string>(
        name: "Name",
        table: "Tags",
        type: "character varying(50)",
        maxLength: 50,
        nullable: false,
        oldClrType: typeof(string),
        oldType: "text");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AlterColumn<string>(
        name: "Name",
        table: "Tags",
        type: "text",
        nullable: false,
        oldClrType: typeof(string),
        oldType: "character varying(50)",
        oldMaxLength: 50);
  }
}
