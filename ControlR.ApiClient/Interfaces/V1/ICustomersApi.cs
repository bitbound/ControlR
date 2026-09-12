using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Customers;

namespace ControlR.ApiClient.Interfaces.V1;

public interface ICustomersApi
{
  [ApiRoute($"{HttpConstants.V1.CustomersEndpoint}/{{customerId}}/devices?tenantId={{tenantId}}", "POST")]
  Task<ApiResult> AssignCustomerDevices(Guid customerId, Guid tenantId, AssignCustomerDevicesRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.CustomersEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<CustomerDto>> CreateCustomer(Guid tenantId, CreateCustomerRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.CustomersEndpoint}/{{customerId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteCustomer(Guid customerId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.CustomersEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<CustomersResponseDto>> GetAllCustomers(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.CustomersEndpoint}/{{customerId}}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<CustomerDto>> GetCustomer(Guid customerId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.CustomersEndpoint}/{{customerId}}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<CustomerDto>> UpdateCustomer(Guid customerId, Guid tenantId, UpdateCustomerRequestDto request, CancellationToken cancellationToken = default);
}