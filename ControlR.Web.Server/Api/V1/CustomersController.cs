using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Customers;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Primitives;
using Microsoft.AspNetCore.Mvc;
using ControlR.Web.Server.Constants;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Customer management. Every operation is tenant-scoped by the required tenantId query
/// parameter.
/// </summary>
[Route(HttpConstants.V1.CustomersEndpoint)]
[ApiController]
[Authorize]
[ApiVersion(ApiVersions.V1)]
public class CustomersController(ICustomerManager customerManager) : ControllerBase
{
  private readonly ICustomerManager _customerManager = customerManager;

  [HttpPost("{customerId:guid}/devices")]
  [Authorize(Policy = PolicyNames.RequireCustomersWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  public async Task<IActionResult> AssignDevices(
    [FromRoute] Guid customerId,
    [FromQuery] Guid tenantId,
    [FromBody] AssignCustomerDevicesRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Problem(
        detail: "User ID not found.",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    var result = await _customerManager.AssignDevices(
      customerId, request.DeviceIds, request.RemoveDeviceIds, resolvedTenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return NoContent();
  }

  [HttpPost]
  [Authorize(Policy = PolicyNames.RequireCustomersWrite)]
  [ProducesResponseType<CustomerDto>(StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  public async Task<ActionResult<CustomerDto>> Create(
    [FromQuery] Guid tenantId,
    [FromBody] CreateCustomerRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Problem(
        detail: "User ID not found.",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    var result = await _customerManager.Create(
      request.Name, request.Description, request.Notes, resolvedTenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return CreatedAtAction(
      nameof(Get),
      new { customerId = result.Value.Id, tenantId = resolvedTenantId },
      ToV1Dto(result.Value));
  }

  [HttpDelete("{customerId:guid}")]
  [Authorize(Policy = PolicyNames.RequireCustomersWrite)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  public async Task<IActionResult> Delete(
    [FromRoute] Guid customerId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Problem(
        detail: "User ID not found.",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    var result = await _customerManager.Delete(customerId, resolvedTenantId, actor, cancellationToken);
    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return NoContent();
  }

  [HttpGet("{customerId:guid}")]
  [Authorize(Policy = PolicyNames.RequireCustomersRead)]
  [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  public async Task<ActionResult<CustomerDto>> Get(
    [FromRoute] Guid customerId,
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var result = await _customerManager.Get(customerId, resolvedTenantId, cancellationToken);
    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return Ok(ToV1Dto(result.Value));
  }

  [HttpGet]
  [Authorize(Policy = PolicyNames.RequireCustomersRead)]
  [ProducesResponseType<CustomersResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  public async Task<ActionResult<CustomersResponseDto>> GetAll(
    [FromQuery] Guid tenantId,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    var customers = await _customerManager.GetAll(resolvedTenantId, cancellationToken);

    return Ok(new CustomersResponseDto
    {
      Items = [.. customers.Select(ToV1Dto)]
    });
  }

  [HttpPut("{customerId:guid}")]
  [Authorize(Policy = PolicyNames.RequireCustomersWrite)]
  [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
  public async Task<ActionResult<CustomerDto>> Update(
    [FromRoute] Guid customerId,
    [FromQuery] Guid tenantId,
    [FromBody] UpdateCustomerRequestDto request,
    CancellationToken cancellationToken)
  {
    if (!User.TryResolveTenantId(tenantId, out var resolvedTenantId))
    {
      return Forbid();
    }

    if (User.ToPrincipalDescriptor() is not { } actor)
    {
      return Problem(
        detail: "User ID not found.",
        statusCode: StatusCodes.Status400BadRequest,
        title: V1ProblemTitles.InvalidRequest);
    }

    var result = await _customerManager.Update(
      customerId, request.Name, request.Description, request.Notes, resolvedTenantId, actor, cancellationToken);

    if (!result.IsSuccess)
    {
      return ToV1Failure(result);
    }

    return Ok(ToV1Dto(result.Value));
  }

  private static CustomerDto ToV1Dto(InternalDtos.CustomerDto source)
  {
    return new CustomerDto(
      source.Id,
      source.Name,
      source.Description,
      source.Notes,
      source.CreatedAt,
      source.DeviceCount);
  }

  // The manager never returns Forbidden today (cross-tenant ids surface as NotFound via the
  // explicit TenantId predicates), but keep the collapse so a future Forbidden cannot act as
  // an existence oracle against other tenants' customers.
  private static ActionResult ToV1Failure<T>(HttpResult<T> result) =>
    ToV1Failure(result.ToHttpResult());

  private static ActionResult ToV1Failure(HttpResult result)
  {
    return result.ErrorCode == HttpResultErrorCode.Forbidden
      ? new NotFoundResult()
      : result.ToActionResult();
  }
}
