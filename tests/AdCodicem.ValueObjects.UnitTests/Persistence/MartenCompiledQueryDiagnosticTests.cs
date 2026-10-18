using System.Globalization;
using System.IO;
using System.Reflection;
using Marten.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// <c>VO0048</c> over the real Marten: the analyzer the compiler runs over this project, loaded from the copy
/// <c>CopyValueObjectAnalyzers</c> puts next to the tests, run over the compiled query
/// <see cref="MartenInternalsTests"/> pins Marten's replay of a value-object identity with, declared again against this
/// assembly's <see cref="MartenAccount"/> and <see cref="CustomerId"/>, the way an application declares it. The generator
/// tests compile against stand-ins of Marten's types under their names; this checks the names against Marten itself.
/// </summary>
public sealed class MartenCompiledQueryDiagnosticTests
{
    /// <summary>
    /// The pinned query type, word for word, beside one Marten reads no value object of: a member marked
    /// <c>[MartenIgnore]</c>, and a collection, which the package binds.
    /// </summary>
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Linq.Expressions;
        using AdCodicem.ValueObjects.UnitTests.Domain;
        using AdCodicem.ValueObjects.UnitTests.Persistence;
        using Marten.Events.CodeGeneration;
        using Marten.Linq;

        namespace Analyzed;

        public sealed class MartenAccountsById : ICompiledListQuery<MartenAccount>
        {
            public CustomerId Id { get; set; }

            public Expression<Func<IMartenQueryable<MartenAccount>, IEnumerable<MartenAccount>>> QueryIs()
                => accounts => accounts.Where(x => x.Id == Id);
        }

        public sealed class MartenAccountsOfIds : ICompiledListQuery<MartenAccount>
        {
            [MartenIgnore]
            public CustomerId Ignored { get; set; }

            public List<CustomerId> Ids { get; set; } = [];

            public Expression<Func<IMartenQueryable<MartenAccount>, IEnumerable<MartenAccount>>> QueryIs()
                => accounts => accounts.Where(x => Ids.Contains(x.Id));
        }
        """;

    /// <summary>
    /// The identity query Marten refuses until it has built the document's mapping, and answers with its first identity
    /// after, which no code of the package reaches at run time, is the shape <c>VO0048</c> reports at build time, with
    /// the form Marten binds on every call.
    /// </summary>
    [Fact]
    public async Task The_identity_query_Marten_answers_with_its_first_identity_is_reported_by_VO0048()
    {
        // The snippet declares the pinned type as it stands: the same interface, the same one public member.
        typeof(MartenAccountsById).GetInterfaces().Should().Contain(typeof(ICompiledListQuery<MartenAccount>));
        typeof(MartenAccountsById).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(static property => (property.Name, property.PropertyType))
            .Should().Equal((nameof(MartenAccountsById.Id), typeof(CustomerId)));
        typeof(MartenAccountsById).GetFields(BindingFlags.Instance | BindingFlags.Public).Should().BeEmpty();

        var compilation = Compile(Source);
        compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty();

        var diagnostics = await compilation.WithAnalyzers([LoadAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        var diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0048");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan)
            .Should().Be("Id");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "'Id' of the compiled query 'MartenAccountsById' holds the value object 'CustomerId', which Marten refuses as a "
            + "parameter, or, once it takes the type for a document's identity, accepts and answers on every call with the "
            + "first call's value. Declare it as Guid and compare the value object's Value with it, x.Id.Value == Id, which "
            + "needs AdCodicem.ValueObjects.Marten's UseValueObjects(), without which Marten reads a Value no document holds; "
            + "or, for a document's identity, whose Value Marten does not read, hold the values in an array or a list of the "
            + "value object (a type implementing IList<T>), read with Contains or IsOneOf, which "
            + "AdCodicem.ValueObjects.Marten's UseValueObjects() binds on every call.");
    }

    /// <summary>Compiles a snippet against what this process runs on, this assembly among it, Marten included.</summary>
    private static CSharpCompilation Compile(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(MartenAccount).Assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));

        return CSharpCompilation.Create(
            "Analyzed",
            [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>
    /// Loads the analyzer from the generator's assembly the compiler ran over this project: Roslyn 5, which this project
    /// references, runs it as the SDK's compiler does an analyzer built against an earlier Roslyn.
    /// </summary>
    private static DiagnosticAnalyzer LoadAnalyzer()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "analyzers", "AdCodicem.ValueObjects.Generators.dll");
        var type = Assembly.LoadFrom(path)
            .GetType("AdCodicem.ValueObjects.Generators.Analyzers.MartenCompiledQueryAnalyzer", throwOnError: true)!;

        return (DiagnosticAnalyzer)Activator.CreateInstance(type)!;
    }
}
