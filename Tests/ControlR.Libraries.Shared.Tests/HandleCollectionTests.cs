using ControlR.Libraries.Shared.Collections;

namespace ControlR.Libraries.Shared.Tests;

public class HandleCollectionTests
{
  [Fact]
  public async Task AddHandler_ShouldAllowSameSubscriberOnce_WhenAddedMultipleTimes()
  {
    // Arrange
    var collection = new HandlerCollection<TestData>();
    var subscriber = new object();
    var invocationCount = 0;
    var testData = new TestData { Message = "Test" };

    // Act
    collection.AddHandler(subscriber, _ => { invocationCount++; return Task.CompletedTask; });

    // Adding the same subscriber again should throw or replace
    var exception = Record.Exception(() =>
        collection.AddHandler(subscriber, _ => { invocationCount++; return Task.CompletedTask; }));

    // Assert - ConditionalWeakTable.Add throws if key already exists
    Assert.NotNull(exception);
    Assert.IsType<ArgumentException>(exception);
  }

  [Fact]
  public async Task AddHandler_ShouldInvokeHandler_WhenInvoked()
  {
    // Arrange
    var collection = new HandlerCollection<TestData>();
    var subscriber = new object();
    var handlerInvoked = false;
    var testData = new TestData { Message = "Test" };

    collection.AddHandler(subscriber, data =>
    {
      handlerInvoked = true;
      return Task.CompletedTask;
    });

    // Act
    await collection.InvokeHandlers(testData, CancellationToken.None);

    // Assert
    Assert.True(handlerInvoked);
  }

  [Fact]
  public async Task AddHandler_ShouldInvokeMultipleHandlers_WhenMultipleAdded()
  {
    // Arrange
    var collection = new HandlerCollection<TestData>();
    var subscriber1 = new object();
    var subscriber2 = new object();
    var subscriber3 = new object();
    var invocationCount = 0;
    var testData = new TestData { Message = "Test" };

    collection.AddHandler(subscriber1, _ => { invocationCount++; return Task.CompletedTask; });
    collection.AddHandler(subscriber2, _ => { invocationCount++; return Task.CompletedTask; });
    collection.AddHandler(subscriber3, _ => { invocationCount++; return Task.CompletedTask; });

    // Act
    await collection.InvokeHandlers(testData, CancellationToken.None);

    // Assert
    Assert.Equal(3, invocationCount);
  }

  [Fact]
  public async Task AddHandler_ShouldNotInvokeDisposedHandler_WhenDisposed()
  {
    // Arrange
    var collection = new HandlerCollection<TestData>();
    var subscriber = new object();
    var handlerInvoked = false;
    var testData = new TestData { Message = "Test" };

    var disposable = collection.AddHandler(subscriber, data =>
    {
      handlerInvoked = true;
      return Task.CompletedTask;
    });

    // Act
    disposable.Dispose();
    await collection.InvokeHandlers(testData, CancellationToken.None);

    // Assert
    Assert.False(handlerInvoked);
  }

  [Fact]
  public async Task AddHandler_ShouldPassCorrectData_WhenInvoked()
  {
    // Arrange
    var collection = new HandlerCollection<TestData>();
    var subscriber = new object();
    TestData? receivedData = null;
    var testData = new TestData { Message = "Test Message" };

    collection.AddHandler(subscriber, data =>
    {
      receivedData = data;
      return Task.CompletedTask;
    });

    // Act
    await collection.InvokeHandlers(testData, CancellationToken.None);

    // Assert
    Assert.NotNull(receivedData);
    Assert.Equal("Test Message", receivedData.Message);
  }

  [Fact]
  public async Task AddHandler_ShouldSupportAsyncHandlers_WhenAwaitingAsyncOperations()
  {
    // Arrange
    var collection = new HandlerCollection<TestData>();
    var subscriber = new object();
    var asyncOperationCompleted = false;
    var testData = new TestData { Message = "Test" };

    collection.AddHandler(subscriber, async _ =>
    {
      await Task.Delay(10);
      asyncOperationCompleted = true;
    });

    // Act
    await collection.InvokeHandlers(testData, CancellationToken.None);

    // Assert
    Assert.True(asyncOperationCompleted);
  }

