using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ControlR.Libraries.Configuration.InMemory.Tests;

public class InMemoryConfigurationAccessorTests
{
  [Fact]
  public void Configuration_ReadsInitialValues_FromInMemoryProvider()
  {
    var (accessor, config) = SetupServices();

    accessor.SetValue("Setting:Key", "Value");

    Assert.Equal("Value", config["Setting:Key"]);
    Assert.Equal("Value", config.GetSection("Setting")["Key"]);
  }

  [Fact]
  public void MultipleUpdates_MaintainCorrectState()
  {
    var (accessor, monitor, _) = SetupServicesWithOptions<TestOptions>("Test");

    for (var i = 0; i < 10; i++)
    {
      accessor.SetValue("Test:Number", i.ToString());
      var options = monitor.CurrentValue;
      Assert.Equal(i, options.Number);
    }
  }

  [Fact]
  public void OptionsMonitor_ReactsToChanges_WithOnChange()
  {
    var (accessor, monitor, _) = SetupServicesWithOptions<TestOptions>("Test");

    var changeCount = 0;
    TestOptions? lastOptions = null;

    monitor.OnChange(options =>
    {
      changeCount++;
      lastOptions = options;
    });

    accessor.SetValue("Test:Value", "First");
    Assert.Equal(1, changeCount);
    Assert.Equal("First", lastOptions?.Value);

    accessor.SetValue("Test:Value", "Second");
    Assert.Equal(2, changeCount);
    Assert.Equal("Second", lastOptions?.Value);
  }

  [Fact]
  public void SetValue_SetsConfigurationValue_AndTriggersReload()
  {
    var (accessor, monitor, _) = SetupServicesWithOptions<TestOptions>("Test");

    accessor.SetValue("Test:Value", "Initial");

    var options = monitor.CurrentValue;
    Assert.Equal("Initial", options.Value);

    accessor.SetValue("Test:Value", "Updated");

    options = monitor.CurrentValue;
    Assert.Equal("Updated", options.Value);
  }

  [Fact]
  public void SetValue_WithNull_RemovesKey()
  {
    var (accessor, config) = SetupServices();

    accessor.SetValue("Test:Key", "Value");
    Assert.Equal("Value", config["Test:Key"]);

    accessor.SetValue("Test:Key", null);
    Assert.Null(config["Test:Key"]);
  }

  [Fact]
  public void SetValues_SetsMultipleValues_AndTriggersReload()
  {
    var (accessor, monitor, _) = SetupServicesWithOptions<TestOptions>("Test");

    accessor.SetValues(
    [
      new("Test:Value", "First"),
      new("Test:Number", "42")
    ]);

    var options = monitor.CurrentValue;
    Assert.Equal("First", options.Value);
    Assert.Equal(42, options.Number);

    accessor.SetValues(
    [
      new("Test:Value", "Second"),
      new("Test:Number", "99")
    ]);

    options = monitor.CurrentValue;
    Assert.Equal("Second", options.Value);
    Assert.Equal(99, options.Number);
  }

  [Fact]
  public void SetValues_WithNullValues_RemovesKeys()
  {
    var (accessor, config) = SetupServices();

    accessor.SetValues(
    [
      new("Test:Key1", "Value1"),
      new("Test:Key2", "Value2")
    ]);

    Assert.Equal("Value1", config["Test:Key1"]);
    Assert.Equal("Value2", config["Test:Key2"]);

    accessor.SetValues(
    [
      new("Test:Key1", null),
      new("Test:Key2", null)
    ]);

    Assert.Null(config["Test:Key1"]);
    Assert.Null(config["Test:Key2"]);
  }

  private static (IInMemoryConfigurationAccessor accessor, IConfiguration config) SetupServices()
  {
    var services = new ServiceCollection();
    var configBuilder = new ConfigurationBuilder();

    services.AddInMemoryConfiguration(configBuilder);
    var config = configBuilder.Build();

    var provider = services.BuildServiceProvider();
    var accessor = provider.GetRequiredService<IInMemoryConfigurationAccessor>();

    return (accessor, config);
  }

  private static (IInMemoryConfigurationAccessor accessor, IOptionsMonitor<T> monitor, IConfiguration config) SetupServicesWithOptions<T>(string sectionName)
    where T : class
  {
    var services = new ServiceCollection();
    var configBuilder = new ConfigurationBuilder();

    services.AddInMemoryConfiguration(configBuilder);
    var config = configBuilder.Build();
    services.AddOptions<T>().Bind(config.GetSection(sectionName));

    var provider = services.BuildServiceProvider();
    var accessor = provider.GetRequiredService<IInMemoryConfigurationAccessor>();
    var monitor = provider.GetRequiredService<IOptionsMonitor<T>>();

    return (accessor, monitor, config);
  }

  private class TestOptions
  {
    public int Number { get; set; }
    public string? Value { get; set; }
  }
}
