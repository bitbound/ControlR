using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.Libraries.Avalonia.Controls.Dialogs;
using ControlR.Libraries.Avalonia.ViewModels;
using ControlR.Viewer.Avalonia.Views.Dialogs;
using System.Linq;

namespace ControlR.Viewer.Avalonia.ViewModels.Dialogs;

public interface ITextPromptDialogViewModel : IDisposable, IViewReference<TextPromptDialogView>
{
  IRelayCommand CancelCommand { get; }
  string CancelText { get; }
  bool CanSubmit { get; }
  Task<string?> Completion { get; }
  bool HasValidationError { get; }
  string InputHint { get; }
  string InputLabel { get; }
  string InputText { get; set; }
  IRelayCommand SubmitCommand { get; }
  string SubmitText { get; }
  string Subtitle { get; }
  string ValidationMessage { get; }
}

internal sealed partial class TextPromptDialogViewModel(
  IDialogProvider dialogProvider,
  string subtitle,
  string inputLabel,
  string inputHint,
  string submitText,
  string cancelText) : ViewModelBase<TextPromptDialogView>, ITextPromptDialogViewModel
{
  private readonly IDialogProvider _dialogProvider = dialogProvider;
  private readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars().Union(['/', '\\']).Distinct().ToArray();
  private readonly TaskCompletionSource<string?> _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

  public string CancelText { get; } = cancelText;
  public bool CanSubmit => !HasValidationError && !string.IsNullOrWhiteSpace(InputText);
  public Task<string?> Completion => _taskCompletionSource.Task;
  public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);
  public string InputHint { get; } = inputHint;
  public string InputLabel { get; } = inputLabel;
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(CanSubmit), nameof(HasValidationError))]
  public partial string InputText { get; set; } = string.Empty;
  public string SubmitText { get; } = submitText;
  public string Subtitle { get; } = subtitle;
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(CanSubmit), nameof(HasValidationError))]
  public partial string ValidationMessage { get; set; } = string.Empty;

  [RelayCommand]
  private void Cancel()
  {
    _taskCompletionSource.TrySetResult(null);
    _dialogProvider.Close();
  }

  private string GetValidationMessage(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return Assets.Resources.FileSystem_NewFolderValidationRequired;
    }

    var invalidCharacter = value.FirstOrDefault(character => _invalidFileNameChars.Contains(character));
    if (invalidCharacter != default)
    {
      return string.Format(Assets.Resources.FileSystem_NewFolderValidationInvalidCharacter, invalidCharacter);
    }

    if (value.EndsWith(' ') || value.EndsWith('.'))
    {
      return Assets.Resources.FileSystem_NewFolderValidationTrailingCharacter;
    }

    return string.Empty;
  }

  partial void OnInputTextChanged(string value)
  {
    ValidationMessage = GetValidationMessage(value);
    SubmitCommand.NotifyCanExecuteChanged();
  }

  [RelayCommand(CanExecute = nameof(CanSubmit))]
  private void Submit()
  {
    if (!CanSubmit)
    {
      return;
    }

    _taskCompletionSource.TrySetResult(InputText.Trim());
    _dialogProvider.Close();
  }
}