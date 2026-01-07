namespace ControlR.Web.Client.ViewModels;

public class DeviceViewModel(DeviceResponseDto deviceDto) : IEquatable<DeviceViewModel>
{
  public DeviceResponseDto Dto => deviceDto;
  public Guid Id => deviceDto.Id;
  public bool IsVisible { get; set; }

  public bool Equals(DeviceViewModel? other)
  {
    return Id == other?.Id;
  }

  public override bool Equals(object? obj)
  {
    if (obj is DeviceViewModel other)
    {
      return Equals(other);
    }
    return false;
  }

  public override int GetHashCode()
  {
    return Id.GetHashCode();
  }
}
