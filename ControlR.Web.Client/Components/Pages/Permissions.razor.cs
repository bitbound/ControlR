﻿using Microsoft.AspNetCore.Components;

namespace ControlR.Web.Client.Components.Pages;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class Permissions : ComponentBase
{
  private readonly ConcurrentList<TagResponseDto> _tags = [];

  [Inject]
  public required IControlrApi ControlrApi { get; init; }
  
  [Inject]
  public required ISnackbar Snackbar { get; init; }

  protected override async Task OnInitializedAsync()
  {
    await base.OnInitializedAsync();

    var tagResult = await ControlrApi.GetAllTags(true);
    if (!tagResult.IsSuccess)
    {
      Snackbar.Add("Failed to load tags", Severity.Error);
      return;
    }
    _tags.AddRange(tagResult.Value);
  }

  private async Task HandleTagsChanged() => await InvokeAsync(StateHasChanged);
}