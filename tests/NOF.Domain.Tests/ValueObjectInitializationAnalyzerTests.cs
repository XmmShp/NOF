using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NOF.Domain;
using NOF.Domain.SourceGenerator;
using System.Collections.Immutable;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public sealed class ValueObjectInitializationAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var compilation = CSharpCompilation.CreateCompilation(
            "TestAssembly",
            source,
            isDll: true,
            typeof(IValueObject<>));
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new ValueObjectInitializationAnalyzer());
        return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
    }

    [Theory]
    [InlineData("Name value = default;")]
    [InlineData("Name value = default!;")]
    [InlineData("var value = default(Name);")]
    [InlineData("Name value = new();")]
    [InlineData("var value = new Name();")]
    public async Task DirectZeroInitialization_ReportsNOF018(string statement)
    {
        var source = $$"""
            using NOF.Domain;
            namespace Test;

            public readonly partial struct Name : IValueObject<string>;
            public static class Usage
            {
                public static void Run()
                {
                    {{statement}}
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF018");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Name.Of(...)", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitUnderlyingDefaultConvertedToNullable_ReportsNOF018()
    {
        const string source = """
            using NOF.Domain;
            namespace Test;

            public readonly partial struct Name : IValueObject<string>;
            public static class Usage
            {
                public static Name? Create() => default(Name);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Single(diagnostics, static diagnostic => diagnostic.Id == "NOF018");
    }

    [Fact]
    public async Task NullableValueObjectDefault_DoesNotReportNOF018()
    {
        const string source = """
            using NOF.Domain;
            namespace Test;

            public readonly partial struct Name : IValueObject<string>;
            public static class Usage
            {
                public static Name? First() => default;
                public static Name? Second() => default(Name?);
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF018");
    }

    [Fact]
    public async Task ImplicitAndGenericZeroInitialization_DoNotReportNOF018()
    {
        const string source = """
            using NOF.Domain;
            using System;
            namespace Test;

            public readonly partial struct Name : IValueObject<string>;
            public sealed class Usage
            {
                private Name _field;

                public static Name[] Allocate() => new Name[10];
                public static Name FromGeneric() => CreateDefault<Name>();
                public static Name FromReflection() => Activator.CreateInstance<Name>();
                private static T CreateDefault<T>() where T : struct => default;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF018");
    }

    [Fact]
    public async Task FactoryAndUnrelatedStructInitialization_DoNotReportNOF018()
    {
        const string source = """
            using NOF.Domain;
            namespace Test;

            public readonly partial struct Name : IValueObject<string>;
            public readonly struct Point;
            public static class Usage
            {
                public static Name CreateName() => Name.Of("valid");
                public static Point CreatePoint() => new();
                public static Point DefaultPoint() => default;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, static diagnostic => diagnostic.Id == "NOF018");
    }
}
