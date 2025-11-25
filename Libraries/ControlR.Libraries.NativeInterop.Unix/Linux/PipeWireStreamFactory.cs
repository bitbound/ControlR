using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.NativeInterop.Unix.Linux;

public interface IPipeWireStreamFactory
{
  PipeWireStream Create(uint nodeId, SafeHandle pipewireFd, int expectedLogicalWidth = 0, int expectedLogicalHeight = 0);
}

public class PipeWireStreamFactory(ILoggerFactory loggerFactory) : IPipeWireStreamFactory
{
  private readonly ILoggerFactory _loggerFactory = loggerFactory;

  public PipeWireStream Create(uint nodeId, SafeHandle pipewireFd, int expectedLogicalWidth = 0, int expectedLogicalHeight = 0)
  {
    var logger = _loggerFactory.CreateLogger<PipeWireStream>();
    return new PipeWireStream(logger, nodeId, pipewireFd, expectedLogicalWidth, expectedLogicalHeight);
  }
}
