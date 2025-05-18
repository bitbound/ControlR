using System.ComponentModel.DataAnnotations;

namespace ControlR.Web.Client.DataValidation;


public class EqualToAttribute(string comparisonProperty) : ValidationAttribute
{
  private readonly string _comparisonProperty = comparisonProperty;

  protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
  {
    var currentValue = value;
    var property = validationContext.ObjectType.GetProperty(_comparisonProperty)
      ?? throw new ArgumentException("Property with this name not found.");

    var comparisonValue = property.GetValue(validationContext.ObjectInstance);

    if (!Equals(currentValue, comparisonValue))
    {
      return new ValidationResult(ErrorMessage ?? $"{validationContext.MemberName} must be equal to {_comparisonProperty}");
    }

    return ValidationResult.Success;
  }
}
