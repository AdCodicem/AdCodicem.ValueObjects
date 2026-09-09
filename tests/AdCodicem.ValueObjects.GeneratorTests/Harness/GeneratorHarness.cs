using System.Collections.Immutable;
using System.Reflection;
using AdCodicem.ValueObjects.Generators;
using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

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

    /// <summary>
    /// Compiles source, runs the generator, and reports what came out.
    /// </summary>
    /// <param name="source">Source to compile. A namespace and usings are added if absent.</param>
    /// <returns>The generated sources and every diagnostic produced.</returns>
    public static GeneratorRun Run(string source)
    {
        var compilation = Compile(source);
        var driver = CSharpGeneratorDriver
            .Create([new ValueObjectGenerator().AsSourceGenerator()], parseOptions: ParseOptions, driverOptions: DriverOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        var result = driver.GetRunResult().Results.Single();

        return new GeneratorRun(
            [.. result.GeneratedSources.Select(generated => new GeneratedFile(
                generated.HintName,
                generated.SourceText.ToString()))],
            [.. generatorDiagnostics],
            [.. output.GetDiagnostics().Where(IsRelevant)],
            driver);
    }

    /// <summary>
    /// Compiles source, runs the generator, then runs an analyzer over the result.
    /// </summary>
    /// <typeparam name="TAnalyzer">Analyzer to run.</typeparam>
    /// <param name="source">Source to compile.</param>
    /// <returns>The analyzer's diagnostics.</returns>
    public static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync<TAnalyzer>(string source)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var compilation = Compile(source);
        var updated = CSharpGeneratorDriver
            .Create([new ValueObjectGenerator().AsSourceGenerator()], parseOptions: ParseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        _ = updated;

        var withAnalyzers = output.WithAnalyzers([new TAnalyzer()]);

        return await withAnalyzers.GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
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

    private static CSharpCompilation Compile(string source)
        => CSharpCompilation.Create(
            "GeneratorTests",
            [CSharpSyntaxTree.ParseText(Wrap(source), ParseOptions)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    private static string Wrap(string source)
        => source.Contains("namespace", StringComparison.Ordinal)
            ? source
            : $"""
               using System;
               using AdCodicem.ValueObjects;
               using AdCodicem.ValueObjects.Annotations;

               namespace Test;

               {source}
               """;

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var abstractions = typeof(IValueObject).Assembly.Location;

        return
        [
            .. Net100.References.All,
            MetadataReference.CreateFromFile(abstractions),
        ];
    }

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
public sealed record GeneratorRun(
    ImmutableArray<GeneratedFile> Files,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<Diagnostic> CompilationDiagnostics,
    GeneratorDriver Driver)
{
    /// <summary>Gets the single generated value object file, failing when there is not exactly one.</summary>
    public string SingleValueObject
        => Files.Single(file => !file.HintName.Contains("Registration", StringComparison.Ordinal)).Text;

    /// <summary>Gets the identifiers of every diagnostic reported.</summary>
    public IReadOnlyList<string> Ids => [.. Diagnostics.Select(diagnostic => diagnostic.Id)];
}
