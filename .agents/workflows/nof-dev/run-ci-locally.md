---
description: How to run the full CI pipeline locally before pushing
---

# Run CI Locally

Replicate the GitHub Actions CI pipeline on your local machine.

// turbo
1. Restore dependencies:
   ```bash
   dotnet restore
   ```

// turbo
2. Verify code formatting:
   ```bash
   dotnet format --verify-no-changes --verbosity diagnostic
   ```

// turbo
3. Build the solution in Release mode:
   ```bash
   dotnet build --configuration Release --no-restore
   ```

// turbo
4. Run all tests:
   ```bash
   dotnet test --configuration Release --no-build --verbosity normal
   ```

5. If formatting fails, auto-fix with:
   ```bash
   dotnet format
   ```
   Then review and commit the changes.
