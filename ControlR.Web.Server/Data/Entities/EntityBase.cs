using System.ComponentModel.DataAnnotations;

namespace ControlR.Web.Server.Data.Entities;

public class EntityBase
{
  private Guid _id;

  [Key]
  public Guid Id
  {
    get => _id;
    set
    {
      if (_id == Guid.Empty)
      {
        return;
      }

      _id = value;
    }
  }
}