using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

namespace NOF.SourceGenerator.Shared;

internal enum ValueObjectLengthBuilderFamily
{
    NofInfrastructure,
    EntityFrameworkCore,
}

internal static class ValueObjectLengthConfigurationAnalysis
{
    private const string ValueObjectInterfaceMetadataName = "NOF.Domain.IValueObject<T>";
    private const string LengthAttributeMetadataName = "NOF.Domain.ValueObjectLengthAttribute";

    public static readonly DiagnosticDescriptor RedundantConfiguration = new(
        id: "NOF306",
        title: "Remove redundant value-object maximum length configuration",
        messageFormat: "Value object '{0}' already declares maximum length {1}; remove the redundant HasMaxLength({1}) configuration",
        category: "NOF.Infrastructure",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConflictingConfiguration = new(
        id: "NOF307",
        title: "Value-object maximum length configuration conflicts with its domain constraint",
        messageFormat: "Configured maximum length {1} conflicts with value object '{0}', which declares maximum length {2}; remove the explicit HasMaxLength configuration",
        category: "NOF.Infrastructure",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingDomainConstraint = new(
        id: "NOF308",
        title: "Declare value-object length in the domain layer",
        messageFormat: "Property type '{0}' configures maximum length {1} in infrastructure but does not declare [ValueObjectLength]; move the constraint to the value object",
        category: "NOF.Infrastructure",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [RedundantConfiguration, ConflictingConfiguration, MissingDomainConstraint];

    public static void AnalyzeInvocation(OperationAnalysisContext context, ValueObjectLengthBuilderFamily builderFamily)
    {
        if (context.Operation is not IInvocationOperation invocation
            || invocation.TargetMethod.Name != "HasMaxLength"
            || !IsSupportedMethod(invocation.TargetMethod, builderFamily))
        {
            return;
        }

        var lengthArgument = invocation.Arguments
            .FirstOrDefault(static argument => argument.Parameter?.Name == "maxLength");
        if (lengthArgument?.Value.ConstantValue is not { HasValue: true, Value: int configuredLength })
        {
            return;
        }

        var builderType = invocation.Instance?.Type ?? invocation.TargetMethod.ContainingType;
        if (!TryGetPropertyType(builderType, builderFamily, out var propertyType)
            || !TryGetStringValueObject(propertyType, out var valueObjectType))
        {
            return;
        }

        var displayName = valueObjectType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (!TryGetDeclaredMaximumLength(valueObjectType, out var declaredLength))
        {
            if (configuredLength <= 0)
            {
                return;
            }

            var properties = ImmutableDictionary<string, string?>.Empty
                .Add("ValueObjectType", valueObjectType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .Add("ConfiguredLength", configuredLength.ToString(CultureInfo.InvariantCulture));
            context.ReportDiagnostic(Diagnostic.Create(
                MissingDomainConstraint,
                lengthArgument.Value.Syntax.GetLocation(),
                properties,
                displayName,
                configuredLength));
            return;
        }

        var descriptor = configuredLength == declaredLength
            ? RedundantConfiguration
            : ConflictingConfiguration;
        var arguments = configuredLength == declaredLength
            ? new object[] { displayName, declaredLength }
            : [displayName, configuredLength, declaredLength];

        context.ReportDiagnostic(Diagnostic.Create(
            descriptor,
            lengthArgument.Value.Syntax.GetLocation(),
            arguments));
    }

    private static bool IsSupportedMethod(IMethodSymbol method, ValueObjectLengthBuilderFamily builderFamily)
    {
        var containingType = (method.ReducedFrom ?? method).ContainingType.OriginalDefinition;
        var @namespace = containingType.ContainingNamespace.ToDisplayString();

        return builderFamily switch
        {
            ValueObjectLengthBuilderFamily.NofInfrastructure =>
                containingType.Name == "IDbPropertyBuilder"
                && containingType.Arity == 2
                && @namespace == "NOF.Infrastructure",
            ValueObjectLengthBuilderFamily.EntityFrameworkCore =>
                containingType.Name == "PropertyBuilder"
                && containingType.Arity is 0 or 1
                && @namespace == "Microsoft.EntityFrameworkCore.Metadata.Builders",
            _ => false,
        };
    }

    private static bool TryGetPropertyType(
        ITypeSymbol? builderType,
        ValueObjectLengthBuilderFamily builderFamily,
        out ITypeSymbol propertyType)
    {
        if (builderType is INamedTypeSymbol namedBuilderType)
        {
            foreach (var candidate in EnumerateTypeHierarchy(namedBuilderType))
            {
                var originalDefinition = candidate.OriginalDefinition;
                var @namespace = originalDefinition.ContainingNamespace.ToDisplayString();
                if (builderFamily == ValueObjectLengthBuilderFamily.NofInfrastructure
                    && originalDefinition.Name == "IDbPropertyBuilder"
                    && originalDefinition.Arity == 2
                    && @namespace == "NOF.Infrastructure")
                {
                    propertyType = candidate.TypeArguments[1];
                    return true;
                }

                if (builderFamily == ValueObjectLengthBuilderFamily.EntityFrameworkCore
                    && originalDefinition.Name == "PropertyBuilder"
                    && originalDefinition.Arity == 1
                    && @namespace == "Microsoft.EntityFrameworkCore.Metadata.Builders")
                {
                    propertyType = candidate.TypeArguments[0];
                    return true;
                }
            }
        }

        propertyType = null!;
        return false;
    }

    private static ImmutableArray<INamedTypeSymbol> EnumerateTypeHierarchy(INamedTypeSymbol type)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        for (var current = type; current is not null; current = current.BaseType)
        {
            builder.Add(current);
        }

        builder.AddRange(type.AllInterfaces);
        return builder.ToImmutable();
    }

    private static bool TryGetStringValueObject(
        ITypeSymbol propertyType,
        out INamedTypeSymbol valueObjectType)
    {
        if (propertyType is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: 1
            } nullableType)
        {
            propertyType = nullableType.TypeArguments[0];
        }

        if (propertyType is not INamedTypeSymbol namedPropertyType)
        {
            valueObjectType = null!;
            return false;
        }

        var valueObjectInterface = namedPropertyType.AllInterfaces.FirstOrDefault(static @interface =>
            @interface.IsGenericType
            && @interface.OriginalDefinition.ToDisplayString() == ValueObjectInterfaceMetadataName);
        if (valueObjectInterface?.TypeArguments[0].SpecialType != SpecialType.System_String)
        {
            valueObjectType = null!;
            return false;
        }

        valueObjectType = namedPropertyType;
        return true;
    }

    private static bool TryGetDeclaredMaximumLength(INamedTypeSymbol valueObjectType, out int maximumLength)
    {
        var attribute = valueObjectType.GetAttributes().FirstOrDefault(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == LengthAttributeMetadataName);
        if (attribute?.ConstructorArguments.FirstOrDefault().Value is int declaredLength && declaredLength > 0)
        {
            maximumLength = declaredLength;
            return true;
        }

        maximumLength = 0;
        return false;
    }
}
