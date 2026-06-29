using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class Tenant : EntityBase
{
  public List<Device>? Devices { get; set; }
  public string? Name { get; set; }
  public List<Script>? Scripts { get; set; }
  public List<ScriptExecution>? ScriptExecutions { get; set; }
  public List<Tag>? Tags { get; set; }
  public List<TenantInvite>? TenantInvites { get; set; }
  public List<TenantSetting>? TenantSettings { get; set; }
  public List<AppUser>? Users { get; set; }
}
