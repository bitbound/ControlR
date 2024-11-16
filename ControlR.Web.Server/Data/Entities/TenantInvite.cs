using ControlR.Web.Server.Data.Entities.Bases;
using System.ComponentModel.DataAnnotations;

namespace ControlR.Web.Server.Data.Entities;

public class TenantInvite : TenantEntityBase
{
  [Required]
  public required string ActivationCode { get; set; }

  [EmailAddress]
  public required string InviteeEmail { get; set; }

}
