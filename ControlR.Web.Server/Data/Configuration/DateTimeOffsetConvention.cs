using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace ControlR.Web.Server.Data.Configuration;

public class DateTimeOffsetConvention : IPropertyAddedConvention
{
  public void ProcessPropertyAdded(
    IConventionPropertyBuilder propertyBuilder,
    IConventionContext<IConventionPropertyBuilder> context)
  {
    var property = propertyBuilder.Metadata;
    var propertyType = property.ClrType;

    if (propertyType == typeof(DateTimeOffset) || propertyType == typeof(DateTimeOffset?))
    {
      propertyBuilder.HasConversion(new PostgresDateTimeOffsetConverter());
    }
  }
}