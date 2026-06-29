using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Hubs;
using ControlR.Web.Server.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ControlR.Web.Server.Api;

[Route(HttpConstants.ScriptsEndpoint)]
[ApiController]
[Authorize]
public class ScriptsController : ControllerBase
{
  [HttpPost]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<ScriptDto>> CreateScript(
    [FromServices] AppDb appDb,
    [FromBody] ScriptCreateRequestDto dto)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return NotFound("User tenant not found.");
    }

    var script = new Script
    {
      TenantId = tenantId,
      Name = dto.Name,
      Description = dto.Description,
      CodeContent = dto.CodeContent,
      ShellType = dto.ShellType,
      TimeoutSeconds = dto.TimeoutSeconds
    };

    await appDb.Scripts.AddAsync(script);
    await appDb.SaveChangesAsync();

    return Ok(script.ToDto());
  }

  [HttpGet]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<ScriptDto[]>> GetAllScripts([FromServices] AppDb appDb)
  {
    var scripts = await appDb.Scripts
      .AsNoTracking()
      .OrderBy(x => x.Name)
      .ToListAsync();

    return Ok(scripts.Select(x => x.ToDto()).ToArray());
  }

  [HttpGet("{id:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<ScriptDto>> GetScript(
    [FromServices] AppDb appDb,
    [FromRoute] Guid id)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return NotFound("User tenant not found.");
    }

    var script = await appDb.Scripts
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);

    if (script is null)
    {
      return NotFound();
    }

    return Ok(script.ToDto());
  }

  [HttpPut("{id:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<ScriptDto>> UpdateScript(
    [FromServices] AppDb appDb,
    [FromRoute] Guid id,
    [FromBody] ScriptCreateRequestDto dto)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return NotFound("User tenant not found.");
    }

    var script = await appDb.Scripts
      .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);

    if (script is null)
    {
      return NotFound();
    }

    script.Name = dto.Name;
    script.Description = dto.Description;
    script.CodeContent = dto.CodeContent;
    script.ShellType = dto.ShellType;
    script.TimeoutSeconds = dto.TimeoutSeconds;

    await appDb.SaveChangesAsync();

    return Ok(script.ToDto());
  }

  [HttpDelete("{id:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult> DeleteScript(
    [FromServices] AppDb appDb,
    [FromRoute] Guid id)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return NotFound("User tenant not found.");
    }

    var script = await appDb.Scripts
      .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);

    if (script is null)
    {
      return NotFound();
    }

    appDb.Scripts.Remove(script);
    await appDb.SaveChangesAsync();

    return NoContent();
  }

  [HttpPost("{id:guid}/execute")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<ScriptExecutionDto[]>> ExecuteScript(
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] TimeProvider timeProvider,
    [FromRoute] Guid id,
    [FromBody] Guid[] deviceIds,
    [FromQuery] ScriptRunAs runAs = ScriptRunAs.System)
  {
    if (!User.TryGetTenantId(out var tenantId) || !User.TryGetUserId(out var userId))
    {
      return NotFound("User context not found.");
    }

    var script = await appDb.Scripts
      .AsNoTracking()
      .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);

    if (script is null)
    {
      return NotFound("Script not found.");
    }

    var executions = new List<ScriptExecutionDto>();

    foreach (var deviceId in deviceIds)
    {
      var device = await appDb.Devices
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == deviceId && x.TenantId == tenantId);

      if (device is null)
      {
        continue;
      }

      var execution = new ScriptExecution
      {
        TenantId = tenantId,
        ScriptId = script.Id,
        DeviceId = deviceId,
        ExecutedByUserId = userId.ToString(),
        StartedAt = timeProvider.GetLocalNow(),
        Status = device.IsOnline ? ScriptStatus.Running : ScriptStatus.Offline,
        StdOut = string.Empty,
        StdErr = device.IsOnline ? string.Empty : "Device is offline."
      };

      await appDb.ScriptExecutions.AddAsync(execution);
      await appDb.SaveChangesAsync();

      // Fetch with relations for returning DTO
      var savedExecution = await appDb.ScriptExecutions
        .Include(x => x.Script)
        .Include(x => x.Device)
        .FirstAsync(x => x.Id == execution.Id);

      executions.Add(savedExecution.ToDto());

      if (device.IsOnline && !string.IsNullOrEmpty(device.ConnectionId))
      {
        // Fire and forget script invocation on agent via SignalR
        _ = agentHub.Clients.Client(device.ConnectionId)
          .ExecuteScript(execution.Id, script.CodeContent, script.ShellType, runAs);
      }
    }

    return Ok(executions.ToArray());
  }

  [HttpPost("execute-adhoc")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<ScriptExecutionDto[]>> ExecuteAdHocScript(
    [FromServices] AppDb appDb,
    [FromServices] IHubContext<AgentHub, IAgentHubClient> agentHub,
    [FromServices] TimeProvider timeProvider,
    [FromBody] ExecuteScriptRequestDto request)
  {
    if (!User.TryGetTenantId(out var tenantId) || !User.TryGetUserId(out var userId))
    {
      return NotFound("User context not found.");
    }

    if (string.IsNullOrWhiteSpace(request.AdHocScriptContent) || !request.ShellType.HasValue)
    {
      return BadRequest("Script content and shell type are required.");
    }

    var executions = new List<ScriptExecutionDto>();

    foreach (var deviceId in request.DeviceIds)
    {
      var device = await appDb.Devices
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == deviceId && x.TenantId == tenantId);

      if (device is null)
      {
        continue;
      }

      var execution = new ScriptExecution
      {
        TenantId = tenantId,
        ScriptId = null,
        DeviceId = deviceId,
        ExecutedByUserId = userId.ToString(),
        StartedAt = timeProvider.GetLocalNow(),
        Status = device.IsOnline ? ScriptStatus.Running : ScriptStatus.Offline,
        StdOut = string.Empty,
        StdErr = device.IsOnline ? string.Empty : "Device is offline."
      };

      await appDb.ScriptExecutions.AddAsync(execution);
      await appDb.SaveChangesAsync();

      var savedExecution = await appDb.ScriptExecutions
        .Include(x => x.Device)
        .FirstAsync(x => x.Id == execution.Id);

      executions.Add(savedExecution.ToDto());

      if (device.IsOnline && !string.IsNullOrEmpty(device.ConnectionId))
      {
        _ = agentHub.Clients.Client(device.ConnectionId)
          .ExecuteScript(execution.Id, request.AdHocScriptContent, request.ShellType.Value, request.RunAs);
      }
    }

    return Ok(executions.ToArray());
  }

  [HttpGet("executions/{executionId:guid}")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<ScriptExecutionDto>> GetScriptExecution(
    [FromServices] AppDb appDb,
    [FromRoute] Guid executionId)
  {
    if (!User.TryGetTenantId(out var tenantId))
    {
      return NotFound("User tenant not found.");
    }

    var execution = await appDb.ScriptExecutions
      .AsNoTracking()
      .Include(x => x.Script)
      .Include(x => x.Device)
      .FirstOrDefaultAsync(x => x.Id == executionId && x.TenantId == tenantId);

    if (execution is null)
    {
      return NotFound();
    }

    return Ok(execution.ToDto());
  }

  [HttpGet("executions")]
  [Authorize(Roles = RoleNames.TenantAdministrator)]
  public async Task<ActionResult<ScriptExecutionDto[]>> GetAllExecutions([FromServices] AppDb appDb)
  {
    var executions = await appDb.ScriptExecutions
      .AsNoTracking()
      .Include(x => x.Script)
      .Include(x => x.Device)
      .OrderByDescending(x => x.StartedAt)
      .ToListAsync();

    return Ok(executions.Select(x => x.ToDto()).ToArray());
  }
}
