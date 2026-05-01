namespace ControlR.Web.Server.Services.DeviceManagement;

public enum DeviceAccessScopeKind
{
  None,
  TenantWide,
  SingleDevice,
  TaggedDevices
}

public sealed record DeviceAccessScope
{
  private DeviceAccessScope(
    DeviceAccessScopeKind kind,
    Guid? deviceId,
    IReadOnlyCollection<Guid>? tagIds)
  {
    Kind = kind;
    DeviceId = deviceId;
    TagIds = tagIds ?? [];
  }

  public Guid? DeviceId { get; }
  public DeviceAccessScopeKind Kind { get; }
  public IReadOnlyCollection<Guid> TagIds { get; }

  public static DeviceAccessScope None() => new(DeviceAccessScopeKind.None, null, []);

  public static DeviceAccessScope SingleDevice(Guid deviceId) =>
    new(DeviceAccessScopeKind.SingleDevice, deviceId, []);

  public static DeviceAccessScope TaggedDevices(IReadOnlyCollection<Guid> tagIds) =>
    new(DeviceAccessScopeKind.TaggedDevices, null, tagIds);

  public static DeviceAccessScope TenantWide() => new(DeviceAccessScopeKind.TenantWide, null, []);
}