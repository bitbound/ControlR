using ControlR.Libraries.Shared.Exceptions;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public record ParameterlessDtoBase([property: MsgPackKey] DtoType DtoType) : DtoRecordBase
{
    public void VerifyType(DtoType expectedType)
    {
        if (DtoType != expectedType)
        {
            throw new DtoTypeMismatchException(expectedType, DtoType);
        }
    }
};