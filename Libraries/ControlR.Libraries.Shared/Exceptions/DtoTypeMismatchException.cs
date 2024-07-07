using ControlR.Libraries.Shared.Dtos;

namespace ControlR.Libraries.Shared.Exceptions;
public class DtoTypeMismatchException : Exception
{
    public DtoTypeMismatchException(DtoType expectedType, DtoType actualType)
        : base($"DtoType mismatch.  Expected: {expectedType}.  Actual: {actualType}.")
    {
        ExpectedType = expectedType;
        ActualType = actualType;
    }

    public DtoType ExpectedType { get; }
    public DtoType ActualType { get; }
}
