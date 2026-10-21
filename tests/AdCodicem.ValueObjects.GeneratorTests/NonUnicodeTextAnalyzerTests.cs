using System.Globalization;
using System.IO;
using AdCodicem.ValueObjects.Generators.Analyzers;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// What <c>VO0047</c> reports: a value object declaring <c>Unicode = false</c> whose declared rules let a character
/// outside ASCII through, which SQL Server replaces silently on its way to a <c>varchar</c> column.
/// </summary>
/// <remarks>
/// The analyzer runs over the compilation with the generator's output and the regex generator's, as the compiler runs
/// it, so that <c>Known</c> and the <c>[GeneratedRegex]</c> property bind. What .NET matches under each option the
/// analyzer reads was measured on the regex source generator's own output: <c>\d</c> matches Arabic-Indic digits,
/// <c>\w</c> under <c>ECMAScript</c> the dotted capital I, <c>[A-Z]</c> ignoring case the Kelvin sign, and, without
/// <c>CultureInvariant</c>, <c>I</c> and <c>i</c> nothing beyond ASCII unless the attribute names a culture, where they
/// match the dotted capital I, and the dotless i too under a Turkish one, or the regex is built at run time in the
/// current culture, as under <c>NonBacktracking</c>.
/// </remarks>
public sealed class NonUnicodeTextAnalyzerTests
{
    private const string Effect = "SQL Server writes a character its code page lacks as a look-alike or as '?', silently";

    private const string Culture =
        "it compares its known values as a culture does, which takes other spellings of one, a soft hyphen inside it "
        + "among them: " + Effect + ". Compare them Ordinal or OrdinalIgnoreCase, or leave the text Unicode.";

    private const string KnownValue =
        "a known value holds a character outside ASCII: " + Effect + ". Spell each known value in ASCII, or leave the text Unicode.";

    private const string Nothing =
        "nothing it declares holds the text to ASCII: " + Effect + ". Declare a pattern held to ASCII, such as ^[A-Z0-9]+$, "
        + "or check the text in IValueObjectValidator<string>.";

    private const string Unanchored =
        "its pattern does not hold the whole text, from one end to the other: " + Effect + @". Anchor it at both ends, ^...$ "
        + @"or \A...\z, without RegexOptions.Multiline, and hold it to ASCII, such as [A-Z0-9].";

    private const string Admits =
        "its pattern admits characters outside ASCII: " + Effect + @". Hold the pattern to ASCII: [0-9] rather than \d, "
        + "[A-Za-z] rather than IgnoreCase, a class of its own rather than '.' or a negated class.";

    private const string Invariant = "RegexOptions.CultureInvariant";

    private const string Whitespace = "RegexOptions.IgnorePatternWhitespace | " + Invariant;

