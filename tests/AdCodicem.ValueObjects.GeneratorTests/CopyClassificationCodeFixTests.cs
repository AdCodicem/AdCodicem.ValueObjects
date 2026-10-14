using System.Collections.Immutable;
using AdCodicem.ValueObjects.CodeFixes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using static AdCodicem.ValueObjects.GeneratorTests.LoggingSnippets;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// What the code fix of <c>VO0045</c> writes: the classification of the value object's type, copied onto the parameter or
/// the property where Microsoft.Gen.Logging reads it, after which the logging generator hands the tag to a redactor.
/// </summary>
public sealed class CopyClassificationCodeFixTests
{
    /// <summary>Classification attributes a copy of which would not compile, or could not be written where it is needed.</summary>
    private const string Attributes = """
        public sealed class ClassifiedAttribute(string category) : DataClassificationAttribute(new DataClassification("Snippets", category));

        public sealed class SensitiveAttribute : DataClassificationAttribute
        {
            public SensitiveAttribute() : base(Taxonomy.Personal) { }

            public SensitiveAttribute(string category) : base(new DataClassification("Snippets", category)) { }

            public string? Notes { get; set; }
        }

        [AttributeUsage(AttributeTargets.Struct)]
        public sealed class StructOnlyAttribute : DataClassificationAttribute
        {
            public StructOnlyAttribute() : base(Taxonomy.Personal) { }
        }

        [AttributeUsage(AttributeTargets.Struct)]
        public abstract class StructOnlyBase(DataClassification classification) : DataClassificationAttribute(classification);

        public sealed class DerivedStructOnlyAttribute : StructOnlyBase
        {
            public DerivedStructOnlyAttribute() : base(Taxonomy.Personal) { }
        }

        [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Struct)]
        public sealed class ParameterOnlyAttribute : DataClassificationAttribute
        {
            public ParameterOnlyAttribute() : base(Taxonomy.Personal) { }
        }

        """;

    [Theory]
    [InlineData("Email email", "[PersonalData] Email email", "email <PersonalDataAttribute>")]
    [InlineData("RefEmail? email", "[PersonalData] RefEmail? email", "email <PersonalDataAttribute>")]
    [InlineData("Salary salary", "[FinancialData] Salary salary", "salary <FinancialDataAttribute>")]
    [InlineData("Unknownish value", "[UnknownDataClassification] Unknownish value", "value <UnknownDataClassificationAttribute>")]
    [InlineData("Both value", "[PersonalData] Both value", "value <PersonalDataAttribute>")]
    [InlineData("Reference<PurchaseOrder> reference", "[PersonalData] Reference<PurchaseOrder> reference", "reference <PersonalDataAttribute>")]
    [InlineData("HandWrittenSecret secret", "[PersonalData] HandWrittenSecret secret", "secret <PersonalDataAttribute>")]
    [InlineData("IEnumerable<RefEmail> emails", "[PersonalData] IEnumerable<RefEmail> emails", "emails <PersonalDataAttribute>")]
    [InlineData("System.Collections.Immutable.ImmutableArray<RefEmail>? emails", "[PersonalData] System.Collections.Immutable.ImmutableArray<RefEmail>? emails", "emails <PersonalDataAttribute>")]
    [InlineData("[System.Diagnostics.CodeAnalysis.NotNull] RefEmail email", "[System.Diagnostics.CodeAnalysis.NotNull][PersonalData] RefEmail email", "email <PersonalDataAttribute>")]
    public async Task The_fix_copies_the_type_classification_onto_the_parameter(string parameter, string fixedParameter, string classified)
    {
        var source = $$"""
            [PersonalData]
            [ValueObject<string>(MaxLength = 254)]
            public readonly partial struct Email;

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, {{parameter}});
            }
            """;

        var fixedSource = await FixAsync(source);

        fixedSource.Should().Contain($"public static partial void Logged(ILogger logger, {fixedParameter});");
        var (diagnostics, tags) = await AnalyzeAsync(Body(fixedSource));
        diagnostics.Should().BeEmpty();
        tags.Classified.Should().Equal(classified);
    }

