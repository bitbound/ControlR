using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

namespace ControlR.Web.Client.ViewModels;

/// <summary>
/// A row in the permission-picker dropdown: either a non-selectable family header or a selectable
/// catalog entry. MudBlazor's <see cref="MudAutocomplete{T}"/> has no group API, so headers ride in
/// the same item list and are marked disabled, which keeps them out of click, focus, and arrow
/// navigation. It is a record so the autocomplete's value comparison works on the rebuilt rows.
/// </summary>
internal sealed record PermissionPickerRow
{
  public required string Display { get; init; }

  public required bool IsHeader { get; init; }

  /// <summary>
  /// The catalog entry for a selectable row; <see langword="null"/> for a header.
  /// </summary>
  public PermissionCatalogEntryDto? Entry { get; init; }

  public static PermissionPickerRow Header(string label) => new()
  {
    Display = label,
    IsHeader = true,
  };

  public static PermissionPickerRow ForEntry(string display, PermissionCatalogEntryDto entry) => new()
  {
    Display = display,
    IsHeader = false,
    Entry = entry,
  };
}
