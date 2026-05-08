---
description: How to run the full CI pipeline locally before pushing
---

# Run CI Locally

Replicate the GitHub Actions CI pipeline on your local machine.

1. Restore dependencies:
   ```bash
   dotnet restore NOF.slnx
   ```

2. Verify code formatting:
   ```bash
   dotnet format --verify-no-changes --verbosity diagnostic
   ```

3. Build the solution in Release mode:
   ```bash
   dotnet build NOF.slnx --configuration Release --no-restore
   ```

4. Run all tests:
   ```bash
   dotnet test NOF.slnx --configuration Release --no-build --verbosity normal --collect:"XPlat Code Coverage"
   ```

5. If formatting fails, auto-fix with:
   ```bash
   dotnet format
   ```
   Then review and commit the changes.
