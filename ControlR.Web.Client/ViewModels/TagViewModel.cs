using TagsDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Tags;

namespace ControlR.Web.Client.ViewModels;

public class TagViewModel(TagsDtos.TagResponseDto dto)
{
  public ConcurrentHashSet<Guid> DeviceIds { get; } = [.. dto.DeviceIds];

  public Guid Id { get; } = dto.Id;

  public string Name { get; } = dto.Name;

  public TagType Type { get; set; } = dto.Type;
}