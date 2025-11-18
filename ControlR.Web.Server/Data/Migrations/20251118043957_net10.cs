using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

  /// <inheritdoc />
  public partial class net10 : Migration
  {
      /// <inheritdoc />
      protected override void Up(MigrationBuilder migrationBuilder)
      {
          migrationBuilder.AlterColumn<string>(
              name: "Name",
              table: "AspNetUserTokens",
              type: "character varying(128)",
              maxLength: 128,
              nullable: false,
              oldClrType: typeof(string),
              oldType: "text");

          migrationBuilder.AlterColumn<string>(
              name: "LoginProvider",
              table: "AspNetUserTokens",
              type: "character varying(128)",
              maxLength: 128,
              nullable: false,
              oldClrType: typeof(string),
              oldType: "text");

          migrationBuilder.AlterColumn<string>(
              name: "PhoneNumber",
              table: "AspNetUsers",
              type: "character varying(256)",
              maxLength: 256,
              nullable: true,
              oldClrType: typeof(string),
              oldType: "text",
              oldNullable: true);

          migrationBuilder.AlterColumn<string>(
              name: "ProviderKey",
              table: "AspNetUserLogins",
              type: "character varying(128)",
              maxLength: 128,
              nullable: false,
              oldClrType: typeof(string),
              oldType: "text");

          migrationBuilder.AlterColumn<string>(
              name: "LoginProvider",
              table: "AspNetUserLogins",
              type: "character varying(128)",
              maxLength: 128,
              nullable: false,
              oldClrType: typeof(string),
              oldType: "text");

          migrationBuilder.CreateTable(
              name: "AspNetUserPasskeys",
              columns: table => new
              {
                  CredentialId = table.Column<byte[]>(type: "bytea", maxLength: 1024, nullable: false),
                  UserId = table.Column<Guid>(type: "uuid", nullable: false),
                  Data = table.Column<string>(type: "jsonb", nullable: false)
              },
              constraints: table =>
              {
                  table.PrimaryKey("PK_AspNetUserPasskeys", x => x.CredentialId);
                  table.ForeignKey(
                      name: "FK_AspNetUserPasskeys_AspNetUsers_UserId",
                      column: x => x.UserId,
                      principalTable: "AspNetUsers",
                      principalColumn: "Id",
                      onDelete: ReferentialAction.Cascade);
              });

          migrationBuilder.UpdateData(
              table: "AspNetRoles",
              keyColumn: "Id",
              keyValue: new Guid("8ad85243-aa78-7539-0bf7-0cd6f27bcaa5"),
              column: "ConcurrencyStamp",
              value: "d6b798d2-a7f0-492b-a6ad-7eba9b1e3beb");

          migrationBuilder.UpdateData(
              table: "AspNetRoles",
              keyColumn: "Id",
              keyValue: new Guid("98aecfed-4095-42fd-e4b8-556d5b723bb6"),
              column: "ConcurrencyStamp",
              value: "0b692fe4-63e1-4a99-b021-4fc48ed81f4c");

          migrationBuilder.UpdateData(
              table: "AspNetRoles",
              keyColumn: "Id",
              keyValue: new Guid("dde33610-89dc-e6a4-8d8a-33f3823a180e"),
              column: "ConcurrencyStamp",
              value: "ccfd2843-8a06-43d4-9bf3-6110b4e65900");

          migrationBuilder.UpdateData(
              table: "AspNetRoles",
              keyColumn: "Id",
              keyValue: new Guid("ed0dddf2-c2b2-4160-9ece-4a9e03b2e828"),
              column: "ConcurrencyStamp",
              value: "b23bdf83-ecc8-4ca2-ba24-dc1780bfefc6");

          migrationBuilder.CreateIndex(
              name: "IX_AspNetUserPasskeys_UserId",
              table: "AspNetUserPasskeys",
              column: "UserId");
      }

      /// <inheritdoc />
      protected override void Down(MigrationBuilder migrationBuilder)
      {
          migrationBuilder.DropTable(
              name: "AspNetUserPasskeys");

          migrationBuilder.AlterColumn<string>(
              name: "Name",
              table: "AspNetUserTokens",
              type: "text",
              nullable: false,
              oldClrType: typeof(string),
              oldType: "character varying(128)",
              oldMaxLength: 128);

          migrationBuilder.AlterColumn<string>(
              name: "LoginProvider",
              table: "AspNetUserTokens",
              type: "text",
              nullable: false,
              oldClrType: typeof(string),
              oldType: "character varying(128)",
              oldMaxLength: 128);

          migrationBuilder.AlterColumn<string>(
              name: "PhoneNumber",
              table: "AspNetUsers",
              type: "text",
              nullable: true,
              oldClrType: typeof(string),
              oldType: "character varying(256)",
              oldMaxLength: 256,
              oldNullable: true);

          migrationBuilder.AlterColumn<string>(
              name: "ProviderKey",
              table: "AspNetUserLogins",
              type: "text",
              nullable: false,
              oldClrType: typeof(string),
              oldType: "character varying(128)",
              oldMaxLength: 128);

          migrationBuilder.AlterColumn<string>(
              name: "LoginProvider",
              table: "AspNetUserLogins",
              type: "text",
              nullable: false,
              oldClrType: typeof(string),
              oldType: "character varying(128)",
              oldMaxLength: 128);

          migrationBuilder.UpdateData(
              table: "AspNetRoles",
              keyColumn: "Id",
              keyValue: new Guid("8ad85243-aa78-7539-0bf7-0cd6f27bcaa5"),
              column: "ConcurrencyStamp",
              value: null);

          migrationBuilder.UpdateData(
              table: "AspNetRoles",
              keyColumn: "Id",
              keyValue: new Guid("98aecfed-4095-42fd-e4b8-556d5b723bb6"),
              column: "ConcurrencyStamp",
              value: null);

          migrationBuilder.UpdateData(
              table: "AspNetRoles",
              keyColumn: "Id",
              keyValue: new Guid("dde33610-89dc-e6a4-8d8a-33f3823a180e"),
              column: "ConcurrencyStamp",
              value: null);

          migrationBuilder.UpdateData(
              table: "AspNetRoles",
              keyColumn: "Id",
              keyValue: new Guid("ed0dddf2-c2b2-4160-9ece-4a9e03b2e828"),
              column: "ConcurrencyStamp",
              value: null);
      }
  }
