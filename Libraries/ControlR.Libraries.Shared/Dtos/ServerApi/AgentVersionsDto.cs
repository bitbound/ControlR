namespace ControlR.Libraries.Shared.Dtos.ServerApi;
public record AgentVersionsDto(
  Version WinX86,
  Version WinX64,
  Version LinuxX64,
  Version MacOsArm64,
  Version MacOsX64);