using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.TestingUtilities.Logging;

public sealed record CapturedLog(
  LogLevel LogLevel,
  string CategoryName,
  string Message,
  string? ExceptionMessage);