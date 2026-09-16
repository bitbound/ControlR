using System.Reflection;
using ControlR.Libraries.Api.Contracts.Settings;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Keeps the three artifacts that describe a setting family in lockstep: the name constants,
/// the <c>SettingDefinition</c> members, and the typed DTO. Adding, removing, or renaming a
/// key by hand touches all three, so each is asserted against the others here. Without this,
/// a name constant with no definition is silently accepted and persisted.
/// </summary>
public class SettingsDefinitionParityTests
{
  public static TheoryData<string> Families => ["UserPreferences", "TenantSettings"];

  [Theory]
  [MemberData(nameof(Families))]
  public void All_ContainsExactlyTheDeclaredDefinitions(string familyName)
  {
    var family = SettingFamily.Resolve(familyName);

    var declared = family.DefinitionsType
      .GetProperties(BindingFlags.Public | BindingFlags.Static)
      .Where(x => typeof(ISettingDefinition).IsAssignableFrom(x.PropertyType))
      .Select(x => x.GetValue(null))
      .OfType<ISettingDefinition>()
      .ToArray();

    var missingFromAll = declared.Except(family.All).Select(x => x.Name).ToArray();
    Assert.True(
      missingFromAll.Length == 0,
      $"{family.Name}: these definitions are declared but missing from All: {string.Join(", ", missingFromAll)}.");

    var missingFromDeclarations = family.All.Except(declared).Select(x => x.Name).ToArray();
    Assert.True(
      missingFromDeclarations.Length == 0,
      $"{family.Name}: All contains entries with no matching property: {string.Join(", ", missingFromDeclarations)}.");
  }

  [Theory]
  [MemberData(nameof(Families))]
  public void DefinitionCount_MatchesDtoConstructor(string familyName)
  {
    var family = SettingFamily.Resolve(familyName);

    var dtoParameterCount = family.DtoType
      .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
      .Max(x => x.GetParameters().Length);

    Assert.True(
      family.All.Count == dtoParameterCount,
      $"{family.Name}: {family.All.Count} definitions but {family.DtoType.Name} has {dtoParameterCount} constructor parameters.");
  }

  [Theory]
  [MemberData(nameof(Families))]
  public void DefinitionNames_AreUnique(string familyName)
  {
    var family = SettingFamily.Resolve(familyName);

    var duplicates = family.All
      .GroupBy(x => x.Name, StringComparer.Ordinal)
      .Where(x => x.Count() > 1)
      .Select(x => x.Key)
      .ToArray();

    Assert.True(
      duplicates.Length == 0,
      $"{family.Name}: duplicate definition names: {string.Join(", ", duplicates)}.");
  }

  [Theory]
  [MemberData(nameof(Families))]
  public void Definitions_AndNameConstants_AreInSync(string familyName)
  {
    var family = SettingFamily.Resolve(familyName);

    var nameConstants = family.NamesType
      .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
      .Where(x => x is { IsLiteral: true, IsInitOnly: false } && x.FieldType == typeof(string))
      .Select(x => (string)x.GetRawConstantValue()!)
      .ToHashSet(StringComparer.Ordinal);

    var definitionNames = family.All.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);

    var withoutDefinition = nameConstants.Except(definitionNames).ToArray();
    Assert.True(
      withoutDefinition.Length == 0,
      $"{family.Name}: name constants with no definition: {string.Join(", ", withoutDefinition)}.");

    var withoutConstant = definitionNames.Except(nameConstants).ToArray();
    Assert.True(
      withoutConstant.Length == 0,
      $"{family.Name}: definitions whose name is not declared in {family.NamesType.Name}: {string.Join(", ", withoutConstant)}.");
  }

  [Theory]
  [MemberData(nameof(Families))]
  public void Dto_RoundTripsThroughValues(string familyName)
  {
    var family = SettingFamily.Resolve(familyName);

    var defaults = family.CreateDto(new Dictionary<string, string>());
    var values = family.GetValues(defaults);

    Assert.True(
      values.Count == family.All.Count,
      $"{family.Name}: GetValues returned {values.Count} pairs but there are {family.All.Count} definitions.");

    var definitionNames = family.All.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
    var unexpectedNames = values.Select(x => x.Name).Where(x => !definitionNames.Contains(x)).ToArray();
    Assert.True(
      unexpectedNames.Length == 0,
      $"{family.Name}: GetValues returned names with no definition: {string.Join(", ", unexpectedNames)}.");

    var populated = values
      .Where(x => x.Value is not null)
      .ToDictionary(x => x.Name, x => x.Value!, StringComparer.Ordinal);

    Assert.Equal(defaults, family.CreateDto(populated));
  }

  private sealed class SettingFamily(
    string name,
    Type namesType,
    Type definitionsType,
    Type dtoType,
    IReadOnlyList<ISettingDefinition> all,
    Func<IReadOnlyDictionary<string, string>, object> createDto,
    Func<object, IReadOnlyList<(string Name, string? Value)>> getValues)
  {
    public IReadOnlyList<ISettingDefinition> All { get; } = all;
    public Func<IReadOnlyDictionary<string, string>, object> CreateDto { get; } = createDto;
    public Type DefinitionsType { get; } = definitionsType;
    public Type DtoType { get; } = dtoType;
    public Func<object, IReadOnlyList<(string Name, string? Value)>> GetValues { get; } = getValues;
    public string Name { get; } = name;
    public Type NamesType { get; } = namesType;

    public static SettingFamily Resolve(string name)
    {
      return name switch
      {
        "UserPreferences" => new SettingFamily(
          "UserPreferences",
          typeof(UserPreferenceNames),
          typeof(UserPreferenceDefinitions),
          typeof(InternalDtos.UserPreferencesDto),
          UserPreferenceDefinitions.All,
          values => UserPreferenceDefinitions.CreateDto(values),
          dto => UserPreferenceDefinitions.GetValues((InternalDtos.UserPreferencesDto)dto)),
        "TenantSettings" => new SettingFamily(
          "TenantSettings",
          typeof(TenantSettingNames),
          typeof(TenantSettingDefinitions),
          typeof(InternalDtos.TenantSettingsDto),
          TenantSettingDefinitions.All,
          values => TenantSettingDefinitions.CreateDto(values),
          dto => TenantSettingDefinitions.GetValues((InternalDtos.TenantSettingsDto)dto)),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown setting family.")
      };
    }
  }
}