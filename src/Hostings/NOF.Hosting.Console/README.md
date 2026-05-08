## NOF.Hosting.Console

Console hosting for the NOF Framework.

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
