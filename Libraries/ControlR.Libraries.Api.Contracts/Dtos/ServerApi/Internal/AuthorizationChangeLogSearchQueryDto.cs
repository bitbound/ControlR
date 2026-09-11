using System.ComponentModel.DataAnnotations;

using ControlR.Libraries.Api.Contracts.Constants;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public class AuthorizationChangeLogSearchQueryDto
{
  public string? ActionType { get; set; }

  public string? ActorType { get; set; }

  public DateTimeOffset? From { get; set; }

  public int Page { get; set; }

  public int PageSize { get; set; } = 50;

  [MaxLength(DtoLimits.AuthorizationChangeLogSearchTextMaxLength)]
  public string? SearchText { get; set; }

  public string? TargetType { get; set; }

  public Guid? TenantId { get; set; }

  public DateTimeOffset? To { get; set; }
}
