using MessagePack;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ControlR.Libraries.Ipc;

public interface ICallbackStore
{
  CallbackToken Add(Type contentType, Action<object> callback);

  CallbackToken Add(Func<object, object> handler, Type contentType, Type returnType);

  Task InvokeActions(MessageWrapper wrapper);

  Task InvokeFuncs(MessageWrapper wrapper, Func<MessageWrapper, Task> responseFunc);

  bool TryRemove(Type type, CallbackToken token);

  bool TryRemoveAll(Type type);
}

internal class CallbackStore(IContentTypeResolver contentTypeResolver, ILogger<CallbackStore> logger) : ICallbackStore
{
  private readonly ConcurrentDictionary<Type, List<IpcAction>> _actions = new();
  private readonly SemaphoreSlim _actionsLock = new(1, 1);
  private readonly ConcurrentDictionary<Type, List<IpcFunc>> _funcs = new();
  private readonly SemaphoreSlim _funcsLock = new(1, 1);
  private readonly ILogger<CallbackStore> _logger = logger;

  public CallbackToken Add(Type contentType, Action<object> callback)
  {
    try
    {
      _actionsLock.Wait();
      var token = new CallbackToken();
      var action = new IpcAction(contentType, callback, token);
      _actions.AddOrUpdate(contentType, [action], (_, v) =>
      {
        v.Add(action);
        return v;
      });
      return token;
    }
    finally
    {
      _actionsLock.Release();
    }
  }

  public CallbackToken Add(Func<object, object> handler, Type contentType, Type returnType)
  {
    try
    {
      _funcsLock.Wait();
      var token = new CallbackToken();
      var func = new IpcFunc(handler, contentType, returnType, token);
      _funcs.AddOrUpdate(contentType, [func], (_, v) =>
      {
        v.Add(func);
        return v;
      });
      return token;
    }
    finally
    {
      _funcsLock.Release();
    }
  }

  public async Task InvokeActions(MessageWrapper wrapper)
  {
    var contentType = contentTypeResolver.ResolveType(wrapper.ContentTypeName);
    if (contentType is null)
    {
      return;
    }

    try
    {
      await _actionsLock.WaitAsync();

      if (!_actions.TryGetValue(contentType, out var actions))
      {
        return;
      }

      foreach (var callback in actions)
      {
        if (callback.ContentType == contentType)
        {
          var content = MessagePackSerializer.Deserialize(contentType, wrapper.Content);
          if (content is null)
          {
            _logger.LogError("Failed to deserialize message wrapper.");
            return;
          }
          callback.Action.Invoke(content);
        }
      }
    }
    finally
    {
      _actionsLock.Release();
    }
  }

  public async Task InvokeFuncs(MessageWrapper wrapper, Func<MessageWrapper, Task> responseFunc)
  {
    var contentType = contentTypeResolver.ResolveType(wrapper.ContentTypeName);
    if (contentType is null)
    {
      return;
    }

    try
    {
      await _funcsLock.WaitAsync();

      if (!_funcs.TryGetValue(contentType, out var funcs))
      {
        return;
      }

      foreach (var func in funcs)
      {
        object? result = null;
        var returnType = func.ReturnType;

        if (func.ContentType == contentType)
        {
          var content = MessagePackSerializer.Deserialize(contentType, wrapper.Content);
          if (content is null)
          {
            _logger.LogError("Failed to deserialize message wrapper.");
            return;
          }
          result = func.Handler?.Invoke(content);
        }

        if (result is null)
        {
          _logger.LogError("Handler result is null.");
          return;
        }

        if (result is Task task)
        {
          // Await completion
          await task;

          var rt = result.GetType();
          if (rt.IsGenericType && rt.GetGenericTypeDefinition() == typeof(Task<>))
          {
            // Read Task<T>.Result via reflection
            var value = rt.GetProperty("Result")!.GetValue(result);
            if (value is null)
            {
              _logger.LogError("Handler result is null.");
              return;
            }

            result = value;
            returnType = value.GetType();

            // do something with the extracted value
          }
          else
          {
            // non-generic Task (Task with no result)
            returnType = typeof(void);
            // handle as needed
          }
        }

        var responseWrapper = new MessageWrapper(
            returnType,
            result,
            wrapper.Id);

        await responseFunc.Invoke(responseWrapper);
      }
    }
    finally
    {
      _funcsLock.Release();
    }
  }

  public bool TryRemove(Type type, CallbackToken token)
  {
    try
    {
      var found = 0;
      _funcsLock.Wait();
      _actionsLock.Wait();

      if (_funcs.TryGetValue(type, out var funcs))
      {
        found += funcs.RemoveAll(x => x.CallbackToken.Equals(token));
      }

      if (_actions.TryGetValue(type, out var actions))
      {
        found += actions.RemoveAll(x => x.CallbackToken.Equals(token));
      }

      return found > 0;
    }
    finally
    {
      _funcsLock.Release();
      _actionsLock.Release();
    }
  }

  public bool TryRemoveAll(Type type)
  {
    try
    {
      var found = false;
      _funcsLock.Wait();
      _actionsLock.Wait();

      found |= _funcs.TryRemove(type, out _);
      found |= _actions.TryRemove(type, out _);

      return found;
    }
    finally
    {
      _funcsLock.Release();
      _actionsLock.Release();
    }
  }

  private class IpcAction(Type contentType, Action<object> action, CallbackToken callbackToken)
  {
    public Action<object> Action { get; } = action;
    public CallbackToken CallbackToken { get; } = callbackToken;
    public Type ContentType { get; } = contentType;
  }

  private class IpcFunc
  {
    public IpcFunc(Func<object, object> handler, Type contentType, Type returnType, CallbackToken callbackToken)
    {
      ContentType = contentType;
      Handler = handler;
      ReturnType = returnType;
      CallbackToken = callbackToken;
    }
    
    public CallbackToken CallbackToken { get; }
    public Type? ContentType { get; }
    public Func<object, object>? Handler { get; }
    public Type ReturnType { get; }
  }
}