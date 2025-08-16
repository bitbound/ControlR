using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix.MacOs;

public static class Foundation
{
  private const string FoundationFramework = "/System/Library/Frameworks/Foundation.framework/Foundation";

  [DllImport(FoundationFramework, EntryPoint = "CFDictionaryCreate")]
  public static extern nint CFDictionaryCreate(
    nint allocator, 
    nint[] keys, 
    nint[] values, 
    nint numValues, 
    nint keyCallbacks, 
    nint valueCallbacks);

  [DllImport(FoundationFramework, EntryPoint = "CFRelease")]
  public static extern void CFRelease(nint cf);

  [DllImport(FoundationFramework, EntryPoint = "CFStringCreateWithCString")]
  public static extern nint CFStringCreateWithCString(nint allocator, string cStr, uint encoding);

  // Get pointers to default callback structures
  public static nint GetKCFTypeDictionaryKeyCallBacks()
  {
    if (NativeLibrary.TryGetExport(NativeLibrary.Load(FoundationFramework), "kCFTypeDictionaryKeyCallBacks", out var address))
    {
      return address;
    }
    return nint.Zero;
  }

  public static nint GetKCFTypeDictionaryValueCallBacks()
  {
    if (NativeLibrary.TryGetExport(NativeLibrary.Load(FoundationFramework), "kCFTypeDictionaryValueCallBacks", out var address))
    {
      return address;
    }
    return nint.Zero;
  }

  [DllImport(FoundationFramework, EntryPoint = "CFBooleanGetValue")]
  public static extern bool CFBooleanGetValue(nint boolean);

  // Get kCFBooleanTrue - the proper way to access this constant
  public static nint GetKCFBooleanTrue()
  {
    // Use NativeLibrary to get the symbol
    try
    {
      var handle = NativeLibrary.Load(FoundationFramework);
      if (NativeLibrary.TryGetExport(handle, "kCFBooleanTrue", out var address))
      {
        return Marshal.ReadIntPtr(address);
      }
    }
    catch (Exception)
    {
      // Fallback: try a different approach or return null
    }
    
    // If we can't get the symbol, try creating a CFBoolean directly
    return CFBooleanCreate(nint.Zero, true);
  }

  [DllImport(FoundationFramework, EntryPoint = "CFBooleanCreate")]
  public static extern nint CFBooleanCreate(nint allocator, bool value);

  // Simplified approach using CFDictionaryCreateMutable
  [DllImport(FoundationFramework, EntryPoint = "CFDictionaryCreateMutable")]
  public static extern nint CFDictionaryCreateMutable(nint allocator, nint capacity, nint keyCallBacks, nint valueCallBacks);

  [DllImport(FoundationFramework, EntryPoint = "CFDictionarySetValue")]
  public static extern void CFDictionarySetValue(nint theDict, nint key, nint value);

  // Create a CFDictionary with the prompt option for accessibility permission
  public static nint CreateAccessibilityPromptDictionary()
  {
    nint promptKey = nint.Zero;
    nint dict = nint.Zero;

    try
    {
      // Create a mutable dictionary with proper callback structures
      var keyCallbacks = GetKCFTypeDictionaryKeyCallBacks();
      var valueCallbacks = GetKCFTypeDictionaryValueCallBacks();
      
      dict = CFDictionaryCreateMutable(nint.Zero, 0, keyCallbacks, valueCallbacks);
      if (dict == nint.Zero)
      {
        throw new InvalidOperationException("Failed to create mutable CFDictionary");
      }

      // Create CFString for the key "AXTrustedCheckOptionPrompt"
      promptKey = CFStringCreateWithCString(nint.Zero, "AXTrustedCheckOptionPrompt", 0x08000100); // kCFStringEncodingUTF8
      if (promptKey == nint.Zero)
      {
        throw new InvalidOperationException("Failed to create CFString for prompt key");
      }

      var trueValue = GetKCFBooleanTrue();
      if (trueValue == nint.Zero)
      {
        throw new InvalidOperationException("Failed to get kCFBooleanTrue value");
      }

      // Set the key-value pair in the dictionary
      CFDictionarySetValue(dict, promptKey, trueValue);

      // Clean up the key string AFTER setting it in the dictionary (dictionary retains it)
      CFRelease(promptKey);
      promptKey = nint.Zero; // Prevent double-release

      return dict;
    }
    catch (Exception ex)
    {
      // Clean up on error
      if (promptKey != nint.Zero)
        CFRelease(promptKey);
      if (dict != nint.Zero)
        CFRelease(dict);
      
      throw new InvalidOperationException($"Failed to create accessibility prompt dictionary: {ex.Message}", ex);
    }
  }
}
