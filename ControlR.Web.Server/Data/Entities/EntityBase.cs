using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControlR.Web.Server.Data.Entities;

public class EntityBase
{
  [Key]
  public int Id { get; set; }
  
  public Guid Uid { get; set; }
}