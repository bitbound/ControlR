using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Models;

[MessagePackObject]
public record Drive
{
  [MsgPackKey]
  [JsonConverter(typeof(JsonStringEnumConverter))]
  public DriveType DriveType { get; set; }

  [MsgPackKey]
  public string RootDirectory { get; set; } = string.Empty;

  [MsgPackKey]
  public string Name { get; set; } = string.Empty;

  [MsgPackKey]
  public string DriveFormat { get; set; } = string.Empty;

  [MsgPackKey]
  public double FreeSpace { get; set; }

  [MsgPackKey]
  public double TotalSize { get; set; }

  [MsgPackKey]
  public string VolumeLabel { get; set; } = string.Empty;

  [IgnoreMember]
  [JsonIgnore]
  public double UsedSpace => TotalSize - FreeSpace;

  [IgnoreMember]
  [JsonIgnore]
  public string FreeSpacePercentFormatted
  {
    get
    {
      if (TotalSize > 0)
      {
        return $"{Math.Round(FreeSpace / TotalSize * 100, 2)}%";
      }
      return "Unknown";
    }
  }

  [IgnoreMember]
  [JsonIgnore]
  public string UsedSpacePercentFormatted
  {
    get
    {
      if (TotalSize > 0)
      {
        return $"{Math.Round(UsedSpace / TotalSize * 100, 2)}%";
      }
      return "Unknown";
    }
  }
}
