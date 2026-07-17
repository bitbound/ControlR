using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

/// <inheritdoc />
public partial class Add_ExternalUserColumns : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<string>(
        name: "AccountType",
        table: "AspNetUsers",
        type: "character varying(50)",
        maxLength: 50,
        nullable: false,
        defaultValue: "User");

    migrationBuilder.AddColumn<DateTimeOffset>(
        name: "LastLogin",
        table: "AspNetUsers",
        type: "timestamp with time zone",
        nullable: true);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropColumn(
        name: "AccountType",
        table: "AspNetUsers");

    migrationBuilder.DropColumn(
        name: "LastLogin",
        table: "AspNetUsers");
  }
}
