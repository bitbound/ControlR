using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using ControlR.Web.Client.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ControlR.Web.Client.Components.Permissions;

public partial class TagsTabContent : ComponentBase, IDisposable
{
  private ImmutableArray<IDisposable>? _changeHandlers;
  private string _deviceSearchPattern = string.Empty;
  private string? _newTagName;
  private TagViewModel? _selectedTag;
  private string _tagSearchPattern = string.Empty;
  private string _userSearchPattern = string.Empty;

  [Inject]
  public required IControlrApi ControlrApi { get; init; }

  [Inject]
  public required IDeviceStore DeviceStore { get; init; }

  [Inject]
  public required IDialogService DialogService { get; init; }

  [Inject]
  public required ILogger<TagsTabContent> Logger { get; init; }

  [Inject]
  public required ISnackbar Snackbar { get; init; }

  [Inject]
  public required IAdminTagStore TagStore { get; init; }

  [Inject]
  public required IUserStore UserStore { get; init; }

  private IOrderedEnumerable<DeviceDto> FilteredDevices =>
    DeviceStore.Items
      .Where(x => x.Name.Contains(_deviceSearchPattern, StringComparison.OrdinalIgnoreCase))
      .OrderBy(x => x.Name);

  private IOrderedEnumerable<TagViewModel> FilteredTags =>
    TagStore.Items
      .Where(x => x.Name.Contains(_tagSearchPattern, StringComparison.OrdinalIgnoreCase))
      .OrderBy(x => x.Name);

  private IOrderedEnumerable<UserResponseDto> FilteredUsers =>
    UserStore.Items
      .Where(x => x.UserName?.Contains(_userSearchPattern, StringComparison.OrdinalIgnoreCase) == true)
      .OrderBy(x => x.UserName);

  public void Dispose()
  {
    _changeHandlers?.DisposeAll();
    GC.SuppressFinalize(this);
  }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();
    _changeHandlers =
    [
      TagStore.RegisterChangeHandler(this, async () => await InvokeAsync(StateHasChanged)),
      UserStore.RegisterChangeHandler(this, async () => await InvokeAsync(StateHasChanged)),
      DeviceStore.RegisterChangeHandler(this, async () => await InvokeAsync(StateHasChanged))
    ];
  }

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
    await TagStore.AddOrUpdate(new TagViewModel(createResult.Value));
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

    await TagStore.Remove(_selectedTag.Id);
    Snackbar.Add("Tag deleted", Severity.Success);
  }
  private async Task RenameSelectedTag()
  {
    if (_selectedTag is null)
    {
      return;
    }

    var response = await DialogService.ShowPrompt(
      "Rename Tag",
      $"Rename the {_selectedTag.Name} tag.",
      "Enter a new tag name.");

    if (string.IsNullOrWhiteSpace(response))
    {
      Logger.LogInformation("Tag renamed cancelled.");
      return;
    }

    if (ValidateNewTagName(response) is string { Length: > 0 } error)
    {
      Snackbar.Add(error, Severity.Error);
      return;
    }

    var renameResult = await ControlrApi.RenameTag(_selectedTag.Id, response);
    if (!renameResult.IsSuccess)
    {
      Snackbar.Add(renameResult.Reason, Severity.Error);
      return;
    }

    await TagStore.AddOrUpdate(new TagViewModel(renameResult.Value));
    Snackbar.Add("Tag renamed", Severity.Success);
  }

  private async Task HandleDeviceToggled((DeviceDto device, bool isToggled) args)
  {
    if (_selectedTag is null)
    {
      Snackbar.Add("No tag selected", Severity.Error);
      return;
    }
    
    await SetDeviceTag(args.isToggled, _selectedTag, args.device.Id);
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

  private async Task SetDeviceTag(bool isToggled, TagViewModel tag, Guid deviceId)
  {
    try
    {
      if (isToggled)
      {
        var addResult = await ControlrApi.AddDeviceTag(deviceId, tag.Id);
        if (!addResult.IsSuccess)
        {
          Snackbar.Add(addResult.Reason, Severity.Error);
          return;
        }
        tag.DeviceIds.Add(deviceId);
      }
      else
      {
        var removeResult = await ControlrApi.RemoveDeviceTag(deviceId, tag.Id);
        if (!removeResult.IsSuccess)
        {
          Snackbar.Add(removeResult.Reason, Severity.Error);
          return;
        }
        tag.DeviceIds.Remove(deviceId);
      }

      await TagStore.InvokeItemsChanged();

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

  private async Task SetUserTag(bool isToggled, TagViewModel tag, Guid userId)
  {
    try
    {
      if (isToggled)
      {
        var addResult = await ControlrApi.AddUserTag(userId, tag.Id);
        if (!addResult.IsSuccess)
        {
          Snackbar.Add(addResult.Reason, Severity.Error);
          return;
        }
        tag.UserIds.Add(userId);
      }
      else
      {
        var removeResult = await ControlrApi.RemoveUserTag(userId, tag.Id);
        if (!removeResult.IsSuccess)
        {
          Snackbar.Add(removeResult.Reason, Severity.Error);
          return;
        }
        tag.UserIds.Remove(userId);
      }

      await TagStore.InvokeItemsChanged();

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

    if (tagName.Length > 50)
    {
      return "Tag name must be 50 characters or less.";
    }

    if (FilteredTags.Any(x => x.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase)))
    {
      return "Tag name already exists.";
    }

    return Validators.TagNameValidator().IsMatch(tagName)
      ? "Tag name can only contain lowercase letters, numbers, underscores, and hyphens."
      : null;
  }
}