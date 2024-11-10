using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControlR.Web.Server.Data.Entities.Bases;

public interface IEntityBase
{
  public DateTimeOffset CreatedAt { get; set; }
  public Guid Id { get; set; }
}

public class EntityBase : IEntityBase
{
  private DateTimeOffset _createdAt;
  private Guid _id;

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
}