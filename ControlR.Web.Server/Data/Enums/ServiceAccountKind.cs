namespace ControlR.Web.Server.Data.Enums;

/// <summary>
/// Distinguishes server-scoped (global) service accounts from tenant-scoped service accounts.
/// Stored as a human-readable string in the database via <see cref="ServiceAccountKind"/>
/// EF conversion so that raw DB inspection of the <c>ServiceAccounts</c> table is readable.
/// </summary>
public enum ServiceAccountKind
{
  Tenant,
  Server
}