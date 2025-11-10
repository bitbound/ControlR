using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using ControlR.Libraries.Signalr.Client.Diagnostics;
using ControlR.Libraries.Signalr.Client.Exceptions;
using ControlR.Libraries.Signalr.Client.Internals;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.Signalr.Client;

/// <summary>
///   An abstraction over <see cref="HubConnection"/> that provides a strongly-typed
///   implementation of the server-side hub interface.
/// </summary>
/// <typeparam name="THub">
///   The interface that contains the client-invokable hub methods.
/// </typeparam>
public interface IHubConnection<out THub> : IAsyncDisposable
  where THub : class
{
  /// <summary>
  /// <para>
  ///   Occurs when the connection is closed. The connection could be closed due to an
  ///   error or due to either the server or client intentionally closing the connection
  ///   without error.
  /// </para>
  /// <para>
  ///   If this event was triggered from a connection error, the System.Exception that
  ///   occurred will be passed in as the sole argument to this handler. If this event
  ///   was triggered intentionally by either the client or server, then the argument
  ///   will be null.
  /// </para>
  /// </summary>
  event Func<Exception?, Task>? Closed;

  /// <summary>
  ///  Occurs when an exception is thrown inside the Connect method.
  /// </summary>
  event Func<Exception, Task>? ConnectThrew;

  ///<summary>
  ///<para>
  ///  Occurs when the Microsoft.AspNetCore.SignalR.Client.HubConnection successfully
  ///  reconnects after losing its underlying connection.
  ///</para>
  ///<para>
  ///  The System.String parameter will be the Microsoft.AspNetCore.SignalR.Client.HubConnection's
  ///  new ConnectionId or null if negotiation was skipped.
  ///</para>
  /// </summary>
  event Func<string?, Task>? Reconnected;

  /// <summary>
  /// <para>
  ///   Occurs when the Microsoft.AspNetCore.SignalR.Client.HubConnection starts reconnecting
  ///   after losing its underlying connection.
  /// </para>
  ///<para>
  ///   The System.Exception that occurred will be passed in as the sole argument to
  ///   this handler.
  ///</para>
  ///</summary>
  event Func<Exception?, Task>? Reconnecting;

  /// <summary>
  /// The current connection ID.
  /// </summary>
  string? ConnectionId { get; }

  /// <summary>
  /// The current connection state.
  /// </summary>
  HubConnectionState ConnectionState { get; }

  /// <summary>
  /// Indicates whether the connection is currently connected.
  /// </summary>
  bool IsConnected { get; }

  /// <summary>
  ///   An implementation of the server-side hub interface.  Invoking
  ///   methods on this will invoke the corresponding methods on the server.
  /// </summary>
  THub Server { get; }

  /// <summary>
  /// <para>
  ///   Attempts to connect to the given hub endpoint.  If <paramref name="autoRetry"/> is
  ///   true, this method will only return when the connection is successful or the
  ///   cancellation token is triggered.
  /// </para>
  /// <para>
  ///   Once connected, an internal retry policy will handle reconnections if the
  ///   connection is lost.
  /// </para>
  /// </summary>
  /// <param name="hubEndpoint"></param>
  /// <param name="autoRetry"></param>
  /// <param name="configure"></param>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  Task<bool> Connect(
    Uri hubEndpoint,
    bool autoRetry,
    Action<HttpConnectionOptions>? configure = null,
    CancellationToken cancellationToken = default);

  /// <summary>
  ///   Sends a message to the server without waiting for a response.
  /// </summary>
  /// <param name="methodName"></param>
  /// <param name="cancellationToken"></param>
  /// <param name="args"></param>
  /// <returns>
  ///   A task that will complete as soon as the message is queued,
  ///   not when the server receives or handles it.
  /// </returns>
  Task Send(
     string methodName,
     object?[] args,
     CancellationToken cancellationToken = default);

  /// <summary>
  /// Waits for the connection to be established or reconnected.
  /// </summary>
  /// <param name="cancellationToken"></param>
  /// <returns></returns>
  Task WaitForConnected(CancellationToken cancellationToken);
}

