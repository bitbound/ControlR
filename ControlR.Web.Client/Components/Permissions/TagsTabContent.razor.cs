using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ControlR.Web.Client.Components.Permissions;

public partial class TagsTabContent : ComponentBase
{
  private TagResponseDto? _selectedTag;
  private string? _newTagName;
  
  [Parameter]
  [EditorRequired]
  public required ConcurrentList<TagResponseDto> Tags { get; init; }
  
  [Parameter]
  public EventCallback OnTagsChanged { get; set; }
  
  [Inject]
  public required IControlrApi ControlrApi { get; init; }
  
  [Inject]
  public required ISnackbar Snackbar { get; init; }
  
  [Inject]
  public required IDialogService DialogService { get; init; }
  
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
    Tags.Add(createResult.Value);
    _newTagName = null;
    await OnTagsChanged.InvokeAsync();
  }

  private async Task DeleteSelectedTag()
  {
    if (_selectedTag is null)
    {
      return;
    }
    
    var result = await DialogService.ShowMessageBox("Confirm Deletion", "Are you sure you want to delete this tag?", "Yes", "No");
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
    
    Tags.Remove(_selectedTag);
    Snackbar.Add("Tag deleted", Severity.Success);
    await OnTagsChanged.InvokeAsync();
  }

  private async Task HandleNewTagKeyDown(KeyboardEventArgs args)
  {
    if (args.Key == "Enter")
    {
      await CreateTag();
    }
  }

  [MemberNotNullWhen(true, nameof(_newTagName))]
  private bool IsNewTagNameValid() => ValidateNewTagName(_newTagName) == null;
  
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

    if (Tags.Any(x => x.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase)))
    {
      return "Tag name already exists.";
    }

    return Validators.TagNameValidator().IsMatch(tagName)
      ? "Tag name can only contain lowercase letters, numbers, underscores, and hyphens."
      : null;
  }
}