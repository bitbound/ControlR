namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Customers;

public record CustomerDto(
  Guid Id,
  string Name,
  string? Description,
  string? Notes,
  DateTimeOffset CreatedAt,
  int DeviceCount);