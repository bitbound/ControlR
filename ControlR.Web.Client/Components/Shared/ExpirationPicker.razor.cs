namespace ControlR.Web.Client.Components.Shared;

public partial class ExpirationPicker : ComponentBase
{
  private const int CustomValue = -1;
  private const int NeverValue = 0;

  private static readonly int[] _sourceArray = [30, 90, 365];

  private int _choice = NeverValue;
  private DateTime? _customDate;
  private TimeSpan? _customTime;
  private DateTimeOffset? _lastValue;

  [Inject]
  public required TimeProvider TimeProvider { get; init; }

  [Parameter]
  public DateTimeOffset? Value { get; set; }

  [Parameter]
  public EventCallback<DateTimeOffset?> ValueChanged { get; set; }

  private DateTime MinDate => TimeProvider.GetLocalNow().Date;

  protected override void OnParametersSet()
  {
    base.OnParametersSet();

    // Only resync when Value changes to something we did not just emit, so we
    // don't clobber an in-progress custom date/time edit on unrelated re-renders.
    if (_lastValue == Value)
    {
      return;
    }

    _lastValue = Value;
    ApplyValue(Value);
  }

  private void ApplyValue(DateTimeOffset? value)
  {
    if (value is null)
    {
      _choice = NeverValue;
      _customDate = null;
      _customTime = null;
      return;
    }

    // Reflect a pre-seeded expiration in the dropdown. Match the closest preset
    // (30/90/365 days) for the common case; otherwise fall back to the custom picker.
    var expiresIn = value.Value - TimeProvider.GetUtcNow();
    var presetDays = _sourceArray.FirstOrDefault(d =>
      (expiresIn - TimeSpan.FromDays(d)).Duration() <= TimeSpan.FromDays(1));

    if (presetDays != 0)
    {
      _choice = presetDays;
      _customDate = null;
      _customTime = null;
      return;
    }

    _choice = CustomValue;
    var local = value.Value.ToLocalTime();
    _customDate = local.Date;
    _customTime = local.TimeOfDay;
  }

  private DateTimeOffset BuildCustomValue()
  {
    var date = _customDate ?? TimeProvider.GetLocalNow().Date;
    var local = date.Date + (_customTime ?? TimeSpan.Zero);
    var offset = TimeProvider.LocalTimeZone.GetUtcOffset(local);
    return new DateTimeOffset(local, offset);
  }

  private async Task OnChoiceChanged(int choice)
  {
    _choice = choice;

    switch (choice)
    {
      case NeverValue:
        await ValueChanged.InvokeAsync(null);
        break;
      case CustomValue:
        _customDate ??= TimeProvider.GetLocalNow().Date;
        _customTime ??= TimeProvider.GetLocalNow().TimeOfDay;
        await ValueChanged.InvokeAsync(BuildCustomValue());
        break;
      default:
        await ValueChanged.InvokeAsync(TimeProvider.GetUtcNow().AddDays(choice));
        break;
    }
  }

  private async Task OnDateChanged(DateTime? date)
  {
    _customDate = date;
    if (_customDate is not null)
    {
      await ValueChanged.InvokeAsync(BuildCustomValue());
    }
  }

  private async Task OnTimeChanged(TimeSpan? time)
  {
    _customTime = time;
    if (_customDate is not null)
    {
      await ValueChanged.InvokeAsync(BuildCustomValue());
    }
  }
}