    /// <summary>The patterns reported, with the options they are read under and the reason given.</summary>
    public static TheoryData<string, string, string> ReportedPatterns => new()
    {
        { @"^\d{5}$", Invariant, Admits },
        { @"^\w+$", Invariant, Admits },
        { @"^\p{L}+$", Invariant, Admits },
        { "^.+$", Invariant, Admits },
        { "^[^,;]+$", Invariant, Admits },
        { "^é$", Invariant, Admits },
        { @"^\xE9$", Invariant, Admits },
        { @"^\u00E9$", Invariant, Admits },
        { "^[à-ÿ]+$", Invariant, Admits },
        { @"^[\x20-\xFF]+$", Invariant, Admits },
        { @"^[\d]$", Invariant, Admits },
        { "^(?:[A-Z]|é)$", Invariant, Admits },
        { "^[a-z]+$|^é$", Invariant, Admits },
        { @"^\D+$", "RegexOptions.ECMAScript | " + Invariant, Admits },
        { @"^\w+$", "RegexOptions.ECMAScript | " + Invariant, Admits },
        { @"^[\w\s]+$", "RegexOptions.ECMAScript | " + Invariant, Admits },
        { "^[A-Z]{3}$", "RegexOptions.IgnoreCase | " + Invariant, Admits },
        { "^[a-z]{3}$", "RegexOptions.IgnoreCase | " + Invariant, Admits },
        { "^K$", "RegexOptions.IgnoreCase | " + Invariant, Admits },
        { "^[A-I]{3}$", "RegexOptions.IgnoreCase, cultureName: \"tr-TR\"", Admits },
        { "^[a-i]{3}$", "RegexOptions.IgnoreCase, cultureName: \"en-US\"", Admits },
        { "^[A-I]{3}$", "RegexOptions.IgnoreCase | RegexOptions.NonBacktracking", Admits },

        // The regex source generator folds these under the invariant culture, where I and i match nothing beyond ASCII,
        // but the analyzer does not tell them from the three above.
        { "^[A-I]{3}$", "RegexOptions.IgnoreCase", Admits },
        { "^[a-i]{3}$", "RegexOptions.IgnoreCase", Admits },
        { "[0-9]{5}", Invariant, Unanchored },
        { "^[0-9]{5}", Invariant, Unanchored },
        { "[0-9]{5}$", Invariant, Unanchored },
        { @"\b[0-9]{5}$", Invariant, Unanchored },
        { @"^[0-9]{5}\b", Invariant, Unanchored },
        { "^[A-Z]{3}$", "RegexOptions.Multiline | " + Invariant, Unanchored },
        { @"\A[A-Z]{3}$", "RegexOptions.Multiline | " + Invariant, Unanchored },
        { @"^[A-Z]{3}\z", "RegexOptions.Multiline | " + Invariant, Unanchored },
        { "^[A-Z]{3}|[0-9]{4}$", Invariant, Unanchored },
        { "^[A-Z]{3}$|", Invariant, Unanchored },
        { string.Empty, Invariant, Unanchored },

        // Read as .NET reads them under IgnorePatternWhitespace: a comment runs from '#' to the end of the line, but '#'
        // is a literal escaped or in a class, which ends at its first ']' but one right after its bracket or its '^', or
        // escaped, or closing a subtraction; a '{' starting no quantifier is a literal, and so is a vertical tab, which
        // is no white space there.
        { "^[A-Z]{3}#$", Whitespace, Unanchored },
        { @"^\#é$", Whitespace, Admits },
        { @"^\p{L}+$", Whitespace, Admits },
        { @"^\P{L}+$", Whitespace, Admits },
        { "^[#é]$", Whitespace, Admits },
        { "^[]#é]$", Whitespace, Admits },
        { "^[^]#]$", Whitespace, Admits },
        { @"^[\]#é]$", Whitespace, Admits },
        { "^[A-Z-[]#]]é$", Whitespace, Admits },
        { "^é{0, 0}$", Whitespace, Admits },
        { "^é\v{0}$", Whitespace, Admits },
        { "^[A-Z]{2", Whitespace, Unanchored },
        { "^[A-Z]{2,", Whitespace, Unanchored },
    };

    /// <summary>The patterns left alone, with the options they are read under.</summary>
    public static TheoryData<string, string> QuietPatterns => new()
    {
        { "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", Invariant },
        { "^[0-9]{5}$", Invariant },
        { "^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$", Invariant },
        { @"\A[A-Z]{3}\z|^[0-9]{4}$", Invariant },
        { @"^[A-Z]{3}\Z", Invariant },
        { @"\A[A-Z]{3}\z", "RegexOptions.Multiline | " + Invariant },
        { "^(?:[A-Z]{2})$", Invariant },
        { @"^[A-Z]\b[0-9]$", Invariant },
        { "^é{0}[A-Z]$", Invariant },
        { @"^\x41B$", Invariant },
        { "^$", Invariant },
        { @"^\d{5}$", "RegexOptions.ECMAScript | " + Invariant },
        { @"^[\d\s]+$", "RegexOptions.ECMAScript | " + Invariant },
        { "^[A-HL-Z]{3}$", "RegexOptions.IgnoreCase | " + Invariant },
        { "^A$", "RegexOptions.IgnoreCase | " + Invariant },
        { "^[A-HJ]{3}$", "RegexOptions.IgnoreCase" },
        { "^(?=.*[0-9])[A-Z0-9]+$", Invariant },
        { " ^[A-Z]{3}$ ", Whitespace },
        { "^[A-Z]{3}$\t\n\f\r ", Whitespace },
        { "^[A-Z]{3} # three letters\n$", Whitespace },
        { "^[A-Z]{3}$ # é", Whitespace },
        { "^é {0}[A-Z]$", Whitespace },
        { "^é{0,0} [A-Z]{2,3}$", Whitespace },
        { "^(?: Hi | Hello ) ! $", Whitespace },
        { "^( [A-Z] ){3}$", Whitespace },
        { "^(?: [A-Z] ){3}$", Whitespace },
        { "^[A-Z]{3}(?#a\n:é)$", Whitespace },
    };