    /// <summary>
    /// A property <c>[LogProperties]</c> expands is classified where the logging generator reads it: a record's on its
    /// positional parameter, with no <c>property:</c> target, a class's on its own line, indented as the property is,
    /// with the classification of the value object it makes nullable or of the elements of the collection it holds.
    /// </summary>
    [Fact]
    public async Task The_fix_classifies_a_member_where_the_logging_generator_reads_it()
    {
        var fixedSource = await FixAsync("""
            [PersonalData]
            [ValueObject<string>(MaxLength = 254)]
            public readonly partial struct Email;

            public sealed record Signup(Email Email, RefEmail? Contact, List<RefEmail> Emails, Iban Iban);

            public sealed class Profile
            {
                public RefEmail Contact { get; init; }

                public RefEmail? Maybe { get; init; }

                public IEnumerable<RefEmail> Emails { get; init; } = [];
            }

            public sealed record Party(RefEmail Contact);

            public sealed record Payment(Iban Account, [property: LogProperties] Party Payer);

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Signed up")]
                public static partial void SignedUp(ILogger logger, [LogProperties] Signup signup, [LogProperties] Profile profile);

                [LoggerMessage(Level = LogLevel.Information, Message = "Paid")]
                public static partial void Paid(ILogger logger, [LogProperties] Payment payment);
            }
            """);

        Lines(fixedSource).Should().ContainInConsecutiveOrder(
            "public sealed record Signup([PersonalData] Email Email, [PersonalData] RefEmail? Contact, [PersonalData] List<RefEmail> Emails, Iban Iban);",
            string.Empty,
            "public sealed class Profile",
            "{",
            "    [PersonalData]",
            "    public RefEmail Contact { get; init; }",
            string.Empty,
            "    [PersonalData]",
            "    public RefEmail? Maybe { get; init; }",
            string.Empty,
            "    [PersonalData]",
            "    public IEnumerable<RefEmail> Emails { get; init; } = [];",
            "}",
            string.Empty,
            "public sealed record Party([PersonalData] RefEmail Contact);");

        var (diagnostics, tags) = await AnalyzeAsync(Body(fixedSource));
        diagnostics.Should().BeEmpty();
        tags.Classified.Should().Contain([
            "signup.Email <PersonalDataAttribute>",
            "signup.Contact <PersonalDataAttribute>",
            "signup.Emails <PersonalDataAttribute>",
            "profile.Contact <PersonalDataAttribute>",
            "profile.Maybe <PersonalDataAttribute>",
            "profile.Emails <PersonalDataAttribute>",
            "payment.Payer.Contact <PersonalDataAttribute>"]);
    }

    /// <summary>
    /// Every classification of the type is copied, in the order it declares them, and the namespace of an attribute the
    /// file does not import is imported.
    /// </summary>
    [Fact]
    public async Task The_fix_copies_every_classification_in_declared_order_and_imports_its_namespace()
    {
        var fixedSource = await GeneratorHarness.FixAsync<DataClassificationAnalyzer, CopyClassificationCodeFixProvider>(
            """
            using AdCodicem.ValueObjects.Annotations;
            using Microsoft.Extensions.Logging;

            namespace Snippets;

            [Shop.FinancialData, Shop.PersonalData]
            [ValueObject<string>]
            public readonly partial struct Account;

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, Account account, Shop.RefEmail email);
            }
            """,
            WithDomain,
            withLoggingGenerator: true);

        Lines(fixedSource).Should().Contain("using Shop;");
        Lines(fixedSource).Should().Contain(
            "    public static partial void Logged(ILogger logger, [FinancialData][PersonalData] Account account, [PersonalData] Shop.RefEmail email);");

        var run = GeneratorHarness.Run(fixedSource, references: WithDomain, withLoggingGenerator: true);
        run.CompilationDiagnostics.Should().BeEmpty();
        LoggedTags.Read(run.LoggingSource).Classified.Should().Equal(
            "account <FinancialDataAttribute, PersonalDataAttribute>",
            "email <PersonalDataAttribute>");
    }

