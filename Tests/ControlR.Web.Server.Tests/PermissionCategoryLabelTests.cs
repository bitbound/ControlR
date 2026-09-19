using System.Reflection;
using ControlR.Web.Server.Authz.Permissions;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Guards the category labels the permission picker groups by. A permission's label is declared where
/// it is added, so these tests hold the declarations to their contract.
/// </summary>
public class PermissionCategoryLabelTests
{
  [Fact]
  public void PermissionCatalog_EveryEntryDeclaresACategoryLabel()
  {
    var unlabelled = PermissionCatalog.All.Values
      .Where(metadata => string.IsNullOrWhiteSpace(metadata.CategoryLabel))
      .Select(metadata => metadata.Name)
      .ToList();

    Assert.True(
      unlabelled.Count == 0,
      $"Permissions with no category label: {string.Join(", ", unlabelled)}");
  }

  /// <summary>
  /// Two constants holding the same text would merge two categories into one header, which reads as
  /// one longer list rather than as a mistake.
  /// </summary>
  [Fact]
  public void PermissionCategories_EveryLabelIsDistinct()
  {
    var labels = typeof(PermissionCategories)
      .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
      .Where(field => field.IsLiteral && field.FieldType == typeof(string))
      .Select(field => (string)field.GetRawConstantValue()!)
      .ToList();

    Assert.Equal(labels.Count, labels.Distinct(StringComparer.Ordinal).Count());
  }
}
