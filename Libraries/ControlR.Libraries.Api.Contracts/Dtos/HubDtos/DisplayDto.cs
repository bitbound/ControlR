namespace ControlR.Libraries.Api.Contracts.Dtos.HubDtos;

[MessagePackObject(keyAsPropertyName: true)]
public class DisplayDto
{
  public required string DisplayId { get; init; }
  public int Index { get; init; }
  public bool IsPrimary { get; init; }
  /// <summary>
  /// Display bounds in logical (device-independent) units.  Use X/Y for layout positioning
  /// and Width/Height for logical size.  Consistent across all monitors regardless of DPI.
  /// </summary>
  public required DisplayBoundsDto LogicalBounds { get; init; }
  public required string Name { get; init; }
  /// <summary>
  /// Physical pixel height of the capture frame for this display.
  /// </summary>
  public double PhysicalHeight { get; init; }
  /// <summary>
  /// Physical pixel width of the capture frame for this display.
  /// </summary>
  public double PhysicalWidth { get; init; }

  /// <summary>
  /// The scale factor applied to the display's logical bounds to get the physical capture size.
  /// This is typically the monitor's DPI scale (e.g. 1.25 for 125% scaling) but may be adjusted by the client to achieve a desired capture resolution.
  /// </summary>
  public double ScaleFactor { get; init; }
}
