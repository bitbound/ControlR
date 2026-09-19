using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

namespace ControlR.Web.Client.Helpers;

/// <summary>
/// Groups the permission catalog for display. Each entry carries the category label the server
/// assigned to its permission, so nothing here parses a permission name.
/// </summary>
internal static class PermissionGrouping
{
  /// <summary>
  /// Groups catalog entries by category label, groups ordered by label and entries ordered by
  /// <see cref="PermissionCatalogEntryDto.Name"/> within a group. Empty groups cannot occur because a
  /// group only exists once it has an entry, so callers that filter the entries first get empty-group
  /// hiding for free.
  /// </summary>
  internal static IReadOnlyList<PermissionGroup> GroupForDisplay(IEnumerable<PermissionCatalogEntryDto> entries) =>
    [
      .. entries
        .GroupBy(entry => entry.CategoryLabel, StringComparer.OrdinalIgnoreCase)
        .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
        .Select(group => new PermissionGroup(
          group.Key,
          [.. group.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)]))
    ];
}
