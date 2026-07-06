## NOF.Hosting.Console

Console hosting for the NOF Framework.

`Create(...)` already adds the calling assembly as an application part and applies `NOF.Infrastructure` defaults.

### Usage

```csharp
using NOF.Hosting.Console;

var builder = NOFConsoleHostBuilder.Create(args);

// Add NOF steps here:
// builder.AddRegistrationStep(...);
// builder.AddInitializationStep(...);

using var app = await builder.BuildAsync();
await app.RunAsync();
```
