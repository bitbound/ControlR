using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

/// <inheritdoc />
public partial class Add_Invites : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.CreateTable(
        name: "TenantInvites",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false),
          ActivationCode = table.Column<string>(type: "text", nullable: false),
          InviteeEmail = table.Column<string>(type: "text", nullable: false),
          CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
          TenantId = table.Column<Guid>(type: "uuid", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_TenantInvites", x => x.Id);
          table.ForeignKey(
                    name: "FK_TenantInvites_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateIndex(
        name: "IX_TenantInvites_ActivationCode",
        table: "TenantInvites",
        column: "ActivationCode");

    migrationBuilder.CreateIndex(
        name: "IX_TenantInvites_TenantId",
        table: "TenantInvites",
        column: "TenantId");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "TenantInvites");
  }
}
