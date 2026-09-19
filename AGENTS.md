# Repository Rules

Read `.agents/rules/nof-dev.md` when contributing to the NOF framework.

## Class Accessibility

- Classes should be `public` by default throughout this repository.
- Do not mark a class `internal` unless there is a compelling, concrete reason that requires assembly-only visibility. Document that reason alongside the declaration.
- Being an implementation detail, a background service, or used only inside the repository is not sufficient justification for `internal`.

## Local Environment

This machine has a special local environment. Run every command that invokes `dotnet` with elevated permissions.
