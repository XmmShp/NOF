---
description: Run the repository's GitHub Actions build-and-test sequence locally
---

# Run CI Locally

The CI build-and-test job uses .NET 10, Release configuration, and `NOF.slnx` on Ubuntu and Windows. Windows installs the MAUI workload first.

## Windows Prerequisite

```bash
dotnet workload install maui
```

The project files use non-MAUI stubs on Linux. macOS builds use the MAUI targets and therefore also require the applicable workload.

## CI Sequence

```bash
dotnet restore NOF.slnx
dotnet format --verify-no-changes --verbosity diagnostic
dotnet build NOF.slnx --configuration Release --no-restore
dotnet test NOF.slnx --configuration Release --no-build --verbosity normal --collect:"XPlat Code Coverage"
```

GitHub runs the formatting check only on Linux, but contributors should run it on every platform.

If formatting fails:

```bash
dotnet format
```

Review the resulting changes, then rerun the verification sequence. The CD workflow repeats restore, format, build, and tests before packing projects discovered under `src/`.
