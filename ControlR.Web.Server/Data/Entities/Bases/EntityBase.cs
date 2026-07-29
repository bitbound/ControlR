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
  [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  public DateTimeOffset CreatedAt { get; set; }

  [Key]
  [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
  public Guid Id { get; set; }
}