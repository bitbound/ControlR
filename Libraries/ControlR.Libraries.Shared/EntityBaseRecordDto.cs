using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Shared;

[MessagePackObject]
public record EntityBaseRecordDto(
  [property: MsgPackKey]
  [property: Display(Name = "Id")]
  int Id,

  [property: MsgPackKey]
  [property: Display(Name = "Device Uid")]
   Guid Uid);