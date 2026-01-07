namespace ControlR.Libraries.Shared.Dtos.ServerApi;
 
 [MessagePackObject(keyAsPropertyName: true)]
public record GetAspireUrlResponseDto(bool IsConfigured, Uri? AspireUrl);