namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Customers;

public class CustomersResponseDto
{
  public IReadOnlyList<CustomerDto> Items { get; set; } = [];
}