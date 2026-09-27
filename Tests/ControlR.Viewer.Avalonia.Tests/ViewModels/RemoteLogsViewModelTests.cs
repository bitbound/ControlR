using ControlR.ApiClient;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Avalonia.Controls.Snackbar;
using ControlR.Libraries.TestingUtilities;
using ControlR.Libraries.TestingUtilities.Extensions;
using ControlR.Viewer.Avalonia.Tests.Fakes;
using ControlR.Viewer.Avalonia.ViewModels;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceFileSystem;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.ApiClient.Interfaces.V1;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace ControlR.Viewer.Avalonia.Tests.ViewModels;

public class RemoteLogsViewModelTests
{
  private readonly FakeDeviceState _deviceState;
  private readonly TestUiDispatcher _dispatcher;
  private readonly XunitLogger<RemoteLogsViewModel> _logger;
  private readonly Mock<IControlrApi> _mockApi;
  private readonly Mock<ControlR.ApiClient.Interfaces.V1.IDeviceFileSystemApi> _mockFileSystemApi;
  private readonly Mock<IControlrV1Api> _mockV1Api;
  private readonly FakeSnackbar _snackbar;
  private readonly FakeTimeProvider _timeProvider;

  public RemoteLogsViewModelTests(ITestOutputHelper testOutputHelper)
  {
    _deviceState = new FakeDeviceState();
    _snackbar = new FakeSnackbar();
    _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    _dispatcher = new TestUiDispatcher();
    _logger = new XunitLogger<RemoteLogsViewModel>(testOutputHelper);

    _mockFileSystemApi = new Mock<ControlR.ApiClient.Interfaces.V1.IDeviceFileSystemApi>();
    _mockV1Api = new Mock<IControlrV1Api>(MockBehavior.Loose) { DefaultValue = DefaultValue.Mock };
    _mockV1Api.SetupGet(x => x.DeviceFileSystem).Returns(_mockFileSystemApi.Object);

    _mockApi = new Mock<IControlrApi>(MockBehavior.Loose) { DefaultValue = DefaultValue.Mock };
    _mockApi.SetupGet(x => x.V1).Returns(_mockV1Api.Object);
  }

  [Fact]
  public void ApplyFilter_WithCRLFContent_FiltersCorrectly()
  {
    // Arrange
    var vm = CreateViewModel();
    vm.LogContents = "ERROR: something\r\nINFO: all good\r\nERROR: another";
    vm.FilterText = "ERROR";

    // Act
    vm.ApplyFilter();

    // Assert
    // CRLF is normalized to LF before filtering.
    Assert.Equal("ERROR: something\nERROR: another", vm.DisplayedLogContents);
  }

  [Fact]
  public void ApplyFilter_WithEmptySearch_ReplacesWithAllContent()
  {
    // Arrange
    var vm = CreateViewModel();
    vm.LogContents = "line1\nline2";

    // Act
    vm.ApplyFilter();

    // Assert
    Assert.Equal("line1\nline2", vm.DisplayedLogContents);
  }

