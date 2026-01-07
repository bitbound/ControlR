namespace ControlR.Web.Client.ViewModels;

public class DeviceViewModel(
  DeviceDto deviceDto,
  bool isOutdated) : IEquatable<DeviceViewModel>
{
  public DeviceDto Dto => deviceDto;
  public Guid Id => deviceDto.Id;
  public bool IsOutdated => isOutdated;
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
