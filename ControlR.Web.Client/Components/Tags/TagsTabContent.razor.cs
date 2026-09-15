using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using ControlR.Web.Client.DataValidation;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceTags;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Tags;

namespace ControlR.Web.Client.Components.Tags;

public partial class TagsTabContent : ComponentBase, IDisposable
{
  private ImmutableArray<IDisposable>? _changeHandlers;
  private string? _newTagName;
  private TagViewModel? _selectedTag;
  private string _tagSearchPattern = string.Empty;

  [Inject]
  public required AuthenticationStateProvider AuthState { get; init; }

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
  public required ITagStore TagStore { get; init; }

  private IOrderedEnumerable<TagViewModel> FilteredTags =>
    TagStore.Items
      .Where(x => x.Name.Contains(_tagSearchPattern, StringComparison.OrdinalIgnoreCase))
      .OrderBy(x => x.Name);

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

    if (await AuthState.GetTenantId(Snackbar) is not { } tenantId)
    {
      return;
    }

    var createRequest = new TagCreateRequestDto(_newTagName, TagType.Permission);
    var createResult = await ControlrApi.V1.Tags.CreateTag(tenantId, createRequest);
    if (!createResult.IsSuccess)
    {
      Snackbar.Add(createResult.Reason, Severity.Error);
      return;
    }

    Snackbar.Add("Tag created", Severity.Success);
    var vm = new TagViewModel(createResult.Value);
    await TagStore.AddOrUpdate(vm);
    _newTagName = null;
    await InvokeAsync(StateHasChanged);
  }

  private async Task DeleteSelectedTag()
  {
    if (_selectedTag is null)
    {
      return;
    }

    var result = await DialogService.ShowMessageBoxAsync(
        "Confirm Deletion",
        "Are you sure you want to delete this tag?", "Yes", "No");

    if (!result.HasValue || !result.Value)
    {
      return;
    }

    if (await AuthState.GetTenantId(Snackbar) is not { } tenantId)
    {
      return;
    }

    var deleteResult = await ControlrApi.V1.Tags.DeleteTag(_selectedTag.Id, tenantId);
    if (!deleteResult.IsSuccess)
    {
      Snackbar.Add(deleteResult.Reason, Severity.Error);
      return;
    }

    await TagStore.Remove(_selectedTag.Id);
    _selectedTag = null;
    Snackbar.Add("Tag deleted", Severity.Success);
  }

  private async Task HandleDeviceToggled((InternalDtos.DeviceResponseDto device, bool isToggled) args)
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

    if (await AuthState.GetTenantId(Snackbar) is not { } tenantId)
    {
      return;
    }

    var renameResult = await ControlrApi.V1.Tags.UpdateTag(
      _selectedTag.Id, tenantId, new UpdateTagRequestDto(response));
    if (!renameResult.IsSuccess)
    {
      Snackbar.Add(renameResult.Reason, Severity.Error);
      return;
    }

    var vm = new TagViewModel(renameResult.Value);
    await TagStore.AddOrUpdate(vm);
    Snackbar.Add("Tag renamed", Severity.Success);
  }

  private async Task SetDeviceTag(bool isToggled, TagViewModel tag, Guid deviceId)
  {
    try
    {
      if (await AuthState.GetTenantId(Snackbar) is not { } tenantId)
      {
        return;
      }

      if (isToggled)
      {
        var addRequest = new DeviceTagAddRequestDto(deviceId, tag.Id);
        var addResult = await ControlrApi.V1.DeviceTags.AddDeviceTag(tenantId, addRequest);
        if (!addResult.IsSuccess)
        {
          Snackbar.Add(addResult.Reason, Severity.Error);
          return;
        }
        tag.DeviceIds.Add(deviceId);
      }
      else
      {
        var removeResult = await ControlrApi.V1.DeviceTags.RemoveDeviceTag(deviceId, tag.Id, tenantId);
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
