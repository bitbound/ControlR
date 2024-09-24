using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Shared.Dtos;

public class EntityDtoBase
{
  [MsgPackKey]
  [Display(Name = "Id")]
  public int Id { get; set; }
  
  [MsgPackKey]
  [Display(Name = "Device Uid")]
  public Guid Uid { get; set; }
}