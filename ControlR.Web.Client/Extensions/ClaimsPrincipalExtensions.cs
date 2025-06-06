﻿using ControlR.Web.Client.Authz;
using System.Security.Claims;

namespace ControlR.Web.Client.Extensions;

public static class ClaimsPrincipalExtensions
{
  public static bool IsAuthenticated(this ClaimsPrincipal user)
  {
    return user.Identity?.IsAuthenticated ?? false;
  }

  public static bool TryGetTenantId(
    this ClaimsPrincipal user,
    out Guid tenantId)
  {
    tenantId = Guid.Empty;
    if (!user.IsAuthenticated())
    {
      return false;
    }

    var tenantClaim = user.FindFirst(UserClaimTypes.TenantId);
    if (!Guid.TryParse(tenantClaim?.Value, out var id))
    {
      return false;
    }

    tenantId = id;
    return true;
  }

  public static bool TryGetUserId(
    this ClaimsPrincipal user,
    out Guid userId)
  {
    userId = Guid.Empty;
    if (!user.IsAuthenticated())
    {
      return false;
    }

    var userIdClaim = user.FindFirst(UserClaimTypes.UserId);
    if (!Guid.TryParse(userIdClaim?.Value, out var id))
    {
      return false;
    }

    userId = id;
    return true;
  }
}