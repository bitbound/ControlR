using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Models;

[MessagePackObject]
public record Drive
{
  [JsonConverter(typeof(JsonStringEnumConverter))]
  [Key(nameof(DriveType))]
  public DriveType DriveType { get; set; }

  [Key(nameof(RootDirectory))]
  public string RootDirectory { get; set; } = string.Empty;

  [Key(nameof(Name))]
  public string Name { get; set; } = string.Empty;

  [Key(nameof(DriveFormat))]
  public string DriveFormat { get; set; } = string.Empty;

  [Key(nameof(FreeSpace))]
  public double FreeSpace { get; set; }

  [Key(nameof(TotalSize))]
  public double TotalSize { get; set; }

  [Key(nameof(VolumeLabel))]
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
