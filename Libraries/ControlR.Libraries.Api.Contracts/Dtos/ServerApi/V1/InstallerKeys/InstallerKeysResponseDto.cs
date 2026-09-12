namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.InstallerKeys;

public class InstallerKeysResponseDto
{
  public IReadOnlyList<InstallerKeyDto> Items { get; set; } = [];
}
