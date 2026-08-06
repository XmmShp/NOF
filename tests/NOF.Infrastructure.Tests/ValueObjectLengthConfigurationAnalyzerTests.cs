using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.EntityFrameworkCore;
using NOF.CodeFixes;
using NOF.Domain;
using NOF.Infrastructure;
using NOF.Infrastructure.EntityFrameworkCore.SourceGenerator;
using NOF.Infrastructure.SourceGenerator;
using System.Collections.Immutable;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public sealed class ValueObjectLengthConfigurationAnalyzerTests
{
    private static readonly Type[] _refs =
    [
        typeof(IValueObject<>),
        typeof(ValueObjectLengthAttribute),
        typeof(IDbModelBuilder),
        typeof(DbContext),
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MatchingConstantLength_ReportsNOF306(bool useEfCore)
    {
        var diagnostics = await GetDiagnosticsAsync(CreateSource(useEfCore, "100"), useEfCore);

        var diagnostic = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "NOF306");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictingConstantLength_ReportsNOF307(bool useEfCore)
    {
        var diagnostics = await GetDiagnosticsAsync(CreateSource(useEfCore, "80"), useEfCore);

        var diagnostic = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "NOF307");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConstFieldLength_IsAnalyzed(bool useEfCore)
    {
        var diagnostics = await GetDiagnosticsAsync(CreateSource(useEfCore, "ConfiguredLength", "const int ConfiguredLength = 100;"), useEfCore);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "NOF306");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonConstantLength_IsIgnored(bool useEfCore)
    {
        var diagnostics = await GetDiagnosticsAsync(CreateSource(useEfCore, "ConfiguredLength", "static readonly int ConfiguredLength = 80;"), useEfCore);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id is "NOF306" or "NOF307");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DomainLengthMissing_ReportsNOF308(bool useEfCore)
    {
        var source = CreateSource(useEfCore, "100")
            .Replace("[ValueObjectLength(100)]\n", string.Empty, StringComparison.Ordinal);

        var diagnostics = await GetDiagnosticsAsync(source, useEfCore);

        var diagnostic = Assert.Single(diagnostics, diagnostic => diagnostic.Id == "NOF308");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("global::Test.Name", diagnostic.Properties["ValueObjectType"]);
        Assert.Equal("100", diagnostic.Properties["ConfiguredLength"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DomainLengthMissing_WithNonPositiveConfiguration_IsIgnored(bool useEfCore)
    {
        var source = CreateSource(useEfCore, "-1")
            .Replace("[ValueObjectLength(100)]\n", string.Empty, StringComparison.Ordinal);

        var diagnostics = await GetDiagnosticsAsync(source, useEfCore);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "NOF308");
    }

    [Fact]
    public async Task CodeFix_RemovesExplicitLengthAndPreservesFollowingChain()
    {
        var source = CreateSource(useEfCore: false, "100")
            .Replace(".HasMaxLength(100));", ".HasMaxLength(100).IsRequired());", StringComparison.Ordinal);
        using var workspace = new AdhocWorkspace();
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
        var compilation = await document.Project.GetCompilationAsync();
        var diagnostics = await compilation!.WithAnalyzers(
            [new ValueObjectLengthConfigurationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "NOF306");
        var actions = new List<CodeAction>();
        var codeFixContext = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);
        var provider = new RemoveValueObjectLengthConfigurationCodeFixProvider();

        await provider.RegisterCodeFixesAsync(codeFixContext);

        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changedSolution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
        var changedText = await changedSolution.GetDocument(documentId)!.GetTextAsync();
        Assert.DoesNotContain("HasMaxLength", changedText.ToString());
        Assert.Contains(".IsRequired()", changedText.ToString());
    }

    [Fact]
    public async Task CodeFix_MovesMissingLengthToValueObject()
    {
        var source = CreateSource(useEfCore: false, "100")
            .Replace("[ValueObjectLength(100)]\n", string.Empty, StringComparison.Ordinal);
        using var workspace = new AdhocWorkspace();
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
        var compilation = await document.Project.GetCompilationAsync();
        var diagnostics = await compilation!.WithAnalyzers(
            [new ValueObjectLengthConfigurationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "NOF308");
        var actions = new List<CodeAction>();
        var codeFixContext = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);
        var provider = new MoveValueObjectLengthToDomainCodeFixProvider();

        await provider.RegisterCodeFixesAsync(codeFixContext);

        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changedSolution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
        var changedText = await changedSolution.GetDocument(documentId)!.GetTextAsync();
        Assert.Contains("[global::NOF.Domain.ValueObjectLength(100)]", changedText.ToString());
        Assert.DoesNotContain("HasMaxLength", changedText.ToString());
    }

    [Fact]
    public async Task CodeFix_MovesMissingLengthAcrossProjects()
    {
        const string domainSource = """
            using NOF.Domain;
            namespace Test;
            public readonly struct Name : IValueObject<string> { }
            """;
        const string infrastructureSource = """
            using NOF.Infrastructure;
            namespace Test;

            public sealed class Entity
            {
                public Name Value { get; set; }
            }

            public sealed class Contributor : IDbContextModelCreatingContributor
            {
                public void Configure(IDbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Entity>(entity =>
                        entity.Property(item => item.Value).HasMaxLength(100));
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        var domainProjectId = ProjectId.CreateNewId();
        var infrastructureProjectId = ProjectId.CreateNewId();
        var domainDocumentId = DocumentId.CreateNewId(domainProjectId);
        var infrastructureDocumentId = DocumentId.CreateNewId(infrastructureProjectId);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location));
        var solution = workspace.CurrentSolution
            .AddProject(domainProjectId, "Domain", "Domain", LanguageNames.CSharp)
            .WithProjectCompilationOptions(domainProjectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(domainProjectId, references)
            .AddDocument(domainDocumentId, "Name.cs", SourceText.From(domainSource))
            .AddProject(infrastructureProjectId, "Infrastructure", "Infrastructure", LanguageNames.CSharp)
            .WithProjectCompilationOptions(infrastructureProjectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(infrastructureProjectId, references)
            .AddProjectReference(infrastructureProjectId, new ProjectReference(domainProjectId))
            .AddDocument(infrastructureDocumentId, "Mapping.cs", SourceText.From(infrastructureSource));
        Assert.True(workspace.TryApplyChanges(solution));

        var infrastructureDocument = workspace.CurrentSolution.GetDocument(infrastructureDocumentId)!;
        var compilation = await infrastructureDocument.Project.GetCompilationAsync();
        var diagnostics = await compilation!.WithAnalyzers(
            [new ValueObjectLengthConfigurationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        var diagnostic = Assert.Single(diagnostics, item => item.Id == "NOF308");
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            infrastructureDocument,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await new MoveValueObjectLengthToDomainCodeFixProvider().RegisterCodeFixesAsync(context);

        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changedSolution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
        var changedDomain = (await changedSolution.GetDocument(domainDocumentId)!.GetTextAsync()).ToString();
        var changedInfrastructure = (await changedSolution.GetDocument(infrastructureDocumentId)!.GetTextAsync()).ToString();
        Assert.Contains("[global::NOF.Domain.ValueObjectLength(100)]", changedDomain);
        Assert.DoesNotContain("HasMaxLength", changedInfrastructure);
    }

    [Fact]
    public async Task FixAllInSolution_MovesEachLengthOnceAndRemovesAllConfigurations()
    {
        const string domainSource = """
            using NOF.Domain;
            namespace Test;
            public readonly struct Name : IValueObject<string> { }
            public readonly struct Code : IValueObject<string> { }
            """;
        const string firstInfrastructureSource = """
            using NOF.Infrastructure;
            namespace Test;

            public sealed class FirstEntity
            {
                public Name Value { get; set; }
            }

            public sealed class SecondEntity
            {
                public Name Value { get; set; }
            }

            public sealed class Contributor : IDbContextModelCreatingContributor
            {
                public void Configure(IDbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<FirstEntity>(entity =>
                        entity.Property(item => item.Value).HasMaxLength(100));
                    modelBuilder.Entity<SecondEntity>(entity =>
                        entity.Property(item => item.Value).HasMaxLength(100));
                }
            }
            """;
        const string secondInfrastructureSource = """
            using NOF.Infrastructure;
            namespace Test;

            public sealed class CodeEntity
            {
                public Code Value { get; set; }
            }

            public sealed class Contributor : IDbContextModelCreatingContributor
            {
                public void Configure(IDbModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<CodeEntity>(entity =>
                        entity.Property(item => item.Value).HasMaxLength(50));
                }
            }
            """;
        using var workspace = new AdhocWorkspace();
        var domainProjectId = ProjectId.CreateNewId();
        var firstInfrastructureProjectId = ProjectId.CreateNewId();
        var secondInfrastructureProjectId = ProjectId.CreateNewId();
        var domainDocumentId = DocumentId.CreateNewId(domainProjectId);
        var firstInfrastructureDocumentId = DocumentId.CreateNewId(firstInfrastructureProjectId);
        var secondInfrastructureDocumentId = DocumentId.CreateNewId(secondInfrastructureProjectId);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(static assembly => MetadataReference.CreateFromFile(assembly.Location));
        var solution = workspace.CurrentSolution
            .AddProject(domainProjectId, "Domain", "Domain", LanguageNames.CSharp)
            .WithProjectCompilationOptions(domainProjectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(domainProjectId, references)
            .AddDocument(domainDocumentId, "ValueObjects.cs", SourceText.From(domainSource))
            .AddProject(firstInfrastructureProjectId, "FirstInfrastructure", "FirstInfrastructure", LanguageNames.CSharp)
            .WithProjectCompilationOptions(firstInfrastructureProjectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(firstInfrastructureProjectId, references)
            .AddProjectReference(firstInfrastructureProjectId, new ProjectReference(domainProjectId))
            .AddDocument(firstInfrastructureDocumentId, "FirstMapping.cs", SourceText.From(firstInfrastructureSource))
            .AddProject(secondInfrastructureProjectId, "SecondInfrastructure", "SecondInfrastructure", LanguageNames.CSharp)
            .WithProjectCompilationOptions(secondInfrastructureProjectId, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(secondInfrastructureProjectId, references)
            .AddProjectReference(secondInfrastructureProjectId, new ProjectReference(domainProjectId))
            .AddDocument(secondInfrastructureDocumentId, "SecondMapping.cs", SourceText.From(secondInfrastructureSource));
        Assert.True(workspace.TryApplyChanges(solution));

        var diagnosticsByProject = ImmutableDictionary.CreateBuilder<ProjectId, ImmutableArray<Diagnostic>>();
        foreach (var projectId in new[] { firstInfrastructureProjectId, secondInfrastructureProjectId })
        {
            var project = workspace.CurrentSolution.GetProject(projectId)!;
            var compilation = await project.GetCompilationAsync();
            diagnosticsByProject[projectId] = await compilation!.WithAnalyzers(
                [new ValueObjectLengthConfigurationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        }

        Assert.Equal(3, diagnosticsByProject.Values.SelectMany(static diagnostics => diagnostics).Count());
        var provider = new MoveValueObjectLengthToDomainCodeFixProvider();
        var triggerDocument = workspace.CurrentSolution.GetDocument(firstInfrastructureDocumentId)!;
        var fixAllContext = new FixAllContext(
            triggerDocument,
            provider,
            FixAllScope.Solution,
            "MoveValueObjectLengthToDomain",
            provider.FixableDiagnosticIds,
            new TestFixAllDiagnosticProvider(diagnosticsByProject.ToImmutable()),
            CancellationToken.None);

        var action = await provider.GetFixAllProvider().GetFixAsync(fixAllContext);

        Assert.NotNull(action);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var changedSolution = Assert.Single(operations.OfType<ApplyChangesOperation>()).ChangedSolution;
        var changedDomain = (await changedSolution.GetDocument(domainDocumentId)!.GetTextAsync()).ToString();
        var changedFirstInfrastructure = (await changedSolution.GetDocument(firstInfrastructureDocumentId)!.GetTextAsync()).ToString();
        var changedSecondInfrastructure = (await changedSolution.GetDocument(secondInfrastructureDocumentId)!.GetTextAsync()).ToString();
        Assert.Equal(1, CountOccurrences(changedDomain, "ValueObjectLength(100)"));
        Assert.Equal(1, CountOccurrences(changedDomain, "ValueObjectLength(50)"));
        Assert.DoesNotContain("HasMaxLength", changedFirstInfrastructure);
        Assert.DoesNotContain("HasMaxLength", changedSecondInfrastructure);
    }

    private static int CountOccurrences(string value, string searchValue)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(searchValue, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += searchValue.Length;
        }

        return count;
    }

    private sealed class TestFixAllDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        private readonly ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>> _diagnosticsByProject;

        public TestFixAllDiagnosticProvider(
            ImmutableDictionary<ProjectId, ImmutableArray<Diagnostic>> diagnosticsByProject)
        {
            _diagnosticsByProject = diagnosticsByProject;
        }

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
            => _diagnosticsByProject.TryGetValue(projectId, out var diagnostics)
                ? diagnostics
                : [];
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source, bool useEfCore)
    {
        var extraReferences = _refs.Select(type => type.ToMetadataReference()).ToArray();
        var compilation = CSharpCompilation.CreateCompilation("TestAssembly", source, true, extraReferences);
        var analyzer = useEfCore
            ? (DiagnosticAnalyzer)new EfCoreValueObjectLengthConfigurationAnalyzer()
            : new ValueObjectLengthConfigurationAnalyzer();

        return await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();
    }

    private static string CreateSource(bool useEfCore, string configuredLength, string constantDeclaration = "")
        => useEfCore
            ? $$"""
                using Microsoft.EntityFrameworkCore;
                using NOF.Domain;

                namespace Test;

                [ValueObjectLength(100)]
                public readonly struct Name : IValueObject<string> { }

                public sealed class Entity
                {
                    public int Id { get; set; }
                    public Name Value { get; set; }
                }

                public sealed class Context : DbContext
                {
                    {{constantDeclaration}}

                    protected override void OnModelCreating(ModelBuilder modelBuilder)
                    {
                        modelBuilder.Entity<Entity>().Property(entity => entity.Value).HasMaxLength({{configuredLength}});
                    }
                }
                """
            : $$"""
                using NOF.Domain;
                using NOF.Infrastructure;

                namespace Test;

                [ValueObjectLength(100)]
                public readonly struct Name : IValueObject<string> { }

                public sealed class Entity
                {
                    public Name Value { get; set; }
                }

                public sealed class Contributor : IDbContextModelCreatingContributor
                {
                    {{constantDeclaration}}

                    public void Configure(IDbModelBuilder modelBuilder)
                    {
                        modelBuilder.Entity<Entity>(entity =>
                            entity.Property(item => item.Value).HasMaxLength({{configuredLength}}));
                    }
                }
                """;
}
