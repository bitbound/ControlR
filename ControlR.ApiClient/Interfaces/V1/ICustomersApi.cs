using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using CustDtos = ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Customers;

namespace ControlR.ApiClient.Interfaces.V1;

public interface ICustomersApi
{
  [ApiRoute($"{HttpConstants.V1.CustomersEndpoint}/{{customerId}}/devices?tenantId={{tenantId}}", "POST")]
  Task<ApiResult> AssignCustomerDevices(Guid customerId, Guid tenantId, CustDtos.AssignCustomerDevicesRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.CustomersEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<CustDtos.CustomerDto>> CreateCustomer(Guid tenantId, CustDtos.CreateCustomerRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.CustomersEndpoint}/{{customerId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteCustomer(Guid customerId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.CustomersEndpoint}/{{customerId}}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<CustDtos.CustomerDto>> GetCustomer(Guid customerId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.CustomersEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<CustDtos.CustomersResponseDto>> GetAllCustomers(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.CustomersEndpoint}/{{customerId}}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<CustDtos.CustomerDto>> UpdateCustomer(Guid customerId, Guid tenantId, CustDtos.UpdateCustomerRequestDto request, CancellationToken cancellationToken = default);
}