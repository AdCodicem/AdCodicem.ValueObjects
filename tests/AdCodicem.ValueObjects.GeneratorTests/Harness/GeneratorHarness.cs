using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using AdCodicem.ValueObjects.Generators;
using AdCodicem.ValueObjects.Identifiers;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace AdCodicem.ValueObjects.GeneratorTests.Harness;

/// <summary>
/// Runs the generator and the analyzers over a snippet of source, in process.
/// </summary>
/// <remarks>
/// Driving Roslyn directly rather than through <c>Microsoft.CodeAnalysis.Testing</c> keeps these tests on
/// xUnit v3 — the harness packages bind to v2 — and makes the incremental-cache assertions reachable, which is
/// the part of a generator most likely to regress silently.
/// </remarks>
public static class GeneratorHarness
{
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    private static readonly ImmutableArray<MetadataReference> WithJsonPackage =
        References.Add(MetadataReference.CreateFromFile(typeof(Json.ValueObjectJsonRegistry).Assembly.Location));

    /// <summary>
    /// Compiles source, runs the generator, and reports what came out.
    /// </summary>
    /// <param name="source">Source to compile. A namespace and usings are added if absent.</param>
    /// <param name="documentationMode">
    /// How documentation comments are processed, in the source and in the generated files alike.
    /// <see cref="DocumentationMode.Diagnose"/> is what a project producing its documentation file compiles with,
    /// and the only mode that reports a malformed comment or a broken <c>cref</c>.
    /// </param>
    /// <param name="referenceJsonPackage">
    /// Whether the compilation references AdCodicem.ValueObjects.Json, as a project serializing through a
    /// source-generated context does. The generator then publishes every converter to the package's registry.
    /// </param>
    /// <returns>The generated sources and every diagnostic produced.</returns>
    public static GeneratorRun Run(
        string source,
        DocumentationMode documentationMode = DocumentationMode.Parse,
        bool referenceJsonPackage = false)
    {
        var parseOptions = ParseOptions.WithDocumentationMode(documentationMode);
        var compilation = Compile(source, parseOptions, referenceJsonPackage ? WithJsonPackage : References);
        var driver = CSharpGeneratorDriver
            .Create([new ValueObjectGenerator().AsSourceGenerator()], parseOptions: parseOptions, driverOptions: DriverOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        var result = driver.GetRunResult().Results.Single();

        return new GeneratorRun(
            [.. result.GeneratedSources.Select(generated => new GeneratedFile(
                generated.HintName,
                generated.SourceText.ToString()))],
            [.. generatorDiagnostics],
            [.. output.GetDiagnostics().Where(IsRelevant)],
            driver,
            compilation.SyntaxTrees.Single().GetText(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Gets the framework alone, which is what a project that does not reference the library compiles against.
    /// </summary>
    public static ImmutableArray<MetadataReference> FrameworkReferences { get; } = [.. Net100.References.All];

    /// <summary>
    /// Gets what every snippet compiles against unless told otherwise: the framework, the contracts and the
    /// identifiers.
    /// </summary>
    public static ImmutableArray<MetadataReference> LibraryReferences => References;

    /// <summary>
    /// Compiles source, runs the generator, then runs an analyzer over the result.
    /// </summary>
    /// <typeparam name="TAnalyzer">Analyzer to run.</typeparam>
    /// <param name="source">Source to compile.</param>
    /// <param name="references">What to compile against; <see cref="LibraryReferences"/> when omitted.</param>
    /// <returns>The analyzer's diagnostics.</returns>
    public static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync<TAnalyzer>(
        string source,
        ImmutableArray<MetadataReference>? references = null)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var compilation = Compile(source, references: references);
        var updated = CSharpGeneratorDriver
            .Create([new ValueObjectGenerator().AsSourceGenerator()], parseOptions: ParseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        _ = updated;

        var withAnalyzers = output.WithAnalyzers([new TAnalyzer()]);

        return await withAnalyzers.GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Compiles source, runs the generator, and emits the result as the image of an assembly another snippet can
    /// reference, so that what it declares reaches that snippet as metadata rather than as source.
    /// </summary>
    /// <param name="source">Source to compile. A namespace and usings are added if absent.</param>
    /// <param name="assemblyName">Name of the assembly, distinct from the snippet that will reference it.</param>
    /// <param name="references">What to compile against; <see cref="LibraryReferences"/> when omitted.</param>
    /// <returns>The bytes of the assembly.</returns>
    public static byte[] Emit(string source, string assemblyName, ImmutableArray<MetadataReference>? references = null)
    {
        CSharpGeneratorDriver
            .Create([new ValueObjectGenerator().AsSourceGenerator()], parseOptions: ParseOptions)
            .RunGeneratorsAndUpdateCompilation(Compile(source, references: references, assemblyName: assemblyName), out var output, out _);

        using var image = new MemoryStream();
        var emitted = output.Emit(image, cancellationToken: TestContext.Current.CancellationToken);

        emitted.Diagnostics.Where(IsRelevant).Should().BeEmpty("the assembly a snippet references must compile");

        return image.ToArray();
    }

    /// <summary>
    /// Runs the generator over one source, then over a second, and reports how the model step was reached.
    /// </summary>
    /// <remarks>
    /// This is the assertion that an incremental generator is actually incremental. If the model a value object
    /// produces is not equatable by value, an unrelated edit elsewhere in the compilation re-runs everything
    /// downstream and the IDE slows to a crawl, with nothing failing to show for it.
    /// </remarks>
    /// <param name="first">Source for the first run.</param>
    /// <param name="second">Source for the second run.</param>
    /// <param name="stepName">Tracked step to inspect.</param>
    /// <returns>The reasons recorded on the second run.</returns>
    public static ImmutableArray<IncrementalStepRunReason> RunTwice(string first, string second, string stepName)
    {
        var driver = CSharpGeneratorDriver
            .Create([new ValueObjectGenerator().AsSourceGenerator()], parseOptions: ParseOptions, driverOptions: DriverOptions)
            .RunGenerators(Compile(first))
            .RunGenerators(Compile(second));

        var steps = driver.GetRunResult().Results
            .SelectMany(result => result.TrackedSteps)
            .Where(pair => string.Equals(pair.Key, stepName, StringComparison.Ordinal))
            .SelectMany(pair => pair.Value);

        return [.. steps.SelectMany(step => step.Outputs).Select(output => output.Reason)];
    }

    private static readonly CSharpParseOptions ParseOptions =
        new(LanguageVersion.Preview);

    private static readonly GeneratorDriverOptions DriverOptions =
        new(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true);

    private static CSharpCompilation Compile(
        string source,
        CSharpParseOptions? parseOptions = null,
        ImmutableArray<MetadataReference>? references = null,
        string assemblyName = "GeneratorTests")
        => CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(Wrap(source), parseOptions ?? ParseOptions)],
            references ?? References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    private static string Wrap(string source)
        => source.Contains("namespace", StringComparison.Ordinal)
            ? source
            : $"""
               using System;
               using AdCodicem.ValueObjects;
               using AdCodicem.ValueObjects.Annotations;
               using AdCodicem.ValueObjects.Identifiers;

               namespace Test;

               {source}
               """;

    private static ImmutableArray<MetadataReference> BuildReferences() =>
    [
        .. Net100.References.All,
        MetadataReference.CreateFromFile(typeof(IValueObject).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(EntityIdAttribute).Assembly.Location),
    ];

    /// <summary>
    /// Filters out the noise a bare snippet produces, keeping real compilation failures.
    /// </summary>
    private static bool IsRelevant(Diagnostic diagnostic)
        => diagnostic.Severity >= DiagnosticSeverity.Warning
           && diagnostic.Id is not ("CS1591" or "CS8019");
}

/// <summary>One file produced by the generator.</summary>
/// <param name="HintName">Name the generator gave the file.</param>
/// <param name="Text">Its contents.</param>
public sealed record GeneratedFile(string HintName, string Text);

/// <summary>The outcome of a generator run.</summary>
/// <param name="Files">Files the generator produced.</param>
/// <param name="Diagnostics">Diagnostics the generator reported.</param>
/// <param name="CompilationDiagnostics">Diagnostics from compiling the result.</param>
/// <param name="Driver">The driver, for incremental inspection.</param>
/// <param name="Source">The source compiled, as wrapped by the harness.</param>
public sealed record GeneratorRun(
    ImmutableArray<GeneratedFile> Files,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<Diagnostic> CompilationDiagnostics,
    GeneratorDriver Driver,
    SourceText Source)
{
    /// <summary>Gets the single generated value object file, failing when there is not exactly one.</summary>
    public string SingleValueObject
        => Files.Single(file => !file.HintName.Contains("Registration", StringComparison.Ordinal)).Text;

    /// <summary>Gets the identifiers of every diagnostic reported.</summary>
    public IReadOnlyList<string> Ids => [.. Diagnostics.Select(diagnostic => diagnostic.Id)];

    /// <summary>
    /// Gets the source a diagnostic points at: the text of its span, and the whole line it starts on, trimmed.
    /// </summary>
    /// <remarks>
    /// The generator reports through a location rebuilt from a path and a span, which carries no syntax tree to
    /// read the text back from, so it is read from the source this run compiled.
    /// </remarks>
    /// <param name="diagnostic">A diagnostic reported against <see cref="Source"/>.</param>
    /// <returns>The spanned text and its line.</returns>
    public (string Text, string Line) Locate(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.SourceSpan;

        return (Source.ToString(span), Source.Lines.GetLineFromPosition(span.Start).ToString().Trim());
    }
}
