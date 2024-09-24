using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControlR.Web.Server.Data.Entities;

public class EntityBase
{
  private int _id;

  [Key]
  public int Id
  {
    get => _id;
    set
    {
      if (value == 0)
      {
        return;
      }

      _id = value;
    }
  }
  
  public Guid Uid { get; set; }
}