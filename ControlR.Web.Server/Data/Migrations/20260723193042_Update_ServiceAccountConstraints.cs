using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

/// <inheritdoc />
public partial class Update_ServiceAccountConstraints : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropCheckConstraint(
        name: "CK_ServiceAccounts_Kind_Allowed",
        table: "ServiceAccounts");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddCheckConstraint(
        name: "CK_ServiceAccounts_Kind_Allowed",
        table: "ServiceAccounts",
        sql: "\"Kind\" IN ('Server', 'Tenant')");
  }
}