    /// <summary>
    /// The fix offers nothing where a copy would not compile, an attribute written with arguments, since the logging
    /// generator builds <c>new TAttribute()</c>, or one its usage keeps off the target, nor where it could not be written:
    /// a member of another assembly, a property of a record logged whole. The warning stays, and says what to write.
    /// </summary>
    [Fact]
    public async Task Nothing_is_offered_where_a_copy_would_not_compile_or_could_not_be_written()
    {
        var offered = await GeneratorHarness.OfferedFixTitlesAsync<DataClassificationAnalyzer>(
            new CopyClassificationCodeFixProvider(),
            Header + Attributes + """
            [Classified("Health")]
            [ValueObject<string>]
            public readonly partial struct Diagnosis;

            [Sensitive("Health")]
            [ValueObject<string>]
            public readonly partial struct Note;

            [Sensitive(Notes = "x")]
            [ValueObject<string>]
            public readonly partial struct Remark;

            [StructOnly]
            [ValueObject<string>]
            public readonly partial struct Nickname;

            [DerivedStructOnly]
            [ValueObject<string>]
            public readonly partial struct Alias;

            [PersonalData, Classified("x")]
            [ValueObject<string>]
            public readonly partial struct Mixed;

            [ParameterOnly]
            [ValueObject<string>]
            public readonly partial struct Pin;

            [Sensitive]
            [ValueObject<string>]
            public readonly partial struct Plain;

            public sealed class Card
            {
                public Pin Pin { get; init; }
            }

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(
                    ILogger logger,
                    Diagnosis diagnosis,
                    Note note,
                    Remark remark,
                    Nickname nickname,
                    Alias alias,
                    Mixed mixed,
                    Pin pin,
                    Plain plain);

                [LoggerMessage(Level = LogLevel.Information, Message = "Carded")]
                public static partial void Carded(ILogger logger, [LogProperties] Card card, [LogProperties] RefHolder holder, RefHolder whole);
            }
            """,
            WithDomain,
            withLoggingGenerator: true);

        offered.Select(titles => string.Join(" | ", titles)).Should().Equal(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            "Classify with [ParameterOnly], as 'Pin' is",
            "Classify with [Sensitive], as 'Plain' is",
            string.Empty,
            string.Empty,
            string.Empty);
    }

    /// <summary>
    /// An <c>[AttributeUsage]</c> the compiler refuses, written without its targets, names none: the usage of the nearest
    /// base that declares one applies, as it does to the compiler.
    /// </summary>
    [Fact]
    public async Task A_usage_written_without_its_targets_falls_back_to_the_base_usage()
    {
        var offered = await GeneratorHarness.OfferedFixTitlesAsync<DataClassificationAnalyzer>(
            new CopyClassificationCodeFixProvider(),
            Header + """
            [AttributeUsage]
            public sealed class LooseAttribute : DataClassificationAttribute
            {
                public LooseAttribute() : base(Taxonomy.Personal) { }
            }

            [Loose]
            [ValueObject<string>]
            public readonly partial struct Nickname;

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, Nickname nickname);
            }
            """,
            WithDomain,
            withLoggingGenerator: true);

        offered.Should().ContainSingle().Which.Should().Equal("Classify with [Loose], as 'Nickname' is");
    }

