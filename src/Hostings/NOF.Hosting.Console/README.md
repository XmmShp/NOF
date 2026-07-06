## NOF.Hosting.Console

Console hosting for the NOF Framework.

`Create(...)` already adds the calling assembly as an application part and applies `NOF.Infrastructure` defaults.

### Usage

```csharp
using NOF.Hosting.Console;

var builder = NOFConsoleHostBuilder.Create(args);

// Add NOF services or initialization steps here:
// builder.Services.AddNOFHosting();
// builder.Services.AddInitializationStep(...);

using var app = await builder.BuildAsync();
await app.RunAsync();
```
