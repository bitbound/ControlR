using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Channels;
using ControlR.Libraries.Signalr.Client.Exceptions;

namespace ControlR.Libraries.Signalr.Client.Internals;

/// <summary>
/// Generates a dynamic proxy that implements the specified interface.
/// This is specifically tailored for client-invokable SignalR hub methods.
/// </summary>
internal static class HubProxyGenerator
{
  public static THub CreateProxy<THub>(IInvocationHandler handler)
    where THub : class
  {
    var interfaceType = typeof(THub);
    if (!interfaceType.IsInterface)
      throw new DynamicObjectGenerationException("T must be an interface type.");

    if (handler == null)
      throw new DynamicObjectGenerationException("Handler cannot be null.");

    var assemblyName = new AssemblyName("AsyncDynamicAssembly");
    var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
    var moduleBuilder = assemblyBuilder.DefineDynamicModule("AsyncDynamicModule");

    var typeBuilder = moduleBuilder.DefineType("AsyncDynamicType",
        TypeAttributes.Public | TypeAttributes.Class,
        null,
        [interfaceType]);

    var handlerField = typeBuilder.DefineField("_handler", typeof(IInvocationHandler), FieldAttributes.Private);

    // Constructor
    var ctor = typeBuilder.DefineConstructor(
        MethodAttributes.Public,
        CallingConventions.Standard,
        [typeof(IInvocationHandler)]);

    var ctorIL = ctor.GetILGenerator();
    ctorIL.Emit(OpCodes.Ldarg_0);
    ctorIL.Emit(
      OpCodes.Call,
      typeof(object).GetConstructor(Type.EmptyTypes)
        ?? throw new DynamicObjectGenerationException("Object constructor not found."));
    ctorIL.Emit(OpCodes.Ldarg_0);
    ctorIL.Emit(OpCodes.Ldarg_1);
    ctorIL.Emit(OpCodes.Stfld, handlerField);
    ctorIL.Emit(OpCodes.Ret);

    // Implement interface methods
    foreach (var method in interfaceType.GetMethods())
    {
      ImplementMethod(typeBuilder, method, handlerField);
    }

    var proxyType = typeBuilder.CreateType();
    var instance = Activator.CreateInstance(proxyType, handler)
      ?? throw new DynamicObjectGenerationException("Failed to create instance of proxy type.");

    return (THub)instance;
  }

  private static DynamicObjectGenerationException GetInvalidReturnTypeEx(Type returnType)
  {
    return new DynamicObjectGenerationException(
          $"Unsupported method return type: {returnType}.  Methods must return " +
          "Task, Task<T>, ValueTask<T>, IAsyncEnumerable<T>, or ChannelReader<T>.");
  }

  private static void ImplementMethod(TypeBuilder typeBuilder, MethodInfo method, FieldBuilder handlerField)
  {
    var parameters = method.GetParameters();
    var parameterTypes = Array.ConvertAll(parameters, p => p.ParameterType);
  var hasClientToServerStreamParam = parameters.Any(p => IsClientToServerStreamParam(p.ParameterType));

    var methodBuilder = typeBuilder.DefineMethod(
        method.Name,
        MethodAttributes.Public | MethodAttributes.Virtual,
        method.ReturnType,
        parameterTypes);

    var methodIL = methodBuilder.GetILGenerator();

    // Load 'this' and handler field
    methodIL.Emit(OpCodes.Ldarg_0);
    methodIL.Emit(OpCodes.Ldfld, handlerField);

    // Load method info
    methodIL.Emit(OpCodes.Ldtoken, method);
    methodIL.Emit(
      OpCodes.Call,
      typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle)])
        ?? throw new DynamicObjectGenerationException("GetMethodFromHandle method not found."));

    // Create and load parameters array
    methodIL.Emit(OpCodes.Ldc_I4, parameters.Length);
    methodIL.Emit(OpCodes.Newarr, typeof(object));

    for (var i = 0; i < parameters.Length; i++)
    {
      methodIL.Emit(OpCodes.Dup);
      methodIL.Emit(OpCodes.Ldc_I4, i);
      methodIL.Emit(OpCodes.Ldarg, i + 1);
      if (parameterTypes[i].IsValueType)
      {
        methodIL.Emit(OpCodes.Box, parameterTypes[i]);
      }
      methodIL.Emit(OpCodes.Stelem_Ref);
    }

    // Call the appropriate method on the handler based on the return type

    if (method.ReturnType == typeof(Task))
    {
      // If the method has a client-to-server streaming parameter we must use SendAsync.
      var targetMethodName = hasClientToServerStreamParam ? nameof(IInvocationHandler.SendAsync) : nameof(IInvocationHandler.InvokeVoidAsync);
      methodIL.Emit(
        OpCodes.Callvirt,
        typeof(IInvocationHandler)
          .GetMethod(targetMethodName)
          ?? throw new DynamicObjectGenerationException($"{targetMethodName} method not found on proxy implementation."));
    }
    else if (method.ReturnType == typeof(void))
    {
      if (!hasClientToServerStreamParam)
      {
        throw GetInvalidReturnTypeEx(method.ReturnType); // We only allow void when streaming params are present.
      }
      // Wrap SendAsync(Task) and ignore result; emit call then return.
      methodIL.Emit(
        OpCodes.Callvirt,
        typeof(IInvocationHandler)
          .GetMethod(nameof(IInvocationHandler.SendAsync))
          ?? throw new DynamicObjectGenerationException("SendAsync method not found on proxy implementation."));
      // Discard the returned Task (fire-and-forget). Optionally could Wait but that's discouraged.
      methodIL.Emit(OpCodes.Pop);
    }
    else if (method.ReturnType.IsGenericType)
    {
      var methodName = method.ReturnType switch
      {
        var x when IsAsyncEnumerable(x) => nameof(IInvocationHandler.Stream),
        var x when IsChannelReader(x) => nameof(IInvocationHandler.InvokeChannel),
        var x when IsValueTask(x) => nameof(IInvocationHandler.InvokeValueTaskAsync),
        var x when IsGenericTask(x) => nameof(IInvocationHandler.InvokeAsync),
        _ => throw GetInvalidReturnTypeEx(method.ReturnType)
      };

      methodIL.Emit(
        OpCodes.Callvirt,
        typeof(IInvocationHandler)
          .GetMethod(methodName)
          ?.MakeGenericMethod(method.ReturnType.GetGenericArguments()[0])
            ?? throw new DynamicObjectGenerationException($"{methodName} method not found on proxy implementation."));
    }
    else
    {
      throw GetInvalidReturnTypeEx(method.ReturnType);
    }

    methodIL.Emit(OpCodes.Ret);

    typeBuilder.DefineMethodOverride(methodBuilder, method);
  }

  private static bool IsAsyncEnumerable(Type returnType)
  {
    return returnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>);
  }
  private static bool IsClientToServerStreamParam(Type paramType)
  {
    return paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>);
  }

  private static bool IsChannelReader(Type returnType)
  {
    return returnType.GetGenericTypeDefinition() == typeof(ChannelReader<>);
  }

  private static bool IsGenericTask(Type returnType)
  {
    return returnType.GetGenericTypeDefinition() == typeof(Task<>);
  }

  private static bool IsValueTask(Type returnType)
  {
    return returnType.GetGenericTypeDefinition() == typeof(ValueTask<>);
  }
}
