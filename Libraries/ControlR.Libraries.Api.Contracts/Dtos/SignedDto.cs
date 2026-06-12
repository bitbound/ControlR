namespace ControlR.Libraries.Api.Contracts.Dtos;

[MessagePackObject(keyAsPropertyName: true)]
public record SignedDto<T>(
    T Dto,
    DateTimeOffset Timestamp,
    byte[] Signature,
    string? PublicKey = null);