/// <inheritdoc />
internal sealed class HubConnection<THub, TClient>(
  IServiceProvider serviceProvider,
  ILogger<HubConnection<THub, TClient>> logger) : IHubConnection<THub>
  where TClient : class
  where THub : class
{
  private const BindingFlags BindingFlags =
    System.Reflection.BindingFlags.Public |
    System.Reflection.BindingFlags.Instance |
    System.Reflection.BindingFlags.DeclaredOnly;

  private readonly SemaphoreSlim _connectLock = new(1, 1);

  private readonly ConcurrentBag<IDisposable> _handlerRegistrations = [];
  private readonly ILogger<HubConnection<THub, TClient>> _logger = logger;
  private readonly TimeSpan _maxReconnectDelay = TimeSpan.FromSeconds(30);
  private readonly TimeSpan _maxReconnectJitter = TimeSpan.FromSeconds(20);
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private HubConnection? _connection;
  private dynamic? _hubProxy;

  public event Func<Exception?, Task>? Closed;

  public event Func<Exception, Task>? ConnectThrew;

  public event Func<string?, Task>? Reconnected;

  public event Func<Exception?, Task>? Reconnecting;

  public string? ConnectionId => _connection?.ConnectionId;
  public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;
  public bool IsConnected => ConnectionState == HubConnectionState.Connected;
  public THub Server => _hubProxy ?? throw new InvalidOperationException("Hub connection has not been initialized.");

  internal HubConnection Connection => _connection ?? throw new InvalidOperationException("Hub connection has not been initialized.");

  public async Task<bool> Connect(
    Uri hubEndpoint,
    bool autoRetry,
    Action<HttpConnectionOptions>? configure = null,
    CancellationToken cancellationToken = default)
  {
    var retryCount = 0;
    while (true)
    {
      using var connectActivity = DefaultActivitySource.StartActivity("Connecting to hub.");

      await _connectLock.WaitAsync(cancellationToken);

      try
      {
        if (ConnectionState == HubConnectionState.Connected)
        {
          return true;
        }

        _logger.LogInformation("Connecting to SignalR hub ({HubEndpoint}).", hubEndpoint);

        if (_connection is not null)
        {
          try
          {
            await _connection.StopAsync(cancellationToken);
          }
          catch (Exception ex)
          {
            // This can happen if the connection is already stopped.  We could check the
            // state first, but it might change in between the check and the call to StopAsync.
            _logger.LogInformation(ex, "Exception while stopping connection.  This is probably normal.");
          }

          await _connection.DisposeAsync();
          _connection = null;
        }

        using (DefaultActivitySource.StartActivity("Generating hub proxy."))
        {
          _hubProxy = HubProxyGenerator.CreateProxy<THub>(new ProxyInvocationHandler<THub, TClient>(this));
        }

        _connection = BuildConnection(hubEndpoint, configure);

        using (DefaultActivitySource.StartActivity("Binding client interface."))
        {
          BindClientInterface(_connection);
        }

        await _connection.StartAsync(cancellationToken);

        _logger.LogInformation("Connection successful.");

        return true;
      }
      catch (DynamicObjectGenerationException)
      {
        throw;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to initialize hub connection.");
        try
        {
          ConnectThrew?.Invoke(ex);
        }
        catch
        {
          // ignored
        }
      }
      finally
      {
        _connectLock.Release();
      }

      if (!autoRetry)
      {
        break;
      }

      retryCount++;
      var delay = GetNextRetryDelay(retryCount);
      _logger.LogInformation("Retrying connection in {WaitSeconds} seconds.", delay.TotalSeconds);
      await Task.Delay(delay, cancellationToken);
    }
    return ConnectionState == HubConnectionState.Connected;
  }

  public async ValueTask DisposeAsync()
  {
    foreach (var registration in _handlerRegistrations)
    {
      registration.Dispose();
    }

    if (_connection is not null)
    {
      await _connection.DisposeAsync();
    }
  }

  public async Task Send(
    string methodName,
    object?[] args,
    CancellationToken cancellationToken = default)
  {
    await WaitForConnected(cancellationToken);
    await Connection.SendCoreAsync(methodName, args, cancellationToken);
  }

  public async Task WaitForConnected(CancellationToken cancellationToken)
  {
    while (ConnectionState != HubConnectionState.Connected)
    {
      cancellationToken.ThrowIfCancellationRequested();
      await Task.Delay(100, cancellationToken);
    }
  }

  private static List<MethodInfo> GetMethodsRecursively(Type type, List<MethodInfo>? methods = null)
  {
    methods ??= [];
    methods.AddRange(type.GetMethods(BindingFlags));

    foreach (var interfaceType in type.GetInterfaces())
    {
      methods.AddRange(interfaceType.GetMethods(BindingFlags));
    }

    return methods;
  }

  private void BindClientInterface(HubConnection connection)
  {
    var client = _serviceProvider.GetRequiredService<TClient>();
    var clientMethods = GetMethodsRecursively(typeof(TClient));

    foreach (var method in clientMethods)
    {
      var parameters = method.GetParameters();
      var paramTypes = parameters.Length > 0 ?
        parameters.Select(x => x.ParameterType).ToArray() :
        [];

      IDisposable registration;

      if (method.ReturnType == typeof(Task))
      {
        registration = connection.On(
          methodName: method.Name,
          parameterTypes: paramTypes,
          handler: (args) => InvokeVoidClientMethod(client, method, args));
      }
      else if (method.ReturnType.IsGenericType &&
          method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
      {
        registration = connection.On(
          methodName: method.Name,
          parameterTypes: paramTypes,
          handler: args => InvokeGenericClientMethod(client, method, args));
      }
      else
      {
        throw new DynamicObjectGenerationException(
          $"Invalid client method return type: {method.ReturnType.Name}. " +
          "Client methods must return Task or Task<T>.");
      }

      _handlerRegistrations.Add(registration);
    }
  }

  private HubConnection BuildConnection(Uri hubEndpoint, Action<HttpConnectionOptions>? configure)
  {
    var builder = _serviceProvider
      .GetRequiredService<IHubConnectionBuilder>()
      .WithAutomaticReconnect(new RetryPolicy(this, _logger));

    if (configure is not null)
    {
      builder.WithUrl(hubEndpoint, configure);
    }
    else
    {
      builder.WithUrl(hubEndpoint);
    }

    var connection = builder.Build();

    connection.Closed += HandleClosed;
    connection.Reconnected += HandleReconnected;
    connection.Reconnecting += HandleReconnecting;
    return connection;
  }
  private TimeSpan GetNextRetryDelay(long retryCount)
  {
    var waitSeconds = Math.Min(Math.Pow(retryCount, 2), _maxReconnectDelay.TotalSeconds);
    var jitterMs = RandomNumberGenerator.GetInt32(0, (int)_maxReconnectJitter.TotalMilliseconds);
    var waitTime = TimeSpan.FromSeconds(waitSeconds) + TimeSpan.FromMilliseconds(jitterMs);
    return waitTime;
  }

  private async Task HandleClosed(Exception? exception)
  {
    try
    {
      if (Closed is not null)
      {
        await Closed.Invoke(exception);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error in Closed event handler.");
    }
  }

  private async Task HandleReconnected(string? arg)
  {
    try
    {
      if (Reconnected is not null)
      {
        await Reconnected.Invoke(arg);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error in Reconnected event handler.");
    }
  }

  private async Task HandleReconnecting(Exception? exception)
  {
    try
    {
      if (Reconnecting is not null)
      {
        await Reconnecting.Invoke(exception);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error in Reconnecting event handler.");
    }
  }

  private async Task<object?> InvokeGenericClientMethod(TClient client, MethodInfo method, object?[]? args)
  {
    try
    {
      var returnValue = method.Invoke(client, args);
      if (returnValue is Task returnTask)
      {
        await returnTask;
        return ((dynamic)returnTask).Result;
      }
      else
      {
        _logger.LogWarning(
          "Unexpected return type from client method: {ReturnValueType}.",
          returnValue?.GetType().Name);
      }
      return returnValue;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking client method.");
    }
    return null;
  }

  private async Task<object?> InvokeVoidClientMethod(TClient client, MethodInfo method, object?[]? args)
  {
    try
    {
      var returnValue = method.Invoke(client, args);
      if (returnValue is Task returnTask)
      {
        await returnTask;
      }
      else
      {
        _logger.LogWarning(
          "Unexpected return type from client method: {ReturnValueType}.",
          returnValue?.GetType().Name);
      }
      return returnValue;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while invoking client method.");
    }
    return null;
  }
  private sealed class RetryPolicy(
    HubConnection<THub, TClient> hubConnection,
    ILogger<HubConnection<THub, TClient>> logger) : IRetryPolicy
  {
    private readonly HubConnection<THub, TClient> _hubConnection = hubConnection;
    private readonly ILogger<HubConnection<THub, TClient>> _logger = logger;

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
      var waitTime = _hubConnection.GetNextRetryDelay(retryContext.PreviousRetryCount);

      _logger.LogInformation("Retrying connection in {WaitSeconds} seconds.", waitTime.TotalSeconds);

      return waitTime;
    }
  }
}
