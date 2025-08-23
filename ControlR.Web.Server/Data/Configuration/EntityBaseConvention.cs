using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Configuration;

public class EntityBaseConvention : IEntityTypeAddedConvention
{
  public void ProcessEntityTypeAdded(
    IConventionEntityTypeBuilder entityTypeBuilder,
    IConventionContext<IConventionEntityTypeBuilder> context)
  {
    var entityType = entityTypeBuilder.Metadata.ClrType;

    if (typeof(EntityBase).IsAssignableFrom(entityType))
    {
      entityTypeBuilder
        .Property(typeof(Guid), nameof(EntityBase.Id))
        ?.HasDefaultValueSql("gen_random_uuid()");
        
      entityTypeBuilder
        .Property(typeof(DateTimeOffset), nameof(EntityBase.CreatedAt))
        ?.HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
  }
}
