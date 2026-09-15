namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Invites;

public class InvitesResponseDto
{
  public IReadOnlyList<InviteResponseDto> Items { get; set; } = [];
}
