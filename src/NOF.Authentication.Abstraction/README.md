# NOF.Authentication.Abstraction

Authentication-related execution context abstractions for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

This package defines outbound authentication context hints shared by `NOF.Application`, `NOF.Hosting`, and `NOF.Infrastructure`.

It currently contains:

- `WithServiceToken(...)`
- `GetServiceTokenHeaderName()`
- `WithTokenExchange(...)`
- `GetTokenExchangeHeaderNames()`
- `AuthenticationContextKeys`

These APIs intentionally do not live in `NOF.Contract`, because they are runtime execution directives rather than contract declarations.

## Installation

```shell
dotnet add package NOF.Authentication.Abstraction
```

## License

Apache-2.0
