using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControlR.Web.Server.Data.Migrations;

/// <inheritdoc />
public partial class RemoveOrphanedServerAlertsReadGrants : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    // server.alerts.read was removed from PermissionNames, PermissionCatalog, and
    // PermissionPresets when the server alert banner became visible to every signed-in user and
    // ViewerHub stopped gating the alerts group. Deployments that had already run
    // 20260813192551_Permissions_Phase2, which seeded the grant, keep rows naming a permission the
    // catalog no longer knows. Those rows are inert rather than dangerous, because
    // PermissionCatalog.Get returns null for an unknown name and every reader tolerates that. This
    // deletes them so the assignment tables stop listing a permission that cannot be granted.
    //
    // Deliberately narrow. A general "delete anything the catalog does not know" sweep would destroy
    // administrator grants during a version rollback, when a name is legitimately absent from the
    // running catalog but expected to return.
    migrationBuilder.Sql("""
              DELETE FROM "PermissionAssignments"
              WHERE "PermissionName" = 'server.alerts.read';
              """);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    // No-op. The migration does not capture which rows it deleted, so it cannot restore them, and
    // re-seeding a permission that no longer exists in the catalog would reintroduce exactly the
    // orphaned state this cleanup removes.
  }
}