    /// <summary>
    /// Fixing all of a document classifies each declaration once, however many diagnostics name it: a duplicate would
    /// compile, the attribute allowing several, and change nothing.
    /// </summary>
    [Fact]
    public async Task Fixing_all_in_a_document_classifies_each_declaration_once()
    {
        var fixedSource = await GeneratorHarness.FixAllAsync<DataClassificationAnalyzer, CopyClassificationCodeFixProvider>(
            Header + """
            public sealed record Signup(RefEmail Email, Iban Iban);

            public sealed class Profile
            {
                public RefEmail Contact { get; init; }
            }

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Signed up")]
                public static partial void SignedUp(ILogger logger, [LogProperties] Signup signup, RefEmail email);

                [LoggerMessage(Level = LogLevel.Information, Message = "Again")]
                public static partial void Again(ILogger logger, [LogProperties] Signup signup, [LogProperties] Profile profile);

                [LoggerMessage(Level = LogLevel.Information, Message = "Updated")]
                public static partial void Updated(ILogger logger, [LogProperties] Profile profile, Salary salary);
            }
            """,
            WithDomain,
            withLoggingGenerator: true);

        Lines(fixedSource).Should().ContainInConsecutiveOrder(
            "public sealed record Signup([PersonalData] RefEmail Email, Iban Iban);",
            string.Empty,
            "public sealed class Profile",
            "{",
            "    [PersonalData]",
            "    public RefEmail Contact { get; init; }",
            "}");
        Lines(fixedSource).Should().Contain([
            "    public static partial void SignedUp(ILogger logger, [LogProperties] Signup signup, [PersonalData] RefEmail email);",
            "    public static partial void Updated(ILogger logger, [LogProperties] Profile profile, [FinancialData] Salary salary);"]);
        (await AnalyzeAsync(Body(fixedSource))).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task The_fix_is_offered_for_VO0045_and_fixes_all_in_one_batch()
    {
        var fix = new CopyClassificationCodeFixProvider();

        fix.FixableDiagnosticIds.Should().Equal("VO0045");
        fix.GetFixAllProvider().Should().BeSameAs(WellKnownFixAllProviders.BatchFixer);

        var offered = await GeneratorHarness.OfferedFixTitlesAsync<DataClassificationAnalyzer>(
            fix,
            Header + """
            [PersonalData]
            [ValueObject<string>]
            public readonly partial struct Email;

            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, Email email);
            }
            """,
            WithDomain,
            withLoggingGenerator: true);

        offered.Should().ContainSingle().Which.Should().Equal("Classify with [PersonalData], as 'Email' is");
    }

    /// <summary>
    /// A diagnostic the fix cannot place, which the analyzer never reports so but another tool might, gets no fix: no
    /// target, nothing to classify, a member with no location, a location on no parameter or property, or outside the
    /// solution, a parameter whose type is classified by nothing.
    /// </summary>
    [Fact]
    public async Task Nothing_is_offered_for_a_diagnostic_the_fix_cannot_place()
    {
        const string Source = """
            public static partial class Log
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Logged")]
                public static partial void Logged(ILogger logger, RefEmail email);
            }
            """;

        var elsewhere = CSharpSyntaxTree.ParseText(Header + Source, path: "Elsewhere.cs");

        (await OfferedAsync((tree, text) => Create(At(tree, text, "email)"), properties: null))).Should().BeEmpty();
        (await OfferedAsync((tree, text) => Create(At(tree, text, "email)"), DataClassificationAnalyzer.NoTarget))).Should().BeEmpty();
        (await OfferedAsync((tree, text) => Create(At(tree, text, "email)"), DataClassificationAnalyzer.MemberTarget))).Should().BeEmpty();
        (await OfferedAsync((_, _) => Create(Location.None, DataClassificationAnalyzer.ParameterTarget))).Should().BeEmpty();
        (await OfferedAsync((tree, text) => Create(At(tree, text, "Microsoft.Extensions.Logging"), DataClassificationAnalyzer.ParameterTarget)))
            .Should().BeEmpty();
        (await OfferedAsync((tree, text) => Create(At(tree, text, "logger,"), DataClassificationAnalyzer.ParameterTarget))).Should().BeEmpty();
        (await OfferedAsync((tree, text) => Create(
                At(tree, text, "email)"),
                DataClassificationAnalyzer.MemberTarget,
                Location.Create(elsewhere, new TextSpan((Header + Source).IndexOf("email)", StringComparison.Ordinal), 5)))))
            .Should().BeEmpty();

        // The same diagnostic, placed: the fix is offered.
        (await OfferedAsync((tree, text) => Create(At(tree, text, "email)"), DataClassificationAnalyzer.ParameterTarget)))
            .Should().Equal("Classify with [PersonalData], as 'RefEmail' is");

        Task<IReadOnlyList<string>> OfferedAsync(Func<SyntaxTree, string, Diagnostic> diagnose)
            => GeneratorHarness.OfferedFixTitlesAsync(new CopyClassificationCodeFixProvider(), Header + Source, diagnose, WithDomain);
    }

    private static Location At(SyntaxTree tree, string text, string token)
        => Location.Create(tree, new TextSpan(text.IndexOf(token, StringComparison.Ordinal), token.TrimEnd(',', ')').Length));

    private static Diagnostic Create(Location location, string? properties, Location? additional = null)
        => Diagnostic.Create(
            DataClassificationAnalyzer.LoggedInClear,
            location,
            additional is null ? null : [additional],
            properties is null
                ? ImmutableDictionary<string, string?>.Empty
                : ImmutableDictionary<string, string?>.Empty.Add(DataClassificationAnalyzer.TargetKey, properties),
            "email",
            "RefEmail",
            "[PersonalData]",
            "Classify it");

    private static Task<string> FixAsync(string source)
        => GeneratorHarness.FixAsync<DataClassificationAnalyzer, CopyClassificationCodeFixProvider>(
            Header + source,
            WithDomain,
            withLoggingGenerator: true);

    /// <summary>Gets a fixed snippet without the header, which the analysis adds back.</summary>
    private static string Body(string fixedSource)
    {
        fixedSource.Should().StartWith(Header);

        return fixedSource[Header.Length..];
    }

    private static IReadOnlyList<string> Lines(string source)
        => [.. source.Split('\n').Select(line => line.TrimEnd('\r'))];
}
