using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

/// <inheritdoc />
public partial class Add_ServiceAccounts : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.CreateTable(
        name: "ServiceAccounts",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
          Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
          TenantId = table.Column<Guid>(type: "uuid", nullable: true),
          Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
          Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
          IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
          CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_ServiceAccounts", x => x.Id);
          table.ForeignKey(
                    name: "FK_ServiceAccounts_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateTable(
        name: "ServiceAccountCredentials",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
          ServiceAccountId = table.Column<Guid>(type: "uuid", nullable: false),
          Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
          HashedSecret = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
          ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
          CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_ServiceAccountCredentials", x => x.Id);
          table.ForeignKey(
                    name: "FK_ServiceAccountCredentials_ServiceAccounts_ServiceAccountId",
                    column: x => x.ServiceAccountId,
                    principalTable: "ServiceAccounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateIndex(
        name: "IX_ServiceAccountCredentials_Id",
        table: "ServiceAccountCredentials",
        column: "Id");

    migrationBuilder.CreateIndex(
        name: "IX_ServiceAccountCredentials_ServiceAccountId",
        table: "ServiceAccountCredentials",
        column: "ServiceAccountId");

    migrationBuilder.Sql(
        "CREATE UNIQUE INDEX \"IX_ServiceAccounts_Kind_TenantId_Name\" " +
        "ON \"ServiceAccounts\" (\"Kind\", \"TenantId\", \"Name\") " +
        "NULLS NOT DISTINCT;");

    migrationBuilder.CreateIndex(
        name: "IX_ServiceAccounts_TenantId",
        table: "ServiceAccounts",
        column: "TenantId");

    migrationBuilder.Sql(
        "ALTER TABLE \"ServiceAccounts\" " +
        "ADD CONSTRAINT \"CK_ServiceAccounts_Kind_TenantId\" " +
        "CHECK (\"Kind\" = 'Server' AND \"TenantId\" IS NULL " +
        "OR \"Kind\" = 'Tenant' AND \"TenantId\" IS NOT NULL);");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "ServiceAccountCredentials");

    migrationBuilder.DropTable(
        name: "ServiceAccounts");
  }
}
