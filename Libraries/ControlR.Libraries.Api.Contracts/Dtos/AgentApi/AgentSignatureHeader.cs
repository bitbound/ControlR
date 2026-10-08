using System.Diagnostics.CodeAnalysis;
using MessagePack;

namespace ControlR.Libraries.Api.Contracts.Dtos.AgentApi;

/// <summary>
/// Encodes and decodes the header an installed agent uses to authenticate a REST request. The value
/// is a base64 MessagePack-encoded <see cref="SignedDto{T}"/> whose payload is an
/// <see cref="AgentRequestAttestationDto"/>.
/// </summary>
public static class AgentSignatureHeader
{
  public const string Name = "x-agent-signature";

  public static string Encode(SignedDto<AgentRequestAttestationDto> signedDto)
  {
    return Convert.ToBase64String(MessagePackSerializer.Serialize(signedDto));
  }

  public static bool TryDecode(
    string? headerValue,
    [NotNullWhen(true)] out SignedDto<AgentRequestAttestationDto>? signedDto)
  {
    signedDto = null;

    if (string.IsNullOrWhiteSpace(headerValue))
    {
      return false;
    }

    try
    {
      signedDto = MessagePackSerializer.Deserialize<SignedDto<AgentRequestAttestationDto>>(
        Convert.FromBase64String(headerValue));
      return signedDto is not null;
    }
    catch (Exception)
    {
      // A malformed header is a failed authentication, not a failed request. The caller reports it.
      return false;
    }
  }
}
