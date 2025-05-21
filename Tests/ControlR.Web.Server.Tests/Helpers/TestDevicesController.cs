using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Web.Server.Api;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ControlR.Web.Server.Tests.Helpers;

/// <summary>
/// Provides a testing-friendly implementation of DevicesController for tests
/// </summary>
public class TestDevicesController : DevicesController
{
    /// <summary>
    /// A test-friendly implementation of GetDevicesGridData that avoids Entity Framework query translation errors
    /// </summary>
    public async Task<ActionResult<DeviceGridResponseDto>> GetDevicesGridDataTest(
        DeviceGridRequestDto requestDto,
        AppDb appDb,
        IAuthorizationService authorizationService,
        ILogger<DevicesController> logger)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            logger.LogInformation("Processing device grid request: UserId: {UserId}, Page {Page}, PageSize {PageSize}, Search: {SearchText}, HideOffline: {HideOffline}, TagIds: {TagIds}",
                userId, requestDto.Page, requestDto.PageSize, requestDto.SearchText, requestDto.HideOfflineDevices,
                requestDto.TagIds != null ? string.Join(",", requestDto.TagIds) : "none");
            
            // Load all devices into memory to avoid EF Core query translation issues
            var devices = await appDb.Devices
                .Include(d => d.Tags)
                .ToListAsync();

            // Create a client-side queryable
            var query = devices.AsQueryable();
            
            // Apply filtering
            if (!string.IsNullOrWhiteSpace(requestDto.SearchText))
            {
                var searchText = requestDto.SearchText.ToLower();
                query = query.Where(d => 
                    d.Name.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                    d.Alias.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                    d.OsDescription.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                    d.ConnectionId.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                    d.MacAddresses.Any(m => m.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)) ||
                    d.CurrentUsers.Any(u => u.Contains(searchText, StringComparison.CurrentCultureIgnoreCase)));
            }
            
            if (requestDto.HideOfflineDevices)
            {
                query = query.Where(d => d.IsOnline);
            }

            if (requestDto.TagIds != null && requestDto.TagIds.Count > 0)
            {
                query = query.Where(d => d.Tags != null && d.Tags.Any(t => requestDto.TagIds.Contains(t.Id)));
            }
            
            // Apply sorting
            if (requestDto.SortDefinitions != null && requestDto.SortDefinitions.Count > 0)
            {
                IOrderedQueryable<Device>? orderedQuery = null;
                
                foreach (var sortDef in requestDto.SortDefinitions.OrderBy(s => s.SortOrder))
                {
                    Func<IQueryable<Device>, IOrderedQueryable<Device>> orderFunc;
                    
                    switch (sortDef.PropertyName)
                    {
                        case nameof(Device.Name):
                            orderFunc = q => sortDef.Descending ? q.OrderByDescending(d => d.Name) : q.OrderBy(d => d.Name);
                            break;
                        case nameof(Device.IsOnline):
                            orderFunc = q => sortDef.Descending ? q.OrderByDescending(d => d.IsOnline) : q.OrderBy(d => d.IsOnline);
                            break;
                        case "CpuUtilization":
                            orderFunc = q => sortDef.Descending ? q.OrderByDescending(d => d.CpuUtilization) : q.OrderBy(d => d.CpuUtilization);
                            break;
                        case "UsedMemoryPercent":
                            orderFunc = q => sortDef.Descending ? 
                                q.OrderByDescending(d => d.UsedMemory / (double)(d.TotalMemory == 0 ? 1 : d.TotalMemory)) : 
                                q.OrderBy(d => d.UsedMemory / (double)(d.TotalMemory == 0 ? 1 : d.TotalMemory));
                            break;
                        case "UsedStoragePercent":
                            orderFunc = q => sortDef.Descending ? 
                                q.OrderByDescending(d => d.UsedStorage / (double)(d.TotalStorage == 0 ? 1 : d.TotalStorage)) : 
                                q.OrderBy(d => d.UsedStorage / (double)(d.TotalStorage == 0 ? 1 : d.TotalStorage));
                            break;
                        default:
                            continue;
                    }
                    
                    orderedQuery = orderedQuery == null ? orderFunc(query) : orderFunc(orderedQuery);
                }
                
                query = orderedQuery ?? query;
            }
            
            // Count total items before pagination
            var totalCount = query.Count();
            
            // Apply pagination
            var pagedItems = query
                .Skip(requestDto.Page * requestDto.PageSize)
                .Take(requestDto.PageSize)
                .ToList();
            
            // Filter for authorized devices
            var authorizedDevices = new List<DeviceDto>();
            
            foreach (var device in pagedItems)
            {
                var authResult = await authorizationService.AuthorizeAsync(User, device, Authz.Policies.DeviceAccessByDeviceResourcePolicy.PolicyName);
                if (authResult.Succeeded)
                {
                    authorizedDevices.Add(device.ToDto());
                }
            }
            
            var response = new DeviceGridResponseDto
            {
                Items = authorizedDevices,
                TotalItems = totalCount
            };
            
            logger.LogInformation("Returning device grid data: Total: {TotalItems}, Returned: {ItemCount}, Page: {Page}, PageSize: {PageSize}",
                response.TotalItems, response.Items.Count, requestDto.Page, requestDto.PageSize);
            
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving device grid data: {ErrorMessage}", ex.Message);
            return StatusCode(500, "An error occurred while retrieving device data");
        }
    }
}
