using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.EntityFrameworkCore;
using NOF.CodeFixes;
using NOF.Contract;
using NOF.Domain;
using NOF.Infrastructure.EntityFrameworkCore;
using System.Collections.Immutable;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public sealed class GeneralCodeFixProviderTests
{
    private static readonly Type[] _forceLoadedAssemblies =
    [
        typeof(IValueObject<>),
        typeof(IRpcService),
        typeof(DbContext),
        typeof(NOFDbContext),
    ];

    [Theory]
    [InlineData("NOF010", "public readonly struct Value", "Value", "partial struct Value")]
    [InlineData("NOF104", "public class Failures", "Failures", "partial class Failures")]
    [InlineData("NOF300", "public class Server", "Server", "partial class Server")]
    [InlineData("NOF021", "public class Mappings", "Mappings", "static partial class Mappings")]
    public async Task ModifierFixes_AddRequiredModifiers(
        string diagnosticId,
        string declaration,
        string locationText,
        string expected)
    {
        var source = $$"""
            namespace Test;
            {{declaration}} { }
            """;

        var changed = await ApplyFirstFixAsync(
            source,
            diagnosticId,
            locationText,
            new TypeDeclarationCodeFixProvider());

        Assert.Contains(expected, changed);
    }

    [Fact]
    public async Task NullableUnderlyingTypeFix_RemovesNullableAnnotation()
    {
        const string source = """
            #nullable enable
            using NOF.Domain;
            namespace Test;
            public readonly partial struct Name : IValueObject<string?> { }
            """;

        var changed = await ApplyFirstFixAsync(
            source,
            "NOF011",
            "Name",
            new TypeDeclarationCodeFixProvider());

        Assert.Contains("IValueObject<string>", changed);
        Assert.DoesNotContain("IValueObject<string?>", changed);
    }

    [Fact]
    public async Task RequestTypeFix_ConvertsReadonlyStructToClass()
    {
        const string source = """
            namespace Test;
            public readonly struct Request { }
            """;

        var changed = await ApplyFirstFixAsync(
            source,
            "NOF200",
            "Request",
            new TypeDeclarationCodeFixProvider());

        Assert.Contains("public class Request", changed);
        Assert.DoesNotContain("readonly", changed);
    }

    [Fact]
    public async Task ParameterlessConstructorFix_AddsConstructor()
    {
        const string source = """
            namespace Test;
            public class Request
            {
                public Request(string value) { }
            }
            """;

        var changed = await ApplyFirstFixAsync(
            source,
            "NOF202",
            "Request",
            new TypeDeclarationCodeFixProvider());

        Assert.Matches(@"public\s+Request\s*\(\s*\)", changed);
    }

    [Fact]
    public async Task VoidRpcReturnFix_ReturnsNofResult()
    {
        const string source = """
            namespace Test;
            public interface IService
            {
                void Execute(object request);
            }
            """;

        var changed = await ApplyFirstFixAsync(
            source,
            "NOF209",
            "Execute",
            new TypeDeclarationCodeFixProvider());

        Assert.Contains("global::NOF.Contract.Result Execute", changed);
    }

    [Fact]
    public async Task TransportContractFix_AddsRpcServiceBase()
    {
        const string source = """
            using NOF.Contract;
            namespace Test;
            [TransportOverMemory]
            public interface IService { }
            """;

        var changed = await ApplyFirstFixAsync(
            source,
            "NOF211",
            "TransportOverMemory",
            new TypeDeclarationCodeFixProvider());

        Assert.Contains(": global::NOF.Contract.IRpcService", changed);
    }

    [Fact]
    public async Task DbContextFix_ReplacesDirectEfCoreBase()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            namespace Test;
            public class Context : DbContext
            {
                public Context(DbContextOptions<Context> options) : base(options) { }
            }
            """;

        var changed = await ApplyFirstFixAsync(
            source,
            "NOF304",
            "Context",
            new TypeDeclarationCodeFixProvider());

        Assert.Contains("global::NOF.Infrastructure.EntityFrameworkCore.NOFDbContext", changed);
    }

    [Fact]
    public async Task DbContextFix_WithoutCompatibleConstructor_IsNotOffered()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;
            namespace Test;
            public class Context : DbContext { }
            """;

        var (_, actions) = await GetCodeActionsAsync(
            source,
            "NOF304",
            "Context",
            new TypeDeclarationCodeFixProvider());

        Assert.Empty(actions);
    }

    [Theory]
    [InlineData("NOF020")]
    [InlineData("NOF211")]
    [InlineData("NOF213")]
    [InlineData("NOF216")]
    public async Task AttributeRemovalFix_RemovesDiagnosedAttribute(string diagnosticId)
    {
        const string source = """
            namespace Test;
            [System.Obsolete]
            public interface IService { }
            """;

        var changed = await ApplyFirstFixAsync(
            source,
            diagnosticId,
            "System.Obsolete",
            new AttributeRemovalCodeFixProvider());

        Assert.DoesNotContain("Obsolete", changed);
    }

    [Fact]
    public async Task MultipleTransportFix_OffersOneRemovalPerTransport()
    {
        const string source = """
            using NOF.Contract;
            namespace Test;
            [TransportOverMemory]
            [TransportOverHttp(HttpRpcStyle.JsonRpc)]
            public interface IService : IRpcService { }
            """;

        var (_, actions) = await GetCodeActionsAsync(
            source,
            "NOF212",
            "IService",
            new AttributeRemovalCodeFixProvider());

        Assert.Equal(2, actions.Count);
    }

    [Theory]
    [InlineData("NOF013", "Of(value);")]
    [InlineData("NOF014", "Validate(value);")]
    public async Task NormalizeFix_RemovesStandaloneInvocation(string diagnosticId, string invocation)
    {
        var source = $$"""
            namespace Test;
            public static class Value
            {
                public static void Normalize(string value)
                {
                    {{invocation}}
                }

                private static void Of(string value) { }
                private static void Validate(string value) { }
            }
            """;

        var changed = await ApplyFirstFixAsync(
            source,
            diagnosticId,
            invocation[..invocation.IndexOf('(')],
            new RemoveNormalizeInvocationCodeFixProvider());

        Assert.DoesNotContain($"{invocation}\n", changed);
    }

    [Fact]
    public async Task NormalizeFix_ForNestedInvocation_IsNotOffered()
    {
        const string source = """
            namespace Test;
            public static class Value
            {
                public static string Normalize(string value) => Of(value);
                private static string Of(string value) => value;
            }
            """;

        var (_, actions) = await GetCodeActionsAsync(
            source,
            "NOF013",
            "Of(value)",
            new RemoveNormalizeInvocationCodeFixProvider());

        Assert.Empty(actions);
    }

    [Fact]
    public async Task OrderingFix_CastsExpressionLambdaToPrimitive()
    {
        const string source = """
            using System.Linq;
            namespace Test;
            public static class Query
            {
                public static void Run(Item[] items) => items.OrderBy(item => item.Name);
            }
            public sealed class Item { public object Name { get; set; } = new(); }
            """;
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add("PrimitiveType", "global::System.String")
            .Add("NullableKey", "false");

        var changed = await ApplyFirstFixAsync(
            source,
            "NOF015",
            "item => item.Name",
            new ValueObjectOrderingCodeFixProvider(),
            properties);

        Assert.Matches(@"item\s*=>\s*\((?:global::System\.)?(?:string|String)\)item\.Name", changed);
    }

    [Fact]
    public async Task DaemonResolutionFix_InsertsImmediateResolution()
    {
        const string source = """
            namespace Test;
            public static class Worker
            {
                public static void Run(dynamic services)
                {
                    using var scope = services.CreateScope();
                    Use(scope);
                }

                private static void Use(dynamic value) { }
            }
            """;

        var changed = await ApplyFirstFixAsync(
            source,
            "NOF040",
            "CreateScope",
            new DaemonServiceResolutionCodeFixProvider());

        var resolutionIndex = changed.IndexOf("scope.ServiceProvider.ResolveDaemonServices();", StringComparison.Ordinal);
        var useIndex = changed.IndexOf("Use(scope);", StringComparison.Ordinal);
        Assert.True(resolutionIndex >= 0 && resolutionIndex < useIndex);
    }

    [Fact]
    public async Task NativeBuildFix_UsesAwaitedNofPipeline()
    {
        const string source = """
            namespace Test;
            public static class Program
            {
                public static async System.Threading.Tasks.Task Run(dynamic builder)
                {
                    var app = builder.NativeBuilder.Build();
                }
            }
            """;
        var properties = ImmutableDictionary<string, string?>.Empty.Add("RecommendedMethod", "BuildAsync");

        var changed = await ApplyFirstFixAsync(
            source,
            "NOF401",
            "Build",
            new NativeHostBuilderBuildCodeFixProvider(),
            properties);

        Assert.Contains("await builder.BuildAsync()", changed);
        Assert.DoesNotContain("NativeBuilder.Build", changed);
    }

    [Fact]
    public async Task NativeBuildFix_InNonAsyncLocalFunction_IsNotOffered()
    {
        const string source = """
            namespace Test;
            public static class Program
            {
                public static async System.Threading.Tasks.Task Run(dynamic builder)
                {
                    void BuildLocal()
                    {
                        var app = builder.NativeBuilder.Build();
                    }
                }
            }
            """;
        var properties = ImmutableDictionary<string, string?>.Empty.Add("RecommendedMethod", "BuildAsync");

        var (_, actions) = await GetCodeActionsAsync(
            source,
            "NOF401",
            "Build();",
            new NativeHostBuilderBuildCodeFixProvider(),
            properties);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task BatchFixProviders_SupportFixAllInSolution()
    {
        var cases = new[]
        {
            new SolutionFixAllCase(
                "type declaration",
                "NOF010",
                """
                    namespace Test;
                    public readonly struct Value { }
                    """,
                "Value",
                new TypeDeclarationCodeFixProvider(),
                "Add partial modifier",
                static text => text.Contains("partial struct Value", StringComparison.Ordinal)),
            new SolutionFixAllCase(
                "attribute removal",
                "NOF213",
                """
                    namespace Test;
                    [System.Obsolete]
                    public interface IService { }
                    """,
                "System.Obsolete",
                new AttributeRemovalCodeFixProvider(),
                "RemoveInvalidAttribute",
                static text => !text.Contains("Obsolete", StringComparison.Ordinal)),
            new SolutionFixAllCase(
                "normalize invocation",
                "NOF013",
                """
                    namespace Test;
                    public static class Value
                    {
                        public static void Normalize(string value)
                        {
                            Of(value);
                        }

                        private static void Of(string value) { }
                    }
                    """,
                "Of(value)",
                new RemoveNormalizeInvocationCodeFixProvider(),
                "RemoveInvocationFromNormalize",
                static text => !text.Contains("Of(value);", StringComparison.Ordinal)),
            new SolutionFixAllCase(
                "value object ordering",
                "NOF015",
                """
                    using System.Linq;
                    namespace Test;
                    public static class Query
                    {
                        public static void Run(Item[] items) => items.OrderBy(item => item.Name);
                    }
                    public sealed class Item { public object Name { get; set; } = new(); }
                    """,
                "item => item.Name",
                new ValueObjectOrderingCodeFixProvider(),
                "CastOrderingKeyToPrimitive",
                static text => text.Contains("(global::System.String)item.Name", StringComparison.Ordinal),
                ImmutableDictionary<string, string?>.Empty
                    .Add("PrimitiveType", "global::System.String")
                    .Add("NullableKey", "false")),
            new SolutionFixAllCase(
                "daemon service resolution",
                "NOF040",
                """
                    namespace Test;
                    public static class Worker
                    {
                        public static void Run(dynamic services)
                        {
                            using var scope = services.CreateScope();
                            Use(scope);
                        }

                        private static void Use(dynamic value) { }
                    }
                    """,
                "CreateScope",
                new DaemonServiceResolutionCodeFixProvider(),
                "ResolveDaemonServices",
                static text => text.Contains("scope.ServiceProvider.ResolveDaemonServices();", StringComparison.Ordinal)),
            new SolutionFixAllCase(
                "native host build",
                "NOF401",
                """
                    namespace Test;
                    public static class Program
                    {
                        public static async System.Threading.Tasks.Task Run(dynamic builder)
                        {
                            var app = builder.NativeBuilder.Build();
                        }
                    }
                    """,
                "Build",
                new NativeHostBuilderBuildCodeFixProvider(),
                "UseNofBuildAsync",
                static text => text.Contains("await builder.BuildAsync()", StringComparison.Ordinal)
                    && !text.Contains("NativeBuilder.Build", StringComparison.Ordinal),
                ImmutableDictionary<string, string?>.Empty.Add("RecommendedMethod", "BuildAsync")),
            new SolutionFixAllCase(
                "value object length configuration",
                "NOF306",
                """
                    namespace Test;
                    public static class Configuration
                    {
                        public static void Configure(dynamic property)
                        {
                            property.HasMaxLength(100).IsRequired();
                        }
                    }
                    """,
                "100",
                new RemoveValueObjectLengthConfigurationCodeFixProvider(),
                nameof(RemoveValueObjectLengthConfigurationCodeFixProvider),
                static text => text.Contains("property.IsRequired();", StringComparison.Ordinal)
                    && !text.Contains("HasMaxLength", StringComparison.Ordinal)),
        };

        foreach (var testCase in cases)
        {
            var changedDocuments = await ApplySolutionFixAllAsync(testCase);

            Assert.Equal(2, changedDocuments.Length);
            Assert.All(
                changedDocuments,
                text => Assert.True(testCase.IsFixed(text), $"Solution Fix All failed for {testCase.Name}:{Environment.NewLine}{text}"));
        }
    }

    [Fact]
    public async Task MultipleTransportFix_FixAllRemovesOnlySelectedTransport()
    {
        var testCase = new SolutionFixAllCase(
            "selected transport removal",
            "NOF212",
            """
                using NOF.Contract;
                namespace Test;
                [TransportOverMemory]
                [TransportOverHttp(HttpRpcStyle.JsonRpc)]
                public interface IService : IRpcService { }
                """,
            "IService",
            new AttributeRemovalCodeFixProvider(),
            "RemoveTransport:TransportOverMemoryAttribute",
            static text => !text.Contains("TransportOverMemory", StringComparison.Ordinal)
                && text.Contains("TransportOverHttp", StringComparison.Ordinal));

        var changedDocuments = await ApplySolutionFixAllAsync(testCase);

        Assert.Equal(2, changedDocuments.Length);
        Assert.All(changedDocuments, text => Assert.True(testCase.IsFixed(text), text));
    }

    private static async Task<string> ApplyFirstFixAsync(
        string source,
        string diagnosticId,
        string locationText,
        CodeFixProvider provider,
        ImmutableDictionary<string, string?>? properties = null)
    {
        var (document, actions) = await GetCodeActionsAsync(source, diagnosticId, locationText, provider, properties);
        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changedSolution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
        return (await changedSolution.GetDocument(document.Id)!.GetTextAsync()).ToString();
    }

    private static async Task<(Document Document, IReadOnlyList<CodeAction> Actions)> GetCodeActionsAsync(
        string source,
        string diagnosticId,
        string locationText,
        CodeFixProvider provider,
        ImmutableDictionary<string, string?>? properties = null)
    {
        _ = _forceLoadedAssemblies.Length;
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location));
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(projectId, references)
            .AddDocument(documentId, "Test.cs", SourceText.From(source));
        Assert.True(workspace.TryApplyChanges(solution));

        var document = workspace.CurrentSolution.GetDocument(documentId)!;
        var syntaxTree = await document.GetSyntaxTreeAsync();
        var start = source.IndexOf(locationText, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find diagnostic location '{locationText}'.");
        var descriptor = new DiagnosticDescriptor(
            diagnosticId,
            diagnosticId,
            diagnosticId,
            "Tests",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(
            descriptor,
            Location.Create(syntaxTree!, new TextSpan(start, locationText.Length)),
            properties ?? ImmutableDictionary<string, string?>.Empty);
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        return (document, actions);
    }

    private static async Task<ImmutableArray<string>> ApplySolutionFixAllAsync(SolutionFixAllCase testCase)
    {
        _ = _forceLoadedAssemblies.Length;
        using var workspace = new AdhocWorkspace();
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location))
            .ToImmutableArray();
        var solution = workspace.CurrentSolution;
        var documentIds = ImmutableArray.CreateBuilder<DocumentId>();

        for (var index = 1; index <= 2; index++)
        {
            var projectId = ProjectId.CreateNewId();
            var documentId = DocumentId.CreateNewId(projectId);
            documentIds.Add(documentId);
            solution = solution
                .AddProject(projectId, $"TestProject{index}", $"TestProject{index}", LanguageNames.CSharp)
                .WithProjectCompilationOptions(projectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddMetadataReferences(projectId, references)
                .AddDocument(documentId, "Test.cs", SourceText.From(testCase.Source));
        }

        Assert.True(workspace.TryApplyChanges(solution));

        var descriptor = new DiagnosticDescriptor(
            testCase.DiagnosticId,
            testCase.DiagnosticId,
            testCase.DiagnosticId,
            "Tests",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
        var diagnosticsByProject = ImmutableDictionary.CreateBuilder<ProjectId, ImmutableArray<Diagnostic>>();
        foreach (var documentId in documentIds)
        {
            var document = workspace.CurrentSolution.GetDocument(documentId)!;
            var syntaxTree = await document.GetSyntaxTreeAsync();
            var start = testCase.Source.IndexOf(testCase.LocationText, StringComparison.Ordinal);
            Assert.True(start >= 0, $"Could not find diagnostic location '{testCase.LocationText}'.");
            var diagnostic = Diagnostic.Create(
                descriptor,
                Location.Create(syntaxTree!, new TextSpan(start, testCase.LocationText.Length)),
                testCase.Properties);
            diagnosticsByProject.Add(document.Project.Id, [diagnostic]);
        }

        var triggerDocument = workspace.CurrentSolution.GetDocument(documentIds[0])!;
        var fixAllProvider = Assert.IsAssignableFrom<FixAllProvider>(testCase.Provider.GetFixAllProvider());
        var context = new FixAllContext(
            triggerDocument,
            testCase.Provider,
            FixAllScope.Solution,
            testCase.EquivalenceKey,
            testCase.Provider.FixableDiagnosticIds,
            new TestFixAllDiagnosticProvider(diagnosticsByProject.ToImmutable()),
            CancellationToken.None);
        var action = await fixAllProvider.GetFixAsync(context);
        Assert.NotNull(action);

        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changedSolution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
        var changedDocuments = ImmutableArray.CreateBuilder<string>();
        foreach (var documentId in documentIds)
        {
            changedDocuments.Add((await changedSolution.GetDocument(documentId)!.GetTextAsync()).ToString());
        }

        return changedDocuments.ToImmutable();
    }

    private sealed record SolutionFixAllCase(
        string Name,
        string DiagnosticId,
        string Source,
        string LocationText,
        CodeFixProvider Provider,
        string EquivalenceKey,
        Func<string, bool> IsFixed,
        ImmutableDictionary<string, string?>? DiagnosticProperties = null)
    {
        public ImmutableDictionary<string, string?> Properties { get; } =
            DiagnosticProperties ?? ImmutableDictionary<string, string?>.Empty;
    }

    private sealed class TestFixAllDiagnosticProvider(
        ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>> diagnosticsByProject)
        : FixAllContext.DiagnosticProvider
    {
        public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
            Document document,
            CancellationToken cancellationToken)
        {
            var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
            return GetDiagnostics(document.Project.Id)
                .Where(diagnostic => diagnostic.Location.SourceTree == syntaxTree);
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken)
            => Task.FromResult(Enumerable.Empty<Diagnostic>());

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
            Project project,
            CancellationToken cancellationToken)
            => Task.FromResult<IEnumerable<Diagnostic>>(GetDiagnostics(project.Id));

        private ImmutableArray<Diagnostic> GetDiagnostics(ProjectId projectId)
            => diagnosticsByProject.TryGetValue(projectId, out var diagnostics)
                ? diagnostics
                : [];
    }
}
