namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;
 
 [MessagePackObject(keyAsPropertyName: true)]
public record GetAspireUrlResponseDto(bool IsConfigured, Uri? AspireUrl);