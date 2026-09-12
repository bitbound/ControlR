using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

/// <inheritdoc />
public partial class Permissions_Phase2 : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropTable(
        name: "AppUserTag");

    migrationBuilder.AddColumn<Guid>(
        name: "CreatedByUserId",
        table: "PersonalAccessTokens",
        type: "uuid",
        nullable: true);

    migrationBuilder.AddColumn<DateTimeOffset>(
        name: "ExpiresAt",
        table: "PersonalAccessTokens",
        type: "timestamp with time zone",
        nullable: true);

    migrationBuilder.AddColumn<DateTimeOffset>(
        name: "RevokedAt",
        table: "PersonalAccessTokens",
        type: "timestamp with time zone",
        nullable: true);

    migrationBuilder.AddColumn<Guid>(
        name: "CustomerId",
        table: "Devices",
        type: "uuid",
        nullable: true);

    migrationBuilder.CreateTable(
        name: "AuthorizationChangeLogs",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
          ActionType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
          ActorPrincipalId = table.Column<Guid>(type: "uuid", nullable: true),
          ActorPrincipalType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
          AfterJson = table.Column<string>(type: "text", nullable: true),
          BeforeJson = table.Column<string>(type: "text", nullable: true),
          CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
          IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
          OwningTenantId = table.Column<Guid>(type: "uuid", nullable: true),
          TargetId = table.Column<Guid>(type: "uuid", nullable: true),
          TargetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
          CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_AuthorizationChangeLogs", x => x.Id);
        });

    migrationBuilder.CreateTable(
        name: "Customers",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
          Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
          Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
          Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
          CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
          TenantId = table.Column<Guid>(type: "uuid", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_Customers", x => x.Id);
          table.ForeignKey(
                    name: "FK_Customers_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateTable(
        name: "DeviceGroups",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
          Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
          Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
          CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
          TenantId = table.Column<Guid>(type: "uuid", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_DeviceGroups", x => x.Id);
          table.ForeignKey(
                    name: "FK_DeviceGroups_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateTable(
        name: "LogonTokens",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
          DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
          ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
          IsConsumed = table.Column<bool>(type: "boolean", nullable: false),
          Prefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
          SessionCorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
          Token = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
          UserCorrelationId = table.Column<string>(type: "character varying(252)", maxLength: 252, nullable: true),
          UserId = table.Column<Guid>(type: "uuid", nullable: false),
          CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
          TenantId = table.Column<Guid>(type: "uuid", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_LogonTokens", x => x.Id);
          table.ForeignKey(
                    name: "FK_LogonTokens_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
          table.ForeignKey(
                    name: "FK_LogonTokens_Devices_DeviceId",
                    column: x => x.DeviceId,
                    principalTable: "Devices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
          table.ForeignKey(
                    name: "FK_LogonTokens_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateTable(
        name: "PermissionAssignments",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
          CreatedByPrincipalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
          CreatedByPrincipalType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
          Effect = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
          IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
          Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
          OwningTenantId = table.Column<Guid>(type: "uuid", nullable: true),
          PermissionName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
          PrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
          PrincipalKind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
          ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
          ScopeKind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
          CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_PermissionAssignments", x => x.Id);
        });

    // Backfill: Map each user's legacy role memberships to the corresponding preset's
    // permission assignments BEFORE the role tables are dropped. No-op on fresh databases
    // (AspNetUserRoles is empty during migration). For upgrades, it preserves existing access.
    // Scope is permission-aware: each row carries the permission's broadest legal
    // scope kind, so server-only permissions backfill to Server scope and tenant/device
    // permissions to Tenant scope — mirroring PermissionPresets + PermissionCatalog so the
    // backfill is a superset of the presets (no upgraded legacy user under-privileged).
    var backfillSql = $"""
              INSERT INTO "PermissionAssignments"
                ("{nameof(PermissionAssignment.PrincipalKind)}", "{nameof(PermissionAssignment.PrincipalId)}", "{nameof(PermissionAssignment.PermissionName)}", "{nameof(PermissionAssignment.Effect)}", "{nameof(PermissionAssignment.ScopeKind)}", "{nameof(PermissionAssignment.ScopeId)}", "{nameof(PermissionAssignment.IsEnabled)}", "{nameof(PermissionAssignment.OwningTenantId)}", "{nameof(PermissionAssignment.CreatedByPrincipalType)}", "{nameof(PermissionAssignment.CreatedByPrincipalId)}")
              SELECT DISTINCT '{PermissionPrincipalKind.User}', ur."UserId", rp."PermissionName", '{PermissionEffect.Allow}',
                rp."ScopeKind",
                CASE
                  WHEN rp."ScopeKind" = '{PermissionScopeKind.Server}' THEN NULL
                  ELSE u."TenantId"
                END,
                true,
                CASE
                  WHEN rp."ScopeKind" = '{PermissionScopeKind.Server}' THEN NULL
                  ELSE u."TenantId"
                END,
                'system', CAST(ur."UserId" AS text)
              FROM "AspNetUserRoles" ur
              INNER JOIN "AspNetRoles" r ON ur."RoleId" = r."Id"
              INNER JOIN "AspNetUsers" u ON ur."UserId" = u."Id"
              INNER JOIN (
                VALUES
                  ('Server Administrator', '{PermissionNames.ServerAuthorizationLogsRead}', '{PermissionScopeKind.Server}'),
                  ('Server Administrator', '{PermissionNames.ServerPermissionsRead}', '{PermissionScopeKind.Server}'),
                  ('Server Administrator', '{PermissionNames.ServerPermissionsWrite}', '{PermissionScopeKind.Server}'),
                  ('Server Administrator', '{PermissionNames.ServerSettingsWrite}', '{PermissionScopeKind.Server}'),
                  ('Server Administrator', '{PermissionNames.ServerTenantsDelete}', '{PermissionScopeKind.Server}'),
                  ('Server Administrator', '{PermissionNames.ServerTenantsRead}', '{PermissionScopeKind.Server}'),
                  ('Server Administrator', '{PermissionNames.ServerTenantsWrite}', '{PermissionScopeKind.Server}'),
                  ('Server Administrator', '{PermissionNames.ServerTelemetryRead}', '{PermissionScopeKind.Server}'),
                  ('Server Administrator', '{PermissionNames.ServerServiceAccountsRead}', '{PermissionScopeKind.Server}'),
                  ('Server Administrator', '{PermissionNames.ServerServiceAccountsWrite}', '{PermissionScopeKind.Server}'),
                  ('Server Administrator', '{PermissionNames.ServerServiceAccountsRotateCredentials}', '{PermissionScopeKind.Server}'),
                  ('Server Administrator', '{PermissionNames.TenantPermissionsRead}', '{PermissionScopeKind.Tenant}'),
                  ('Server Administrator', '{PermissionNames.TenantAuthorizationLogsRead}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantRead}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantSettingsRead}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantSettingsWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantUsersRead}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantUsersWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantUsersDelete}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantUserGroupsRead}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantUserGroupsWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.UserGroupAssignUsers}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantDeviceGroupsRead}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantDeviceGroupsWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.DeviceGroupAssignDevices}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantCustomersRead}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantCustomersWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantTagsWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantPermissionsRead}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantAuthorizationLogsRead}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantPermissionsWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.TenantPermissionsDeny}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.PersonalAccessTokenSelfRead}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.PersonalAccessTokenSelfWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.PersonalAccessTokenOthersRead}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.PersonalAccessTokenOthersWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.ServiceAccountRead}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.ServiceAccountWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.ServiceAccountRotateCredentials}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.InstallerKeyRead}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.InstallerKeyWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.InstallerKeyManageAll}', '{PermissionScopeKind.Tenant}'),
                  ('Tenant Administrator', '{PermissionNames.AgentInstall}', '{PermissionScopeKind.Tenant}'),
                  ('Installer Key Manager', '{PermissionNames.InstallerKeyRead}', '{PermissionScopeKind.Tenant}'),
                  ('Installer Key Manager', '{PermissionNames.InstallerKeyWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Installer Key Manager', '{PermissionNames.AgentInstall}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceRead}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceDelete}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceAliasWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceTagsRead}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceTagsWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceDesktopPreviewRead}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceOverviewRead}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceLogsRead}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceRemoteControlConnect}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceRemoteControlInteract}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceRemoteControlBlockInput}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceRemoteControlElevatedDesktop}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceCtrlAltDelSend}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceClipboardRead}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceClipboardWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceChatSend}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceFileSystemRead}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceFileSystemWrite}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceFileSystemDelete}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceFileSystemTransferUpload}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceFileSystemTransferDownload}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceTerminalUse}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceLogonTokenCreate}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceVncRelayConnect}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceWakeSend}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DevicePowerManage}', '{PermissionScopeKind.Tenant}'),
                  ('Device Superuser', '{PermissionNames.DeviceAgentUpdate}', '{PermissionScopeKind.Tenant}'),
                  ('Agent Installer', '{PermissionNames.AgentInstall}', '{PermissionScopeKind.Tenant}'),
                  ('Agent Installer', '{PermissionNames.InstallerKeyRead}', '{PermissionScopeKind.Tenant}'),
                  ('Agent Installer', '{PermissionNames.InstallerKeyWrite}', '{PermissionScopeKind.Tenant}')
              ) AS rp("RoleName", "PermissionName", "ScopeKind") ON r."Name" = rp."RoleName";
              """;
    migrationBuilder.Sql(backfillSql);

      // Backfill: grant every non-external user the self-service PAT baseline. Pre-permission
      // releases let any authenticated user manage their own tokens, and this preserves that.
      // External (guest) users are synthetic single-device identities, not interactive accounts.
      // NOT EXISTS collapses duplicates with the role backfill above (e.g. Tenant Administrator).
    var selfServiceBackfillSql = $"""
              INSERT INTO "PermissionAssignments"
                ("{nameof(PermissionAssignment.PrincipalKind)}", "{nameof(PermissionAssignment.PrincipalId)}", "{nameof(PermissionAssignment.PermissionName)}", "{nameof(PermissionAssignment.Effect)}", "{nameof(PermissionAssignment.ScopeKind)}", "{nameof(PermissionAssignment.ScopeId)}", "{nameof(PermissionAssignment.IsEnabled)}", "{nameof(PermissionAssignment.OwningTenantId)}", "{nameof(PermissionAssignment.CreatedByPrincipalType)}", "{nameof(PermissionAssignment.CreatedByPrincipalId)}")
              SELECT 'User', u."Id", p."PermissionName", 'Allow', 'Tenant', u."TenantId", true, u."TenantId", 'system', NULL
              FROM "AspNetUsers" u
              CROSS JOIN (VALUES
                ('{PermissionNames.PersonalAccessTokenSelfRead}'),
                ('{PermissionNames.PersonalAccessTokenSelfWrite}')) AS p("PermissionName")
              WHERE u."AccountType" <> 'ExternalUser'
                AND NOT EXISTS (
                  SELECT 1 FROM "PermissionAssignments" pa
                  WHERE pa."PrincipalKind" = 'User'
                    AND pa."PrincipalId" = u."Id"
                    AND pa."PermissionName" = p."PermissionName"
                    AND pa."ScopeKind" = 'Tenant'
                    AND pa."ScopeId" = u."TenantId"
                    AND pa."Effect" = 'Allow');
              """;
    migrationBuilder.Sql(selfServiceBackfillSql);

    migrationBuilder.DropTable(
        name: "AspNetRoleClaims");

    migrationBuilder.DropTable(
        name: "AspNetUserRoles");

    migrationBuilder.DropTable(
        name: "AspNetRoles");

    migrationBuilder.CreateTable(
        name: "UserGroups",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
          Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
          Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
          CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
          TenantId = table.Column<Guid>(type: "uuid", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_UserGroups", x => x.Id);
          table.ForeignKey(
                    name: "FK_UserGroups_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateTable(
        name: "DeviceGroupMembers",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
          DeviceGroupId = table.Column<Guid>(type: "uuid", nullable: false),
          DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
          CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_DeviceGroupMembers", x => x.Id);
          table.ForeignKey(
                    name: "FK_DeviceGroupMembers_DeviceGroups_DeviceGroupId",
                    column: x => x.DeviceGroupId,
                    principalTable: "DeviceGroups",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
          table.ForeignKey(
                    name: "FK_DeviceGroupMembers_Devices_DeviceId",
                    column: x => x.DeviceId,
                    principalTable: "Devices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateTable(
        name: "UserGroupMembers",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
          UserGroupId = table.Column<Guid>(type: "uuid", nullable: false),
          UserId = table.Column<Guid>(type: "uuid", nullable: false),
          CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_UserGroupMembers", x => x.Id);
          table.ForeignKey(
                    name: "FK_UserGroupMembers_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
          table.ForeignKey(
                    name: "FK_UserGroupMembers_UserGroups_UserGroupId",
                    column: x => x.UserGroupId,
                    principalTable: "UserGroups",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateIndex(
        name: "IX_Devices_CustomerId",
        table: "Devices",
        column: "CustomerId");

    migrationBuilder.CreateIndex(
        name: "IX_AuthorizationChangeLogs_CreatedAt",
        table: "AuthorizationChangeLogs",
        column: "CreatedAt");

    migrationBuilder.CreateIndex(
        name: "IX_AuthorizationChangeLogs_OwningTenantId",
        table: "AuthorizationChangeLogs",
        column: "OwningTenantId");

    migrationBuilder.CreateIndex(
        name: "IX_Customers_TenantId_Name",
        table: "Customers",
        columns: new[] { "TenantId", "Name" },
        unique: true);

    migrationBuilder.CreateIndex(
        name: "IX_DeviceGroupMembers_DeviceGroupId_DeviceId",
        table: "DeviceGroupMembers",
        columns: new[] { "DeviceGroupId", "DeviceId" },
        unique: true);

    migrationBuilder.CreateIndex(
        name: "IX_DeviceGroupMembers_DeviceId",
        table: "DeviceGroupMembers",
        column: "DeviceId");

    migrationBuilder.CreateIndex(
        name: "IX_DeviceGroups_TenantId_Name",
        table: "DeviceGroups",
        columns: new[] { "TenantId", "Name" },
        unique: true);

    migrationBuilder.CreateIndex(
        name: "IX_LogonTokens_DeviceId",
        table: "LogonTokens",
        column: "DeviceId");

    migrationBuilder.CreateIndex(
        name: "IX_LogonTokens_TenantId",
        table: "LogonTokens",
        column: "TenantId");

    migrationBuilder.CreateIndex(
        name: "IX_LogonTokens_Token",
        table: "LogonTokens",
        column: "Token",
        unique: true);

    migrationBuilder.CreateIndex(
        name: "IX_LogonTokens_UserId",
        table: "LogonTokens",
        column: "UserId");

    migrationBuilder.CreateIndex(
        name: "IX_PermissionAssignments_PrincipalKind_PrincipalId",
        table: "PermissionAssignments",
        columns: new[] { "PrincipalKind", "PrincipalId" });

    migrationBuilder.CreateIndex(
        name: "IX_PermissionAssignments_PrincipalKind_PrincipalId_PermissionN~",
        table: "PermissionAssignments",
        columns: new[] { "PrincipalKind", "PrincipalId", "PermissionName", "ScopeKind", "ScopeId", "Effect" },
        unique: true)
        .Annotation("Npgsql:NullsDistinct", false);

    migrationBuilder.CreateIndex(
        name: "IX_PermissionAssignments_ScopeKind_ScopeId",
        table: "PermissionAssignments",
        columns: new[] { "ScopeKind", "ScopeId" });

    migrationBuilder.CreateIndex(
        name: "IX_UserGroupMembers_UserGroupId_UserId",
        table: "UserGroupMembers",
        columns: new[] { "UserGroupId", "UserId" },
        unique: true);

    migrationBuilder.CreateIndex(
        name: "IX_UserGroupMembers_UserId",
        table: "UserGroupMembers",
        column: "UserId");

    migrationBuilder.CreateIndex(
        name: "IX_UserGroups_TenantId_Name",
        table: "UserGroups",
        columns: new[] { "TenantId", "Name" },
        unique: true);

    migrationBuilder.AddForeignKey(
        name: "FK_Devices_Customers_CustomerId",
        table: "Devices",
        column: "CustomerId",
        principalTable: "Customers",
        principalColumn: "Id",
        onDelete: ReferentialAction.SetNull);

    migrationBuilder.AddColumn<string>(
        name: "AllowedDesktopSessionIds",
        table: "LogonTokens",
        type: "jsonb",
        nullable: true);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropForeignKey(
        name: "FK_Devices_Customers_CustomerId",
        table: "Devices");

    migrationBuilder.DropTable(
        name: "AuthorizationChangeLogs");

    migrationBuilder.DropTable(
        name: "Customers");

    migrationBuilder.DropTable(
        name: "DeviceGroupMembers");

    migrationBuilder.DropTable(
        name: "LogonTokens");

    migrationBuilder.DropTable(
        name: "PermissionAssignments");

    migrationBuilder.DropTable(
        name: "UserGroupMembers");

    migrationBuilder.DropTable(
        name: "DeviceGroups");

    migrationBuilder.DropTable(
        name: "UserGroups");

    migrationBuilder.DropIndex(
        name: "IX_Devices_CustomerId",
        table: "Devices");

    migrationBuilder.DropColumn(
        name: "CreatedByUserId",
        table: "PersonalAccessTokens");

    migrationBuilder.DropColumn(
        name: "ExpiresAt",
        table: "PersonalAccessTokens");

    migrationBuilder.DropColumn(
        name: "RevokedAt",
        table: "PersonalAccessTokens");

    migrationBuilder.DropColumn(
        name: "CustomerId",
        table: "Devices");

    migrationBuilder.CreateTable(
        name: "AppUserTag",
        columns: table => new
        {
          TagsId = table.Column<Guid>(type: "uuid", nullable: false),
          UsersId = table.Column<Guid>(type: "uuid", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_AppUserTag", x => new { x.TagsId, x.UsersId });
          table.ForeignKey(
                    name: "FK_AppUserTag_AspNetUsers_UsersId",
                    column: x => x.UsersId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
          table.ForeignKey(
                    name: "FK_AppUserTag_Tags_TagsId",
                    column: x => x.TagsId,
                    principalTable: "Tags",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateTable(
        name: "AspNetRoles",
        columns: table => new
        {
          Id = table.Column<Guid>(type: "uuid", nullable: false),
          ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
          Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
          NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_AspNetRoles", x => x.Id);
        });

    migrationBuilder.CreateTable(
        name: "AspNetRoleClaims",
        columns: table => new
        {
          Id = table.Column<int>(type: "integer", nullable: false)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
          ClaimType = table.Column<string>(type: "text", nullable: true),
          ClaimValue = table.Column<string>(type: "text", nullable: true),
          RoleId = table.Column<Guid>(type: "uuid", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
          table.ForeignKey(
                    name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateTable(
        name: "AspNetUserRoles",
        columns: table => new
        {
          UserId = table.Column<Guid>(type: "uuid", nullable: false),
          RoleId = table.Column<Guid>(type: "uuid", nullable: false)
        },
        constraints: table =>
        {
          table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
          table.ForeignKey(
                    name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
          table.ForeignKey(
                    name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.InsertData(
        table: "AspNetRoles",
        columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
        values: new object[,]
        {
                  { new Guid("8ad85243-aa78-7539-0bf7-0cd6f27bcaa5"), "d6b798d2-a7f0-492b-a6ad-7eba9b1e3beb", "Server Administrator", "SERVER ADMINISTRATOR" },
                  { new Guid("963de2cb-fc55-43cd-11ac-dd6261c81bd8"), "a7e1a339-19c3-4d44-97e3-239636906a45", "Installer Key Manager", "INSTALLER KEY MANAGER" },
                  { new Guid("98aecfed-4095-42fd-e4b8-556d5b723bb6"), "0b692fe4-63e1-4a99-b021-4fc48ed81f4c", "Device Superuser", "DEVICE SUPERUSER" },
                  { new Guid("dde33610-89dc-e6a4-8d8a-33f3823a180e"), "ccfd2843-8a06-43d4-9bf3-6110b4e65900", "Agent Installer", "AGENT INSTALLER" },
                  { new Guid("ed0dddf2-c2b2-4160-9ece-4a9e03b2e828"), "b23bdf83-ecc8-4ca2-ba24-dc1780bfefc6", "Tenant Administrator", "TENANT ADMINISTRATOR" }
        });

    migrationBuilder.CreateIndex(
        name: "IX_AppUserTag_UsersId",
        table: "AppUserTag",
        column: "UsersId");

    migrationBuilder.CreateIndex(
        name: "IX_AspNetRoleClaims_RoleId",
        table: "AspNetRoleClaims",
        column: "RoleId");

    migrationBuilder.CreateIndex(
        name: "RoleNameIndex",
        table: "AspNetRoles",
        column: "NormalizedName",
        unique: true);

    migrationBuilder.CreateIndex(
        name: "IX_AspNetUserRoles_RoleId",
        table: "AspNetUserRoles",
        column: "RoleId");
  }
}
