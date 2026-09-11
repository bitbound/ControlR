using System.Net.Http.Json;
using ControlR.ApiClient.Interfaces.V1;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using CustDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Customers;

namespace ControlR.ApiClient;

internal partial class V1Api
{
  async Task<ApiResult> ICustomersApi.AssignCustomerDevices(Guid customerId, Guid tenantId, CustDtos.AssignCustomerDevicesRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.CustomersEndpoint}/{customerId}/devices?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<CustDtos.CustomerDto>> ICustomersApi.CreateCustomer(Guid tenantId, CustDtos.CreateCustomerRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PostAsJsonAsync(
        $"{HttpConstants.V1.CustomersEndpoint}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CustDtos.CustomerDto>(cancellationToken);
    });
  }

  async Task<ApiResult> ICustomersApi.DeleteCustomer(Guid customerId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.DeleteAsync(
        $"{HttpConstants.V1.CustomersEndpoint}/{customerId}?tenantId={tenantId}", cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
    });
  }

  async Task<ApiResult<CustDtos.CustomerDto>> ICustomersApi.GetCustomer(Guid customerId, Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<CustDtos.CustomerDto>(
        $"{HttpConstants.V1.CustomersEndpoint}/{customerId}?tenantId={tenantId}", cancellationToken));
  }

  async Task<ApiResult<CustDtos.CustomersResponseDto>> ICustomersApi.GetAllCustomers(Guid tenantId, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
      await _client.HttpClient.GetFromJsonAsync<CustDtos.CustomersResponseDto>(
        $"{HttpConstants.V1.CustomersEndpoint}?tenantId={tenantId}", cancellationToken));
  }

  async Task<ApiResult<CustDtos.CustomerDto>> ICustomersApi.UpdateCustomer(Guid customerId, Guid tenantId, CustDtos.UpdateCustomerRequestDto request, CancellationToken cancellationToken)
  {
    return await _client.ExecuteApiCall(async () =>
    {
      using var response = await _client.HttpClient.PutAsJsonAsync(
        $"{HttpConstants.V1.CustomersEndpoint}/{customerId}?tenantId={tenantId}", request, cancellationToken);
      await response.EnsureSuccessStatusCodeWithDetails();
      return await response.Content.ReadFromJsonAsync<CustDtos.CustomerDto>(cancellationToken);
    });
  }
}