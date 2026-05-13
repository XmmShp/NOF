# NOF.Domain

Domain primitives package for the [NOF Framework](https://github.com/XmmShp/NOF).

## Overview

`NOF.Domain` currently provides the low-level domain building blocks that are shared across the framework:

- `IValueObject<T>` for source-generated value objects
- `[NewableValueObject]` for `long`-backed ID value objects
- `Failure` and `[Failure(...)]` for strongly-typed failure definitions
- `DomainException` and `ValidationException`
- `IIdGenerator` and `IdGenerator.Current`

This package does **not** currently expose aggregate root, repository, or unit-of-work abstractions.

## Key Abstractions

### `IValueObject<T>`

Implement `IValueObject<T>` on a `readonly partial struct` to define a value object. The source generator produces:

- a private constructor accepting the primitive value
- a static `Of(T)` factory method that calls `Validate(T)`
- an explicit cast operator to the primitive type
- equality members and `ToString()`
- a nested `JsonConverter`

The generated `JsonConverter` is AOT-friendly and resolves primitive serialization through `JsonTypeInfo`.
If you use Native AOT-style publishing, make sure the `JsonSerializerOptions` passed to serialization includes metadata for the primitive type backing the value object.

```csharp
using NOF.Domain;

public readonly partial struct CustomerName : IValueObject<string>
{
    public static void Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Customer name cannot be empty.");
        }
    }
}
```

### `[NewableValueObject]`

Apply `[NewableValueObject]` to a `readonly partial struct` implementing `IValueObject<long>` to generate a `New()` factory backed by `IdGenerator.Current`.

```csharp
using NOF.Domain;

[NewableValueObject]
public readonly partial struct OrderId : IValueObject<long>;
```

Before using `New()`, ensure the app has initialized an `IIdGenerator`. In NOF hosts this is typically handled by infrastructure defaults, which register `IIdGenerator` and run `IdGeneratorInitializationStep` during startup.

### `Failure` and `[Failure]`

Use `[Failure]` on a static partial class to declare strongly-typed domain failures. The source generator emits static members returning `Failure` instances.

```csharp
using NOF.Domain;

[Failure("NotFound", "Order not found", "404001")]
[Failure("AlreadyPaid", "Order has already been paid", "409001")]
public static partial class OrderFailures;
```

`Failure` can also be converted into exceptions when needed:

```csharp
OrderFailures.NotFound.ThrowAsDomainException();
```

### Exceptions

- `DomainException` represents a domain rule violation with an `ErrorCode`
- `ValidationException` is a `DomainException` specialization for validation failures

```csharp
throw new ValidationException("Customer name cannot be empty.");
```

## Installation

```shell
dotnet add package NOF.Domain
```

## License

Apache-2.0
