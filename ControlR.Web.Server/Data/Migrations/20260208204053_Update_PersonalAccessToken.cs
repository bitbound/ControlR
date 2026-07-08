using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

  /// <inheritdoc />
  public partial class Update_PersonalAccessToken : Migration
  {
      /// <inheritdoc />
      protected override void Up(MigrationBuilder migrationBuilder)
      {
          migrationBuilder.DropForeignKey(
              name: "FK_PersonalAccessTokens_Tenants_TenantId",
              table: "PersonalAccessTokens");

          migrationBuilder.DropForeignKey(
              name: "FK_UserPreferences_Tenants_TenantId",
              table: "UserPreferences");

          migrationBuilder.DropIndex(
              name: "IX_UserPreferences_TenantId",
              table: "UserPreferences");

          migrationBuilder.DropIndex(
              name: "IX_PersonalAccessTokens_TenantId",
              table: "PersonalAccessTokens");

          migrationBuilder.DropColumn(
              name: "TenantId",
              table: "UserPreferences");

          migrationBuilder.DropColumn(
              name: "TenantId",
              table: "PersonalAccessTokens");
      }

      /// <inheritdoc />
      protected override void Down(MigrationBuilder migrationBuilder)
      {
          // Delete all PersonalAccessTokens and UserPreferences to avoid issues with existing records 
          // that would have null TenantId or invalid TenantId values after the column is added back. 
          // Records can be recreated after the migration if needed.
          migrationBuilder.Sql("DELETE FROM \"PersonalAccessTokens\"");
          migrationBuilder.Sql("DELETE FROM \"UserPreferences\"");

          migrationBuilder.AddColumn<Guid>(
              name: "TenantId",
              table: "UserPreferences",
              type: "uuid",
              nullable: false,
              defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

          migrationBuilder.AddColumn<Guid>(
              name: "TenantId",
              table: "PersonalAccessTokens",
              type: "uuid",
              nullable: false,
              defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

          migrationBuilder.CreateIndex(
              name: "IX_UserPreferences_TenantId",
              table: "UserPreferences",
              column: "TenantId");

          migrationBuilder.CreateIndex(
              name: "IX_PersonalAccessTokens_TenantId",
              table: "PersonalAccessTokens",
              column: "TenantId");

          migrationBuilder.AddForeignKey(
              name: "FK_PersonalAccessTokens_Tenants_TenantId",
              table: "PersonalAccessTokens",
              column: "TenantId",
              principalTable: "Tenants",
              principalColumn: "Id",
              onDelete: ReferentialAction.Cascade);

          migrationBuilder.AddForeignKey(
              name: "FK_UserPreferences_Tenants_TenantId",
              table: "UserPreferences",
              column: "TenantId",
              principalTable: "Tenants",
              principalColumn: "Id",
              onDelete: ReferentialAction.Cascade);
      }
  }
