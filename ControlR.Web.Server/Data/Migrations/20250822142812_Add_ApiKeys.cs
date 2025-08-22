using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

  /// <inheritdoc />
  public partial class Add_ApiKeys : Migration
  {
      /// <inheritdoc />
      protected override void Up(MigrationBuilder migrationBuilder)
      {
          migrationBuilder.AlterColumn<DateTimeOffset>(
              name: "LockoutEnd",
              table: "AspNetUsers",
              type: "timestamp with time zone",
              nullable: true,
              oldClrType: typeof(DateTimeOffset),
              oldType: "timestamp with time zone",
              oldNullable: true,
              oldDefaultValueSql: "CURRENT_TIMESTAMP");

          migrationBuilder.CreateTable(
              name: "ApiKeys",
              columns: table => new
              {
                  Id = table.Column<Guid>(type: "uuid", nullable: false),
                  FriendlyName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                  HashedKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                  LastUsed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                  CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                  TenantId = table.Column<Guid>(type: "uuid", nullable: false)
              },
              constraints: table =>
              {
                  table.PrimaryKey("PK_ApiKeys", x => x.Id);
                  table.ForeignKey(
                      name: "FK_ApiKeys_Tenants_TenantId",
                      column: x => x.TenantId,
                      principalTable: "Tenants",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
              });

          migrationBuilder.CreateIndex(
              name: "IX_ApiKeys_HashedKey",
              table: "ApiKeys",
              column: "HashedKey",
              unique: true);

          migrationBuilder.CreateIndex(
              name: "IX_ApiKeys_TenantId",
              table: "ApiKeys",
              column: "TenantId");
      }

      /// <inheritdoc />
      protected override void Down(MigrationBuilder migrationBuilder)
      {
          migrationBuilder.DropTable(
              name: "ApiKeys");

          migrationBuilder.AlterColumn<DateTimeOffset>(
              name: "LockoutEnd",
              table: "AspNetUsers",
              type: "timestamp with time zone",
              nullable: true,
              defaultValueSql: "CURRENT_TIMESTAMP",
              oldClrType: typeof(DateTimeOffset),
              oldType: "timestamp with time zone",
              oldNullable: true);
      }
  }
