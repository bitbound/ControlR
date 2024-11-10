namespace ControlR.Web.Client.ViewModels;

public class TagViewModel(TagResponseDto dto) : IHasPrimaryKey
{
  public ConcurrentHashSet<Guid> DeviceIds { get; } = new(dto.DeviceIds);

  public Guid Id { get; } = dto.Id;

  public string Name { get; } = dto.Name;

  public TagType Type { get; set; } = dto.Type;

  public ConcurrentHashSet<Guid> UserIds { get; } = new(dto.UserIds);
}