  [Fact]
  public async Task Dispose_ShouldAllowReuse_WhenSameSubscriberReAdded()
  {
    // Arrange
    var collection = new HandlerCollection<TestData>();
    var subscriber = new object();
    var invocationCount = 0;
    var testData = new TestData { Message = "Test" };

    var disposable1 = collection.AddHandler(subscriber, _ => { invocationCount++; return Task.CompletedTask; });
    disposable1.Dispose();

    // Act
    var disposable2 = collection.AddHandler(subscriber, _ => { invocationCount++; return Task.CompletedTask; });
    await collection.InvokeHandlers(testData, CancellationToken.None);

    // Assert
    Assert.Equal(1, invocationCount);
  }

  [Fact]
  public async Task ExceptionHandler_ShouldBeAwaited_WhenAsync()
  {
    // Arrange
    var exceptionHandlerCompleted = false;
    var collection = new HandlerCollection<TestData>(async _ =>
    {
      await Task.Delay(10);
      exceptionHandlerCompleted = true;
    });
    var subscriber = new object();
    var testData = new TestData { Message = "Test" };

    collection.AddHandler(subscriber, _ => throw new InvalidOperationException());

    // Act
    await collection.InvokeHandlers(testData, CancellationToken.None);

    // Assert
    Assert.True(exceptionHandlerCompleted);
  }

  [Fact]
  public async Task InvokeHandlers_ShouldCatchException_WhenExceptionHandlerProvided()
  {
    // Arrange
    Exception? caughtException = null;
    var collection = new HandlerCollection<TestData>(ex =>
    {
      caughtException = ex;
      return Task.CompletedTask;
    });
    var subscriber = new object();
    var expectedException = new InvalidOperationException("Test exception");
    var testData = new TestData { Message = "Test" };

    collection.AddHandler(subscriber, _ => throw expectedException);

    // Act
    await collection.InvokeHandlers(testData, CancellationToken.None);

    // Assert
    Assert.NotNull(caughtException);
    Assert.Same(expectedException, caughtException);
  }

  [Fact]
  public async Task InvokeHandlers_ShouldContinueInvoking_WhenOneHandlerThrows()
  {
    // Arrange
    var collection = new HandlerCollection<TestData>(_ => Task.CompletedTask);
    var subscriber1 = new object();
    var subscriber2 = new object();
    var handler2Invoked = false;
    var testData = new TestData { Message = "Test" };

    collection.AddHandler(subscriber1, _ => throw new InvalidOperationException());
    collection.AddHandler(subscriber2, _ => { handler2Invoked = true; return Task.CompletedTask; });

    // Act
    await collection.InvokeHandlers(testData, CancellationToken.None);

    // Assert
    Assert.True(handler2Invoked);
  }

  [Fact]
  public async Task InvokeHandlers_ShouldNotThrow_WhenNoExceptionHandlerProvided()
  {
    // Arrange
    var collection = new HandlerCollection<TestData>();
    var subscriber = new object();
    var testData = new TestData { Message = "Test" };

    collection.AddHandler(subscriber, _ => throw new InvalidOperationException());

    // Act & Assert
    await collection.InvokeHandlers(testData, CancellationToken.None);
  }

  [Fact]
  public async Task InvokeHandlers_ShouldNotThrow_WhenNoHandlersRegistered()
  {
    // Arrange
    var collection = new HandlerCollection<TestData>();
    var testData = new TestData { Message = "Test" };

    // Act & Assert
    await collection.InvokeHandlers(testData, CancellationToken.None);
  }

  [Fact]
  public async Task InvokeHandlers_ShouldRespectCancellation_WhenTokenCancelled()
  {
    // Arrange
    var collection = new HandlerCollection<TestData>();
    var subscriber1 = new object();
    var subscriber2 = new object();
    var handler1Invoked = false;
    var handler2Invoked = false;
    var testData = new TestData { Message = "Test" };
    var cts = new CancellationTokenSource();

    collection.AddHandler(subscriber1, _ =>
    {
      handler1Invoked = true;
      cts.Cancel();
      return Task.CompletedTask;
    });
    collection.AddHandler(subscriber2, _ =>
    {
      handler2Invoked = true;
      return Task.CompletedTask;
    });

    // Act
    await collection.InvokeHandlers(testData, cts.Token);

    // Assert
    Assert.True(handler1Invoked);
    Assert.False(handler2Invoked);
  }

  private class TestData
  {
        public string Message { get; set; } = string.Empty;
    }
}