  [Fact]
  public async Task ApplyFilter_WithNoMatchingLines_SetsShowNoFilterMatchesMessage()
  {
    // Arrange
    var vm = CreateViewModel();
    var fileNode = new LogFilesTreeItemViewModel("LogFile.log", LogKind.Agent, null, isFile: true);

    _mockFileSystemApi
      .Setup(x => x.GetDeviceLogFileContents(
        It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LogKind>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Ok("line1\nline2"));

    await vm.SelectNode(fileNode);
    vm.FilterText = "NONEXISTENT";

    // Act
    vm.ApplyFilter();

    // Assert
    Assert.Empty(vm.DisplayedLogContents);
    Assert.True(vm.ShowNoFilterMatchesMessage);
  }

  [Fact]
  public void ApplyFilter_WithNonEmptySearch_ReturnsMatchingLines()
  {
    // Arrange
    var vm = CreateViewModel();
    vm.LogContents = "ERROR: something\nINFO: all good\nERROR: another";
    vm.FilterText = "ERROR";

    // Act
    vm.ApplyFilter();

    // Assert
    Assert.Equal("ERROR: something\nERROR: another", vm.DisplayedLogContents);
  }

  [Fact]
  public void ApplyFilter_WithWhitespaceSearch_ReplacesWithAllContent()
  {
    // Arrange
    var vm = CreateViewModel();
    vm.LogContents = "line1\nline2";
    vm.FilterText = "   ";

    // Act
    vm.ApplyFilter();

    // Assert
    Assert.Equal("line1\nline2", vm.DisplayedLogContents);
  }

  [Fact]
  public void Dispose_DoesNotThrow()
  {
    // Arrange
    var vm = CreateViewModel();

    // Act
    var exception = Record.Exception(() => vm.Dispose());

    // Assert
    Assert.Null(exception);
  }

  [Fact]
  public async Task FilterText_Changes_SchedulesDebounce()
  {
    // Arrange
    var vm = CreateViewModel();
    vm.LogContents = "ERROR: something\nINFO: all good\nERROR: another";

    // Act - setting FilterText triggers ScheduleFilterDebounce
    vm.FilterText = "ERROR";

    var waitResult = await _timeProvider.WaitForWaiters(
      x => x > 0,
      cancellationToken: TestContext.Current.CancellationToken);
    Assert.True(waitResult, "Expected a waiter on the FakeTimeProvider after setting FilterText.");

    _timeProvider.Advance(TimeSpan.FromMilliseconds(500));

    // Assert - debounce should have completed and filtered the log
    Assert.Equal("ERROR: something\nERROR: another", vm.DisplayedLogContents);
  }

  [Fact]
  public async Task LoadLogFiles_FailureShowsSnackbar()
  {
    // Arrange
    var vm = CreateViewModel();

    _mockFileSystemApi
      .Setup(x => x.GetDeviceLogFiles(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Fail<DeviceLogFileListResponseDto>("API error"));

    // Act
    await vm.Initialize();

    // Assert
    Assert.Contains(_snackbar.Calls, c => c.Severity == SnackbarSeverity.Error);
    Assert.False(vm.HasRootItems);
  }

  [Fact]
  public async Task LoadLogFiles_Success_PopulatesRootItems()
  {
    // Arrange
    var vm = CreateViewModel();

    var logGroup = new DeviceLogFileGroupDto(
      "DesktopClient Logs (alice)",
      LogKind.DesktopClient,
      "alice",
      [new DeviceLogFileEntryDto("LogFile20260101.log", "/home/alice/logs/LogFile20260101.log", 1024, DateTimeOffset.UtcNow)]);

    var response = new DeviceLogFileListResponseDto([logGroup]);

    _mockFileSystemApi
      .Setup(x => x.GetDeviceLogFiles(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Ok(response));
    _mockFileSystemApi
      .Setup(x => x.GetDeviceLogFileContents(
        It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LogKind>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Ok("log contents"));

    // Act
    await vm.Initialize();

    // Assert
    Assert.True(vm.HasRootItems);
    var rootItem = vm.RootItems.FirstOrDefault();
    Assert.NotNull(rootItem);
    Assert.Equal("DesktopClient Logs (alice)", rootItem.Name);
    Assert.False(rootItem.IsFile);
    Assert.Single(rootItem.Children);
    var logFileItem = rootItem.Children[0];
    Assert.Equal("LogFile20260101.log", logFileItem.Name);
    Assert.True(logFileItem.IsFile);
    Assert.Equal(LogKind.DesktopClient, logFileItem.Kind);
    Assert.Equal("alice", logFileItem.Username);

    await vm.SelectNode(logFileItem);

    _mockFileSystemApi.Verify(
      x => x.GetDeviceLogFileContents(
        It.IsAny<Guid>(),
        It.Is<Guid>(tenantId => tenantId == _deviceState.CurrentDevice.TenantId),
        It.Is<LogKind>(kind => kind == LogKind.DesktopClient),
        It.Is<string>(fileName => fileName == "LogFile20260101.log"),
        It.Is<string?>(username => username == "alice"),
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task LogContents_Set_ToEmpty_SetsShowNoContentMessage()
  {
    // Arrange
    var vm = CreateViewModel();
    var fileNode = new LogFilesTreeItemViewModel("LogFile.log", LogKind.Agent, null, isFile: true);

    _mockFileSystemApi
      .Setup(x => x.GetDeviceLogFileContents(
        It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LogKind>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Ok("initial content"));

    await vm.SelectNode(fileNode);
    Assert.False(vm.ShowNoContentMessage);

    // Act
    vm.LogContents = string.Empty;

    // Assert
    Assert.True(vm.ShowNoContentMessage);
  }

  [Fact]
  public async Task RefreshCurrentContents_WithFolderNode_ReturnsCompletedTask()
  {
    // Arrange
    var vm = CreateViewModel();
    var folderNode = new LogFilesTreeItemViewModel("MyGroup", kind: null, username: null, isFile: false);
    await vm.SelectNode(folderNode);

    // Act
    var task = vm.RefreshCurrentContents();

    // Assert
    Assert.True(task.IsCompletedSuccessfully);
  }

  [Fact]
  public void RefreshCurrentContents_WithNullNode_ReturnsCompletedTask()
  {
    // Arrange
    var vm = CreateViewModel();

    // Act
    var task = vm.RefreshCurrentContents();

    // Assert
    Assert.True(task.IsCompletedSuccessfully);
  }

  [Fact]
  public async Task SelectNode_WithFileNode_LoadsLogContent()
  {
    // Arrange
    var vm = CreateViewModel();
    var fileNode = new LogFilesTreeItemViewModel("LogFile.log", LogKind.Agent, null, isFile: true);

    _mockFileSystemApi
      .Setup(x => x.GetDeviceLogFileContents(
        It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LogKind>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Ok("file contents"));

    // Act
    await vm.SelectNode(fileNode);

    // Assert
    Assert.Equal("file contents", vm.LogContents);
    Assert.True(fileNode.IsSelected);
  }

  [Fact]
  public async Task SelectNode_WithFolderNode_DoesNotLoadContent()
  {
    // Arrange
    var vm = CreateViewModel();
    var folderNode = new LogFilesTreeItemViewModel("MyGroup", kind: null, username: null, isFile: false);

    // Act
    await vm.SelectNode(folderNode);

    // Assert
    Assert.Empty(vm.LogContents);
    _mockFileSystemApi.Verify(
      x => x.GetDeviceLogFileContents(
        It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LogKind>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task SelectNode_WithNullNode_ClearsLogContents()
  {
    // Arrange
    var vm = CreateViewModel();
    vm.LogContents = "some content";
    var fileNode = new LogFilesTreeItemViewModel("LogFile.log", LogKind.Agent, null, isFile: true);

    _mockFileSystemApi
      .Setup(x => x.GetDeviceLogFileContents(
        It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LogKind>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(ApiResult.Ok("file contents"));

    // Act - first select a file node to set _selectedNode
    await vm.SelectNode(fileNode);
    Assert.Equal("file contents", vm.LogContents);

    // Act - now select null
    await vm.SelectNode(null);

    // Assert
    Assert.Empty(vm.LogContents);
  }

  private RemoteLogsViewModel CreateViewModel()
  {
    return new RemoteLogsViewModel(
      _timeProvider,
      _mockApi.Object,
      _deviceState,
      _snackbar,
      _logger,
      _dispatcher);
  }
}
