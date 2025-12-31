# ControlR Data Redaction Library

A lightweight library for redacting sensitive data in logs and outputs using Microsoft's data compliance framework.

## Overview

This library provides tools to automatically redact sensitive information during logging operations, helping you maintain security and comply with data protection regulations. It uses Microsoft's `Microsoft.Extensions.Compliance.Redaction` package to seamlessly integrate with standard .NET logging infrastructure.

## Installation

```bash
dotnet add package ControlR.Libraries.DataRedaction
```

## Quick Start

### 1. Register the Redactor

Add the redactor to your service collection:

```csharp
using ControlR.Libraries.DataRedaction;

var builder = WebApplication.CreateBuilder(args);

// Add redaction services
builder.Services.AddStarRedactor();

var app = builder.Build();
```

### 2. Mark Sensitive Properties

Use the `ProtectedDataClassification` attribute to mark properties that should be redacted:

```csharp
public class User
{
  public string Name { get; set; }
  
  [ProtectedDataClassification]
  public string Password { get; set; }
  
  [ProtectedDataClassification]
  public string ApiKey { get; set; }
}
```

### 3. Log Structured Data

When you log objects with protected properties, they'll automatically be redacted:

```csharp
var user = new User 
{ 
  Name = "John Doe", 
  Password = "secret123",
  ApiKey = "sk_live_abc123"
};

logger.LogInformation("User details: {@User}", user);
// Output: User details: { Name: "John Doe", Password: "****", ApiKey: "****" }
```

## Features

- **Automatic Redaction**: Sensitive data is redacted automatically during logging
- **Attribute-Based**: Simple `[ProtectedDataClassification]` attribute to mark sensitive properties
- **Star Redactor**: Replaces sensitive values with `****`
- **Standard Integration**: Works with Microsoft's logging and compliance infrastructure
- **Zero Configuration**: Works out of the box with sensible defaults

## API Reference

### `AddStarRedactor()`

Registers the `StarRedactor` and enables redaction in the logging pipeline.

```csharp
services.AddStarRedactor();
```

### `ProtectedDataClassificationAttribute`

Marks a property as containing protected data that should be redacted.

```csharp
[ProtectedDataClassification]
public string SensitiveData { get; set; }
```

### `StarRedactor`

The default redactor that replaces sensitive values with `****`.

### `DefaultDataClassifications`

Provides standard data classification types:
- `Protected`: Data that should be redacted
- `Public`: Data that can be logged without redaction

## Advanced Usage

### Custom Data Classifications

You can use the built-in classifications for more granular control:

```csharp
using Microsoft.Extensions.Compliance.Classification;

public class CustomModel
{
  [DataClassification(DefaultDataClassifications.Public)]
  public string PublicInfo { get; set; }
  
  [DataClassification(DefaultDataClassifications.Protected)]
  public string PrivateInfo { get; set; }
}
```

## Requirements

- .NET 10.0 or later
- Microsoft.Extensions.Compliance.Redaction
- Microsoft.Extensions.Logging

## Learn More

For more information about data redaction in .NET, see the official documentation:
- [Data Redaction in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/data-redaction)

## License

MIT