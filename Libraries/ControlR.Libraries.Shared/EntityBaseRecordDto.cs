using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Shared;

[MessagePackObject]
public record EntityBaseRecordDto(
  [property: MsgPackKey]
  [property: Display(Name = "Id")]
  Guid Id);