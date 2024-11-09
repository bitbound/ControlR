using System.Diagnostics.CodeAnalysis;
using ControlR.Web.Client.Services.Stores;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ControlR.Web.Client.Components.Permissions;

public partial class TagsTabContent : ComponentBase
{
  private string? _newTagName;
  private TagResponseDto? _selectedTag;

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required ITagStore TagStore { get; init; }

  [Inject]
  public required ILogger<TagsTabContent> Logger { get; init; }

  [Inject]
  public required IUserStore UserStore { get; init; }

  private IOrderedEnumerable<TagResponseDto> SortedTags => TagStore.Items.OrderBy(x => x.Name);

  private async Task CreateTag()
  {
    if (string.IsNullOrWhiteSpace(_newTagName))
    {
      Snackbar.Add("Tag name is required", Severity.Error);
      return;
    }

    if (!IsNewTagNameValid())
    {
      return;
    }

    var createResult = await ControlrApi.CreateTag(_newTagName, TagType.Permission);
    if (!createResult.IsSuccess)
    {
      Snackbar.Add(createResult.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("Tag created", Severity.Success);
    TagStore.AddOrUpdate(createResult.Value);
    _newTagName = null;
  }

  private async Task DeleteSelectedTag()
  {
    if (_selectedTag is null)
    {
      return;
    }

    var result =
      await DialogService.ShowMessageBox("Confirm Deletion", "Are you sure you want to delete this tag?", "Yes", "No");
    if (!result.HasValue || !result.Value)
    {
      return;
    }

    var deleteResult = await ControlrApi.DeleteTag(_selectedTag.Id);
    if (!deleteResult.IsSuccess)
    {
      Snackbar.Add(deleteResult.Reason, Severity.Error);
      return;
    }

    TagStore.Remove(_selectedTag);
    Snackbar.Add("Tag deleted", Severity.Success);
  }

  private async Task HandleNewTagKeyDown(KeyboardEventArgs args)
  {
    if (args.Key == "Enter")
    {
      await CreateTag();
    }
  }

  [MemberNotNullWhen(true, nameof(_newTagName))]
  private bool IsNewTagNameValid()
  {
    return ValidateNewTagName(_newTagName) == null;
  }

  private async Task SetTag(bool isToggled, TagResponseDto tag, Guid userId)
  {
    try
    {
      await Task.Yield();

      Snackbar.Add(isToggled
        ? "Tag added"
        : "Tag removed", Severity.Success);

    }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Error while setting tag.");
      Snackbar.Add("An error occurred while setting tag", Severity.Error);
    }
  }

  private string? ValidateNewTagName(string? tagName)
  {
    if (string.IsNullOrWhiteSpace(tagName))
    {
      return null;
    }

    if (tagName.Length > 100)
    {
      return "Tag name must be 100 characters or less.";
    }

    if (SortedTags.Any(x => x.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase)))
    {
      return "Tag name already exists.";
    }

    return Validators.TagNameValidator().IsMatch(tagName)
      ? "Tag name can only contain lowercase letters, numbers, underscores, and hyphens."
      : null;
  }
}