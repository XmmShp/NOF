# NOF.Infrastructure

Unified infrastructure entry package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

`NOF.Infrastructure` provides a single integration entry for infrastructure capabilities by composing:

- `NOF.Hosting.Abstraction`
- `NOF.Application`

This lets consumers reference one package/project while still getting the full default infrastructure setup.

## Usage

Add a single reference to `NOF.Infrastructure` in host or infrastructure-adapter projects.

## Installation

```shell
dotnet add package NOF.Infrastructure
```

## License

Apache-2.0

