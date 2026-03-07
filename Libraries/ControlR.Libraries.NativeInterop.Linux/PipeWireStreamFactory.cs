using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.NativeInterop.Linux;

public interface IPipeWireStreamFactory
{
  /// <summary>
  /// Create a <see cref="PipeWireStream"/> for a given PipeWire node.
  /// <para>The <c>expectedLogicalWidth</c>/<c>expectedLogicalHeight</c> parameters are the
  /// compositor/portal-reported logical dimensions for the stream. The stream may provide
  /// physical pixel caps which can differ; callers should use <see cref="PipeWireStream.Width"/>
  /// and <see cref="PipeWireStream.Height"/> to query the actual capture pixel size once streaming.
  /// </para>
  /// </summary>
  PipeWireStream Create(uint nodeId, SafeHandle pipewireFd, int expectedLogicalWidth = 0, int expectedLogicalHeight = 0);
}

public class PipeWireStreamFactory(ILoggerFactory loggerFactory) : IPipeWireStreamFactory
{
  private readonly ILoggerFactory _loggerFactory = loggerFactory;

  public PipeWireStream Create(uint nodeId, SafeHandle pipewireFd, int expectedLogicalWidth = 0, int expectedLogicalHeight = 0)
  {
    var logger = _loggerFactory.CreateLogger<PipeWireStream>();
    return new PipeWireStream(nodeId, pipewireFd, expectedLogicalWidth, expectedLogicalHeight, logger);
  }
}
