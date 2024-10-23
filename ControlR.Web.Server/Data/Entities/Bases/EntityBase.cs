using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControlR.Web.Server.Data.Entities.Bases;

public class EntityBase
{
  private Guid _id;
  private DateTimeOffset _createdAt;

  [Key]
  public Guid Id
  {
    get => _id;
    set
    {
      if (value == Guid.Empty)
      {
        return;
      }

      _id = value;
    }
  }

  [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  public DateTimeOffset CreatedAt
  {
    get => _createdAt;
    set
    {
      if (value == default ||
          _createdAt != default)
      {
        return;
      }

      _createdAt = value;
    }
  }
}