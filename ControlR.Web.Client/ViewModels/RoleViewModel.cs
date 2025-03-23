namespace ControlR.Web.Client.ViewModels;

public class RoleViewModel(RoleResponseDto dto) : IHasPrimaryKey
{
  public Guid Id { get; } = dto.Id;
  public string Name { get; } = dto.Name;
  public ConcurrentHashSet<Guid> UserIds { get; } = [.. dto.UserIds];
}