    [Theory]
    [MemberData(nameof(ReportedPatterns))]
    public async Task A_pattern_that_lets_a_character_outside_ASCII_through_is_reported_on_the_option(
        string pattern,
        string options,
        string reason)
    {
        var diagnostics = await RunAsync(Pattern(pattern, options));

        Reported(diagnostics, "Code", reason);
    }

    [Theory]
    [MemberData(nameof(QuietPatterns))]
    public async Task A_pattern_that_holds_the_whole_text_to_ASCII_is_left_alone(string pattern, string options)
    {
        var diagnostics = await RunAsync(Pattern(pattern, options));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task A_value_object_declaring_no_rule_is_reported()
    {
        var diagnostics = await RunAsync("[ValueObject<string>(MaxLength = 60, Unicode = false)] public readonly partial struct PersonName;");

        Reported(diagnostics, "PersonName", Nothing);
    }

    /// <summary>
    /// An open set is read for its pattern, whatever known values it names: they hold nothing, since it takes any other
    /// value its rules accept.
    /// </summary>
    /// <param name="declaration">The value object.</param>
    /// <param name="reason">The reason given.</param>
    [Theory]
    [InlineData("""
        [ValueObject<string>(Unicode = false)]
        public readonly partial struct Code : IValueObjectPatternValidator
        {
            [KnownValue] public static readonly Code Main = Known("ABC");

            [GeneratedRegex(@"^\w+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
            public static partial Regex Pattern { get; }
        }
        """, Admits)]
    [InlineData("""
        [ValueObject<string>(MaxLength = 10, Unicode = false)]
        public readonly partial struct Code
        {
            [KnownValue] public static readonly Code Main = Known("ABC");
        }
        """, Nothing)]
    public async Task An_open_set_naming_known_values_is_read_for_its_pattern(string declaration, string reason)
    {
        var diagnostics = await RunAsync(declaration);

        Reported(diagnostics, "Code", reason);
    }

    /// <summary>
    /// A closed set declaring no known value is generated as an open one, which nothing holds: the generator reports
    /// <c>VO0005</c> beside it.
    /// </summary>
    [Fact]
    public async Task A_closed_set_declaring_no_known_value_is_read_as_an_open_one()
    {
        var diagnostics = await RunAsync("[ValueObject<string>(ValueSet = ValueSetKind.Closed, Unicode = false)] public readonly partial struct Empty;");

        Reported(diagnostics, "Empty", Nothing);
    }

    [Fact]
    public async Task A_generic_value_object_is_named_as_its_declaration_writes_it()
    {
        var diagnostics = await RunAsync("""
            public static partial class Outer
            {
                [ValueObject<string>(MaxLength = 12, Unicode = false)]
                public readonly partial struct Reference<TOwner>;
            }
            """);

        Reported(diagnostics, "Outer.Reference<TOwner>", Nothing);
    }

    /// <summary>
    /// A closed set is read for its known values: one spelled outside ASCII is reported, a field or a property, in any
    /// partial declaration, beside a value written as no constant, under an ordinal comparison stated or not.
    /// </summary>
    /// <param name="declaration">The value object.</param>
    [Theory]
    [InlineData("""
        [ValueObject<string>(ValueSet = ValueSetKind.Closed, Unicode = false)]
        public readonly partial struct Grade
        {
            [KnownValue] public static readonly Grade Good = Known("GUT");
            [KnownValue] public static readonly Grade Over = Known("ÜBER");
        }
        """)]
    [InlineData("""
        [ValueObject<string>(ValueSet = ValueSetKind.Closed, Unicode = false)]
        public readonly partial struct Grade
        {
            [KnownValue] public static readonly Grade Good = Known("GUT");
            [KnownValue] public static Grade Over { get; } = Known("ÜBER");
        }
        """)]
    [InlineData("""
        [ValueObject<string>(ValueSet = ValueSetKind.Closed, Unicode = false)]
        public readonly partial struct Grade
        {
            [KnownValue] public static readonly Grade Good = Known("GUT");
        }

        public readonly partial struct Grade
        {
            [KnownValue] public static readonly Grade Over = Known("ÜBER");
        }
        """)]
    [InlineData("""
        [ValueObject<string>(ValueSet = ValueSetKind.Closed, Unicode = false)]
        public readonly partial struct Grade
        {
            [KnownValue] public static readonly Grade Good = Known(string.Concat("G", "UT"));
            [KnownValue] public static readonly Grade Over = Known("ÜBER");
        }
        """)]
    [InlineData("""
        [ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.Ordinal, Unicode = false)]
        public readonly partial struct Grade
        {
            [KnownValue] public static readonly Grade Over = Known("ÜBER");
        }
        """)]
    [InlineData("""
        [ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.OrdinalIgnoreCase, Unicode = false)]
        public readonly partial struct Grade
        {
            [KnownValue] public static readonly Grade Over = Known("ÜBER");
        }
        """)]
    public async Task A_known_value_spelled_outside_ASCII_is_reported(string declaration)
    {
        var diagnostics = await RunAsync(declaration);

        Reported(diagnostics, "Grade", KnownValue);
    }

    /// <summary>
    /// A closed set compared as a culture compares takes other spellings of a known value, a soft hyphen inside it among
    /// them, and stores the one it was given, whatever its known values.
    /// </summary>
    /// <param name="comparison">The comparison.</param>
    [Theory]
    [InlineData("InvariantCulture")]
    [InlineData("InvariantCultureIgnoreCase")]
    [InlineData("CurrentCulture")]
    [InlineData("CurrentCultureIgnoreCase")]
    public async Task A_closed_set_compared_as_a_culture_compares_is_reported(string comparison)
    {
        var diagnostics = await RunAsync($$"""
            [ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.{{comparison}}, Unicode = false)]
            public readonly partial struct Status
            {
                [KnownValue] public static readonly Status Done = Known("DONE");
            }
            """);

        Reported(diagnostics, "Status", Culture);
    }

    /// <summary>
    /// What holds the text to ASCII is left alone: known values spelled in ASCII and compared ordinally, whatever a pattern
    /// beside them, a known value whose text is no constant or that the generator refuses, a member that is no known
    /// value, and a hook whose code may hold the text itself.
    /// </summary>
    /// <param name="declaration">The value object.</param>
    /// <param name="compiles">Whether the snippet compiles clean, a known value the generator refuses aside.</param>
    [Theory]
    [InlineData("""
        [ValueObject<string>(ValueSet = ValueSetKind.Closed, Unicode = false)]
        public readonly partial struct Country
        {
            [KnownValue] public static readonly Country France = Known("FR");
            [KnownValue] public static Country Germany { get; } = Known("DE");
        }
        """, true)]
    [InlineData("""
        public static class Codes
        {
            public const string France = "FR";
        }

        [ValueObject<string>(ValueSet = ValueSetKind.Closed, Unicode = false)]
        public readonly partial struct Country
        {
            [KnownValue] public static readonly Country France = Known(Codes.France);
            [KnownValue] public static readonly Country DE = Known(nameof(DE));
        }
        """, true)]
    [InlineData("""
        [ValueObject<string>(ValueSet = ValueSetKind.Closed, Unicode = false)]
        public readonly partial struct Country
        {
            [KnownValue] public static readonly Country France = Known("FR");
            [KnownValue] public static readonly Country Other = Known(string.Concat("D", "É"));
        }
        """, true)]
    [InlineData("""
        [ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.OrdinalIgnoreCase, Unicode = false)]
        public readonly partial struct Status
        {
            [KnownValue] public static readonly Status Final = Known("FINAL");
        }
        """, true)]
    [InlineData("""
        [ValueObject<string>(ValueSet = ValueSetKind.Closed, Unicode = false)]
        public readonly partial struct Country : IValueObjectPatternValidator
        {
            [KnownValue] public static readonly Country France = Known("FR");

            [GeneratedRegex(@"^\w+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
            public static partial Regex Pattern { get; }
        }
        """, true)]
    [InlineData("""
        [ValueObject<string>(ValueSet = ValueSetKind.Closed, Unicode = false)]
        public readonly partial struct Country
        {
            [KnownValue] public static readonly Country France = Known("FR");

            public static readonly string Note = "Ünterschied";
        }
        """, true)]
    [InlineData("""
        [ValueObject<string>(ValueSet = ValueSetKind.Closed, Unicode = false)]
        public readonly partial struct Country
        {
            [KnownValue] public static readonly Country France = Known("FR");
            [KnownValue] public static readonly Country Nowhere = default;
            [KnownValue] public static readonly Country Other = Create("É");
            [KnownValue] public static readonly Country Missing = Known(null!);
        }
        """, false)]
    [InlineData("""
        [ValueObject<string>(Unicode = false)]
        public readonly partial struct Sku : IValueObjectValidator<string>
        {
            public static ValidationResult ValidateValue(in string value)
                => System.Text.Ascii.IsValid(value) ? ValidationResult.Success : ValidationResult.InvalidFormat("An SKU is ASCII.");
        }
        """, true)]
    [InlineData("""
        [ValueObject<string>(Unicode = false)]
        public readonly partial struct Sku : IValueObjectSpanNormalizer
        {
            public static string NormalizeValue(ReadOnlySpan<char> value) => value.ToString();
        }
        """, true)]
    [InlineData("""
        [ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.InvariantCulture, Unicode = false)]
        public readonly partial struct Status : IValueObjectNormalizer<string>
        {
            [KnownValue] public static readonly Status Done = Known("DONE");

            public static string NormalizeValue(string value) => value.Trim();
        }
        """, true)]
    [InlineData("""
        [ValueObject<string>(Unicode = false)]
        public readonly partial struct Plain : IValueObjectPatternValidator
        {
            private static readonly Regex Compiled = new("^.+$", RegexOptions.None, TimeSpan.FromSeconds(1));

            public static Regex Pattern => Compiled;
        }
        """, true)]
    public async Task What_holds_the_text_to_ASCII_is_left_alone(string declaration, bool compiles)
    {
        var diagnostics = await RunAsync(declaration, compiles);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// Only a value object over text declaring the option is read: not one over another type (<c>VO0008</c>), not a type
    /// the generator refuses the shape of (<c>VO0002</c>), not one carrying both annotations (<c>VO0018</c>), not a
    /// struct that is no value object, not one declaring Unicode text or nothing, and not one whose enum option holds an
    /// undefined value (<c>VO0020</c>), for which nothing is generated.
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    [Theory]
    [InlineData("[ValueObject<int>(Unicode = false)] public readonly partial struct Quantity;")]
    [InlineData("[ValueObject<string>(Unicode = false)] public partial struct Mutable;")]
    [InlineData("[ValueObject<string>(Unicode = false)] public readonly partial record struct Recorded;")]
    [InlineData("[ValueObject<string>(Unicode = false)] public readonly ref partial struct Stacked;")]
    [InlineData("[ValueObject<string>(Unicode = false)] public sealed partial class Classy;")]
    [InlineData("[ValueObject<string>(Unicode = false)] [EntityId(\"acc\")] public readonly partial struct AccountId;")]
    [InlineData("public readonly struct Point;")]
    [InlineData("[ValueObject<string>(MaxLength = 60, Unicode = true)] public readonly partial struct PersonName;")]
    [InlineData("[ValueObject<string>(MaxLength = 60)] public readonly partial struct PersonName;")]
    [InlineData("[ValueObject<string>(Comparison = (StringComparison)42, Unicode = false)] public readonly partial struct Code;")]
    [InlineData("[ValueObject<string>(ValueSet = (ValueSetKind)7, Unicode = false)] public readonly partial struct Code;")]
    public async Task Only_a_value_object_over_text_declaring_the_option_is_read(string declaration)
    {
        var diagnostics = await RunAsync(declaration, compiles: false);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A pattern .NET refuses under <c>IgnorePatternWhitespace</c>, for an escape, a property, a comment or a class left
    /// open, is refused by the reader too, and left alone: the regex generator reports it.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    [Theory]
    [InlineData(@"^[A-Z]$\")]
    [InlineData(@"^\p{L")]
    [InlineData("^[A-Z]$(?#")]
    [InlineData(@"^[A-Z\")]
    public async Task A_pattern_NET_refuses_under_IgnorePatternWhitespace_is_left_alone(string pattern)
    {
        var diagnostics = await RunAsync(Pattern(pattern, Whitespace), compiles: false);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>A compilation that references no <c>[ValueObject&lt;T&gt;]</c> is not analyzed at all.</summary>
    [Fact]
    public async Task A_compilation_without_the_attribute_is_left_alone()
    {
        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<NonUnicodeTextAnalyzer>(
            "namespace Plain;\n\npublic readonly struct Code;\n",
            GeneratorHarness.FrameworkReferences);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A column of names kept under a Latin-1 code page on purpose silences the warning for its value object alone, as an
    /// analyzer's diagnostic is silenced, which a generator's is not.
    /// </summary>
    [Fact]
    public async Task A_pragma_around_one_attribute_silences_it_for_that_value_object_alone()
    {
        var diagnostics = await RunAsync("""
            #pragma warning disable VO0047 // A varchar column of names under Latin-1, kept on purpose.
            [ValueObject<string>(MaxLength = 60, Unicode = false)]
            #pragma warning restore VO0047
            public readonly partial struct LegacyName;

            [ValueObject<string>(MaxLength = 60, Unicode = false)]
            public readonly partial struct PersonName;
            """);

        Reported(diagnostics, "PersonName", Nothing);
    }

    /// <summary>The help link lands on the section of the Entity Framework Core page that explains the warning.</summary>
    [Fact]
    [Trait("Reads", "Documentation")]
    public void The_help_link_lands_on_the_section_that_explains_it()
    {
        var link = new Uri(NonUnicodeTextAnalyzer.NonUnicodeTextAdmitsOtherCharacters.HelpLinkUri);

        link.GetLeftPart(UriPartial.Path).Should().Be("https://adcodicem.github.io/AdCodicem.ValueObjects/docs/how-to/ef-core");

        var page = File.ReadAllLines(Path.Combine(RepositoryRoot(), "website", "docs", "how-to", "ef-core.md"));
        page.Should().Contain("slug: /how-to/ef-core");
        page.Select(Anchor).Should().Contain(link.Fragment.TrimStart('#'));
    }

    /// <summary>
    /// The compiler never hands an analyzer a null context, so the guard is only reached by a direct call, which it
    /// tolerates.
    /// </summary>
    [Fact]
    public void The_analyzer_initialized_without_a_context_does_nothing()
    {
        var initialize = () => new NonUnicodeTextAnalyzer().Initialize(null!);

        initialize.Should().NotThrow();
        new NonUnicodeTextAnalyzer().SupportedDiagnostics.Should().ContainSingle()
            .Which.Should().BeSameAs(NonUnicodeTextAnalyzer.NonUnicodeTextAdmitsOtherCharacters);
    }

    /// <summary>Compiles a value object over text declaring the option, whose pattern is the one given.</summary>
    private static string Pattern(string pattern, string options) => $$"""
        [ValueObject<string>(Unicode = false)]
        public readonly partial struct Code : IValueObjectPatternValidator
        {
            [GeneratedRegex(@"{{pattern.Replace("\"", "\"\"", StringComparison.Ordinal)}}", {{options}}, matchTimeoutMilliseconds: 1000)]
            public static partial Regex Pattern { get; }
        }
        """;

    /// <summary>
    /// Runs the analyzer over a snippet, its diagnostics in source order, once the snippet is shown to compile clean with
    /// the generator's output, unless it is told not to.
    /// </summary>
    private static async Task<IReadOnlyList<Diagnostic>> RunAsync(string source, bool compiles = true)
    {
        if (compiles)
        {
            GeneratorHarness.Run(source).CompilationDiagnostics.Should().BeEmpty();
        }

        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<NonUnicodeTextAnalyzer>(source);

        return [.. diagnostics.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start).ThenBy(static diagnostic => diagnostic.Id, StringComparer.Ordinal)];
    }

    /// <summary>Asserts the one diagnostic: its identifier, its severity, its message and where it is located.</summary>
    private static void Reported(IReadOnlyList<Diagnostic> diagnostics, string type, string reason)
    {
        var diagnostic = diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0047");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be($"'{type}' is stored as non-Unicode text, but {reason}");
        diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan)
            .Should().Be("Unicode = false");
    }

    /// <summary>
    /// Gets the anchor Docusaurus gives a heading of any level: lower case, punctuation dropped, spaces as hyphens. Any
    /// other line gives none.
    /// </summary>
    private static string Anchor(string line)
    {
        if (!line.StartsWith('#'))
        {
            return string.Empty;
        }

        var heading = line.TrimStart('#').Trim().ToLowerInvariant();

        return string.Concat(heading.Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-'))
            .Replace(' ', '-');
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AdCodicem.ValueObjects.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
