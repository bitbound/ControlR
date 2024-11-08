using ControlR.Libraries.Shared.Exceptions;

namespace ControlR.Libraries.Shared.Dtos.HubDtos;

[MessagePackObject]
public record ParameterlessDtoBase([property: MsgPackKey] DtoType DtoType)
{
    public void VerifyType(DtoType expectedType)
    {
        if (DtoType != expectedType)
        {
            throw new DtoTypeMismatchException(expectedType, DtoType);
        }
    }
};