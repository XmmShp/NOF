## NOF.Hosting.Console

Console hosting for the NOF Framework.

### Usage

```csharp
using NOF.Hosting.Console;

var builder = NOFConsoleHostBuilder.Create(args);

// Add NOF steps here:
// builder.AddRegistrationStep(...);
// builder.AddInitializationStep(...);

await builder.RunAsync();
```

