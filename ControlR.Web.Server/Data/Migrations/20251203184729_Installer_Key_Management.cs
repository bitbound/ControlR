using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

  /// <inheritdoc />
  public partial class Installer_Key_Management : Migration
  {
      /// <inheritdoc />
      protected override void Up(MigrationBuilder migrationBuilder)
      {
          migrationBuilder.CreateTable(
              name: "AgentInstallerKeys",
              columns: table => new
              {
                  Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                  AllowedUses = table.Column<long>(type: "bigint", nullable: true),
                  CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                  Expiration = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                  FriendlyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                  HashedKey = table.Column<string>(type: "text", nullable: false),
                  KeyType = table.Column<int>(type: "integer", nullable: false),
                  CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                  TenantId = table.Column<Guid>(type: "uuid", nullable: false)
              },
              constraints: table =>
              {
                  table.PrimaryKey("PK_AgentInstallerKeys", x => x.Id);
                  table.ForeignKey(
                      name: "FK_AgentInstallerKeys_Tenants_TenantId",
                      column: x => x.TenantId,
                      principalTable: "Tenants",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
              });

          migrationBuilder.CreateTable(
              name: "AgentInstallerKeyUsages",
              columns: table => new
              {
                  Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                  AgentInstallerKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                  DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                  CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                  TenantId = table.Column<Guid>(type: "uuid", nullable: false)
              },
              constraints: table =>
              {
                  table.PrimaryKey("PK_AgentInstallerKeyUsages", x => x.Id);
                  table.ForeignKey(
                      name: "FK_AgentInstallerKeyUsages_AgentInstallerKeys_AgentInstallerKe~",
                      column: x => x.AgentInstallerKeyId,
                      principalTable: "AgentInstallerKeys",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
                  table.ForeignKey(
                      name: "FK_AgentInstallerKeyUsages_Tenants_TenantId",
                      column: x => x.TenantId,
                      principalTable: "Tenants",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
              });

          migrationBuilder.InsertData(
              table: "AspNetRoles",
              columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
              values: new object[] { new Guid("963de2cb-fc55-43cd-11ac-dd6261c81bd8"), "a7e1a339-19c3-4d44-97e3-239636906a45", "Installer Key Manager", "INSTALLER KEY MANAGER" });

          migrationBuilder.CreateIndex(
              name: "IX_AgentInstallerKeys_TenantId",
              table: "AgentInstallerKeys",
              column: "TenantId");

          migrationBuilder.CreateIndex(
              name: "IX_AgentInstallerKeyUsages_AgentInstallerKeyId",
              table: "AgentInstallerKeyUsages",
              column: "AgentInstallerKeyId");

          migrationBuilder.CreateIndex(
              name: "IX_AgentInstallerKeyUsages_TenantId",
              table: "AgentInstallerKeyUsages",
              column: "TenantId");
      }

      /// <inheritdoc />
      protected override void Down(MigrationBuilder migrationBuilder)
      {
          migrationBuilder.DropTable(
              name: "AgentInstallerKeyUsages");

          migrationBuilder.DropTable(
              name: "AgentInstallerKeys");

          migrationBuilder.DeleteData(
              table: "AspNetRoles",
              keyColumn: "Id",
              keyValue: new Guid("963de2cb-fc55-43cd-11ac-dd6261c81bd8"));
      }
  }
