using ControlR.Libraries.Shared.Dtos.HubDtos;

namespace ControlR.Libraries.Shared.Exceptions;

public class DtoTypeMismatchException(DtoType expectedType, DtoType actualType)
  : Exception($"DtoType mismatch.  Expected: {expectedType}.  Actual: {actualType}.")
{
  public DtoType ExpectedType { get; } = expectedType;
  public DtoType ActualType { get; } = actualType;
}