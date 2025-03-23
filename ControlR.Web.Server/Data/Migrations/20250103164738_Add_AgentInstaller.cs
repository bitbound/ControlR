using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

/// <inheritdoc />
public partial class Add_AgentInstaller : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.InsertData(
        table: "AspNetRoles",
        columns: ["Id", "ConcurrencyStamp", "Name", "NormalizedName"],
        values: [new Guid("dde33610-89dc-e6a4-8d8a-33f3823a180e"), null, "Agent Installer", "AGENT INSTALLER"]);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DeleteData(
        table: "AspNetRoles",
        keyColumn: "Id",
        keyValue: new Guid("dde33610-89dc-e6a4-8d8a-33f3823a180e"));
  }
}
