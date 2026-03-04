namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
 
 [MessagePackObject(keyAsPropertyName: true)]
public record GetAspireUrlResponseDto(bool IsConfigured, Uri? AspireUrl);