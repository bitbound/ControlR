using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

namespace ControlR.Web.Client.ViewModels;

/// <summary>
/// One header row and the permission catalog entries that belong under it.
/// </summary>
/// <param name="Label">The category label the server supplied, shown as the header.</param>
/// <param name="Entries">The entries in the category, ordered by name.</param>
internal readonly record struct PermissionGroup(
  string Label,
  IReadOnlyList<PermissionCatalogEntryDto> Entries);
