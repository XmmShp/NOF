# NOF.Hosting.Maui

.NET MAUI hosting package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the .NET MAUI host integration for NOF applications, wrapping `MauiAppBuilder` with NOF host abstractions and application initialization support.

## Features

- **Initialization Steps** - supports `IApplicationInitializationStep`
- **`[AutoInject]` Support** - bundled source generators for automatic DI registration
- **Seamless Integration** - works with existing `MauiAppBuilder` configuration

## Usage

```csharp
public static MauiApp CreateMauiApp()
{
    var builder = NOFMauiAppBuilder.Create();

    builder.MauiAppBuilder
        .UseMauiApp<App>()
        .ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        });

    var nofApp = builder.BuildAsync().GetAwaiter().GetResult();
    return nofApp.MauiApp;
}
```

`Create()` already adds the calling assembly as an application part. It does not apply `NOF.Infrastructure` defaults automatically. Use `AddApplicationPart(...)` only when you need to register additional assemblies.

## Installation

```shell
dotnet add package NOF.Hosting.Maui
```

## License

Apache-2.0
