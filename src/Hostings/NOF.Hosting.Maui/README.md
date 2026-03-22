# NOF.Hosting.Maui

.NET MAUI hosting package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

Provides the .NET MAUI host integration for NOF applications, wrapping `MauiAppBuilder` with the NOF step pipeline for service registration and application initialization.

## Features

- **NOF Step Pipeline** â€?full support for `IServiceRegistrationStep` and `IApplicationInitializationStep`
- **`[AutoInject]` Support** â€?bundled source generators for automatic DI registration
- **Seamless Integration** â€?works with existing `MauiAppBuilder` configuration

## Usage

```csharp
public static MauiApp CreateMauiApp()
{
    var mauiBuilder = MauiApp.CreateBuilder();

    mauiBuilder
        .UseMauiApp<App>()
        .ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        });

    var builder = NOFMauiAppBuilder.Create(mauiBuilder);

    builder.WithAutoApplicationParts();

    return builder.BuildMauiAppAsync().GetAwaiter().GetResult();
}
```

## Installation

```shell
dotnet add package NOF.Hosting.Maui
```

## License

Apache-2.0

