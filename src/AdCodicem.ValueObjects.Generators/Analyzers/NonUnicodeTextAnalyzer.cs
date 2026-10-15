using System.Collections.Immutable;
using System.Text;
using AdCodicem.ValueObjects.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AdCodicem.ValueObjects.Generators.Analyzers;

/// <summary>
/// Reports a value object declaring its text non-Unicode whose declared rules let a character outside ASCII through.
/// </summary>
/// <remarks>
/// <para>
/// <c>Unicode = false</c> maps a value object's text to non-Unicode text, <c>varchar</c> on SQL Server, whose code page
/// holds ASCII and some characters beside it, or none. SQL Server writes a character the code page lacks as a look-alike
/// or as <c>?</c>, with no error, whether an <c>nvarchar</c> parameter reaches the column, SqlClient encodes a
/// <c>varchar</c> one, or a migration alters the column; a read then trusts the replaced value. The option is how the text
/// is stored, not a rule, so nothing checks it at run time: the rules the type declares are what holds the text to ASCII,
/// and this analyzer reads them.
/// </para>
/// <para>
/// A closed set holds the text to its known values, so it is read for them: one spelled outside ASCII is reported, and so
/// is a set compared as a culture compares, which takes other spellings of a known value, a soft hyphen inside it among
/// them, and stores the one it was given. <see cref="StringComparison.OrdinalIgnoreCase"/> equates no character outside
/// ASCII with one inside it. A known value whose text is no constant is taken as it comes, beside the others. Any other
/// type is read for its pattern: none is reported, and so is one that does not hold the whole text, since a match may
/// start and end anywhere unless every alternative of the pattern is anchored at both ends (<c>^</c> and <c>$</c> only
/// without <c>RegexOptions.Multiline</c>), or that admits a character outside ASCII: a literal or a range beyond
/// U+007F, <c>.</c>, a negated class, and <c>\d</c>, <c>\w</c>, <c>\s</c> and <c>\p{…}</c>, which .NET reads over
/// Unicode, though <c>RegexOptions.ECMAScript</c> holds <c>\d</c> and <c>\s</c> to ASCII (its <c>\w</c> still matches
/// <c>İ</c>, U+0130). Under <c>RegexOptions.IgnoreCase</c>, <c>K</c> and <c>k</c> also match the Kelvin sign, and, without
/// <c>RegexOptions.CultureInvariant</c>, <c>I</c> and <c>i</c> are reported too: <c>[GeneratedRegex]</c> folds case under
/// the invariant culture, where they match no character outside ASCII, unless it names a culture, where they match
/// <c>İ</c>, and <c>ı</c> too under a Turkish or an Azerbaijani one, or builds the regex at run time, in the current
/// culture, as it does under <c>RegexOptions.NonBacktracking</c>; the analyzer does not tell these apart. A pattern
/// declared with <c>RegexOptions.IgnorePatternWhitespace</c> is read as .NET reads it, without its white space and its
/// comments.
/// </para>
/// <para>
/// A normalizer or a validator hook is code that may hold the text to ASCII itself, so its type is left alone, as
/// <c>VO0011</c> leaves a type implementing another hook. So is a pattern the shared reader refuses, a lookaround among
/// them, and one written without <c>[GeneratedRegex]</c>, whose text is known at run time only.
/// </para>
/// <para>
/// It is an analyzer rather than a diagnostic of the generator, which neither <c>#pragma</c> nor a severity setting
/// silences: a column of names kept under a Latin-1 code page on purpose is a legitimate reason to silence it, for one
/// type. The diagnostic is located on the <c>Unicode = false</c> argument of the attribute.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NonUnicodeTextAnalyzer : DiagnosticAnalyzer
{
    // RegexOptions, as the number [GeneratedRegex] holds them.
    private const int IgnoreCase = 1;
    private const int Multiline = 2;
    private const int IgnorePatternWhitespace = 32;
    private const int EcmaScript = 256;
    private const int CultureInvariant = 512;

    /// <summary>The format the message names a type in: as the consumer writes it, keywords included.</summary>
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.CSharpShortErrorMessageFormat;

    /// <summary>The hooks whose code may hold the text to ASCII itself, by metadata name.</summary>
    private static readonly string[] CodeHooks = ["IValueObjectNormalizer`1", "IValueObjectSpanNormalizer", "IValueObjectValidator`1"];

    private static readonly (string Reason, string Remedy) ComparedAsACulture = (
        "it compares its known values as a culture does, which takes other spellings of one, a soft hyphen inside it among them",
        "Compare them Ordinal or OrdinalIgnoreCase, or leave the text Unicode");

    private static readonly (string Reason, string Remedy) KnownValueOutsideAscii = (
        "a known value holds a character outside ASCII",
        "Spell each known value in ASCII, or leave the text Unicode");

    private static readonly (string Reason, string Remedy) NothingHoldsIt = (
        "nothing it declares holds the text to ASCII",
        "Declare a pattern held to ASCII, such as ^[A-Z0-9]+$, or check the text in IValueObjectValidator<string>");

    private static readonly (string Reason, string Remedy) NotAnchored = (
        "its pattern does not hold the whole text, from one end to the other",
        @"Anchor it at both ends, ^...$ or \A...\z, without RegexOptions.Multiline, and hold it to ASCII, such as [A-Z0-9]");

    private static readonly (string Reason, string Remedy) AdmitsOtherCharacters = (
        "its pattern admits characters outside ASCII",
        @"Hold the pattern to ASCII: [0-9] rather than \d, [A-Za-z] rather than IgnoreCase, a class of its own rather than '.' or a negated class");

    /// <summary>VO0047, a warning: non-Unicode text that the rules of its value object do not hold to ASCII.</summary>
    public static readonly DiagnosticDescriptor NonUnicodeTextAdmitsOtherCharacters = new(
        "VO0047",
        "Non-Unicode text that its rules do not hold to ASCII",
        "'{0}' is stored as non-Unicode text, but {1}: SQL Server writes a character its code page lacks as a look-alike "
        + "or as '?', silently. {2}.",
        "AdCodicem.ValueObjects",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Unicode = false maps a value object's text to non-Unicode text, varchar on SQL Server, whose code "
        + "page holds ASCII and some characters beside it, or none. Where the rules the type declares let another "
        + "character through, SQL Server replaces it on its way to the column, with no error, and a read trusts the "
        + "replaced value. An anchored pattern of ASCII classes, a closed set of known values spelled in ASCII and "
        + "compared ordinally, or a validator holds the text to ASCII.",
        helpLinkUri: "https://adcodicem.github.io/AdCodicem.ValueObjects/docs/how-to/ef-core#non-unicode-text");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [NonUnicodeTextAdmitsOtherCharacters];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            return;
        }

        // The declaration the consumer writes: the generated partial declares no option and no known value.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            if (!compilationContext.Compilation.GetTypesByMetadataName(ValueObjectGenerator.ValueObjectAttributeName).IsEmpty)
            {
                compilationContext.RegisterSymbolStartAction(AnalyzeType, SymbolKind.NamedType);
            }
        });
    }

    private static void AnalyzeType(SymbolStartAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        // The shapes VO0002 refuses get nothing from the generator; both annotations on one type is VO0018.
        if (type.TypeKind != TypeKind.Struct || !type.IsReadOnly || type.IsRecord || type.IsRefLikeType
            || ValueObjectGenerator.FindAttribute(type, ValueObjectGenerator.ValueObjectAttributeName) is not { } attribute
            || attribute.AttributeClass!.TypeArguments[0].SpecialType != SpecialType.System_String
            || ValueObjectGenerator.CarriesAttribute(type, ValueObjectGenerator.EntityIdAttributeName))
        {
            return;
        }

        // Read as the generator reads them: a value its enum does not define is VO0020, and nothing is generated.
        var arguments = ValueObjectGenerator.NamedArguments(attribute);
        if (!ValueObjectGenerator.DeclaresNonUnicode(arguments)
            || CodeHooks.Any(hook => ValueObjectGenerator.ImplementsHook(type, hook))
            || !ValueObjectGenerator.TryReadEnumName(arguments, "ValueSet", out var valueSet)
            || !ValueObjectGenerator.TryReadEnumName(arguments, "Comparison", out var comparison))
        {
            return;
        }

        var location = UnicodeArgument(attribute, context.CancellationToken);

        // An open set, or a closed one marking no known value, which the generator generates as an open one (VO0005).
        if (valueSet != "Closed" || !ValueObjectGenerator.KnownValueMembers(type).Any())
        {
            if (PatternReason(type) is { } reason)
            {
                context.RegisterSymbolEndAction(end => Report(end, type, location, reason));
            }

            return;
        }

        if (comparison is not (null or "Ordinal" or "OrdinalIgnoreCase"))
        {
            context.RegisterSymbolEndAction(end => Report(end, type, location, ComparedAsACulture));
            return;
        }

        // Written by the initializers of the type, each analyzed on its own, possibly at once; read once they all have.
        // Known is called in the initializer of a known value alone (VO0037), which may be a field or a property.
        var outside = 0;
        context.RegisterOperationAction(
            operationContext =>
            {
                if (SpellsOutsideAscii(((ISymbolInitializerOperation)operationContext.Operation).Value))
                {
                    Interlocked.Exchange(ref outside, 1);
                }
            },
            OperationKind.FieldInitializer,
            OperationKind.PropertyInitializer);

        context.RegisterSymbolEndAction(end =>
        {
            if (Volatile.Read(ref outside) == 1)
            {
                Report(end, type, location, KnownValueOutsideAscii);
            }
        });
    }

    /// <summary>
    /// Determines whether the initializer of a known value, <c>Known(...)</c>, hands it a constant holding a character
    /// outside ASCII.
    /// </summary>
    /// <param name="initializer">What initializes the member.</param>
    /// <returns><see langword="true"/> when the text is a constant spelled outside ASCII.</returns>
    private static bool SpellsOutsideAscii(IOperation initializer)
        => initializer is IInvocationOperation { TargetMethod.Name: "Known" } known
           && known.Arguments.Any(static argument =>
               argument.Value.ConstantValue is { HasValue: true, Value: string text } && text.Any(static character => character > '\x7F'));

    /// <summary>
    /// Tells why the pattern of an open set lets a character outside ASCII through, if it does.
    /// </summary>
    /// <param name="type">The value object.</param>
    /// <returns>The reason and its remedy, or <see langword="null"/> when the pattern holds the text to ASCII or cannot be read.</returns>
    private static (string Reason, string Remedy)? PatternReason(INamedTypeSymbol type)
    {
        if (!ValueObjectGenerator.ImplementsHook(type, "IValueObjectPatternValidator"))
        {
            return NothingHoldsIt;
        }

        if (ValueObjectGenerator.ReadGeneratedRegex(type) is not { Text: { } text } generated
            || !ValueObjectPatternSyntax.TryParse(
                (generated.Options & IgnorePatternWhitespace) != 0 ? WithoutPatternWhitespace(text) : text,
                out var root))
        {
            return null;
        }

        if (!HoldsWholeText(root, (generated.Options & Multiline) != 0))
        {
            return NotAnchored;
        }

        var reading = new Reading(
            (generated.Options & IgnoreCase) != 0,
            (generated.Options & CultureInvariant) != 0,
            (generated.Options & EcmaScript) != 0);

        return AdmitsNonAscii(root, reading) ? AdmitsOtherCharacters : null;
    }

    /// <summary>
    /// Determines whether every alternative of the top level starts and ends with an anchor that holds a match to the whole
    /// text: <c>\A</c>, and <c>^</c> without <c>RegexOptions.Multiline</c>, at its start; <c>\z</c> or <c>\Z</c>, and
    /// <c>$</c> without it, at its end.
    /// </summary>
    /// <param name="root">The pattern.</param>
    /// <param name="multiline">Whether <c>RegexOptions.Multiline</c> makes <c>^</c> and <c>$</c> match at any line.</param>
    /// <returns><see langword="true"/> when a match cannot leave a character of the text out.</returns>
    private static bool HoldsWholeText(PatternNode root, bool multiline)
        => (root is PatternAlternation alternation ? alternation.Alternatives : ImmutableArray.Create((PatternSequence)root))
            .All(sequence => sequence.Items.Length > 0
                && StartsText(sequence.Items[0], multiline)
                && EndsText(sequence.Items[sequence.Items.Length - 1], multiline));

    private static bool StartsText(PatternNode node, bool multiline)
        => node is PatternAnchor { Kind: PatternAnchorKind.StartOfText }
           || (!multiline && node is PatternAnchor { Kind: PatternAnchorKind.StartOfLine });

    private static bool EndsText(PatternNode node, bool multiline)
        => node is PatternAnchor { Kind: PatternAnchorKind.EndOfText or PatternAnchorKind.EndOfTextOrFinalNewline }
           || (!multiline && node is PatternAnchor { Kind: PatternAnchorKind.EndOfLine });

    /// <summary>Determines whether a node of the pattern matches a character outside ASCII.</summary>
    /// <param name="node">The node.</param>
    /// <param name="reading">The options the pattern is read under.</param>
    /// <returns><see langword="true"/> when it matches one, or may.</returns>
    private static bool AdmitsNonAscii(PatternNode node, Reading reading) => node switch
    {
        PatternAlternation alternation => alternation.Alternatives.Any(item => AdmitsNonAscii(item, reading)),
        PatternSequence sequence => sequence.Items.Any(item => AdmitsNonAscii(item, reading)),
        PatternGroup group => AdmitsNonAscii(group.Content, reading),
        PatternQuantifier quantifier => quantifier.Maximum != 0 && AdmitsNonAscii(quantifier.Item, reading),
        PatternLiteral literal => literal.Character > '\x7F' || (reading.IgnoreCase && FoldsOutside(literal.Character, literal.Character, reading)),
        PatternRange range => range.To > '\x7F' || (reading.IgnoreCase && FoldsOutside(range.From, range.To, reading)),
        PatternAnchor => false,

        // ECMAScript holds \d and \s to ASCII; its \w still matches İ (U+0130), and the negations match beyond it.
        PatternClassEscape { Letter: 'd' or 's' } when reading.EcmaScript => false,

        // A subtraction only narrows a class: leaving it out errs towards the warning.
        PatternCharacterClass characterClass => characterClass.Negated || characterClass.Items.Any(item => AdmitsNonAscii(item, reading)),

        // '.', and the class escapes, which .NET reads over Unicode.
        _ => true,
    };

    /// <summary>
    /// Determines whether a range of ASCII characters matches one outside ASCII when case is ignored: the Kelvin sign for
    /// <c>K</c> and <c>k</c>, and, without <c>RegexOptions.CultureInvariant</c>, <c>İ</c> and <c>ı</c> for <c>I</c> and
    /// <c>i</c>, which a culture the attribute names, or the current one a regex built at run time folds under, may match.
    /// </summary>
    private static bool FoldsOutside(char from, char to, Reading reading)
        => Within(from, to, 'K') || Within(from, to, 'k')
           || (!reading.CultureInvariant && (Within(from, to, 'I') || Within(from, to, 'i')));

    private static bool Within(char from, char to, char character) => from <= character && character <= to;

    /// <summary>
    /// Writes a pattern declared with <c>RegexOptions.IgnorePatternWhitespace</c> as the pattern .NET reads with no
    /// option: outside a class, it drops white space that is not escaped and a comment from <c>#</c> to the end of the
    /// line, and reads a <c>{</c> that starts no quantifier as a literal, which is escaped here, since the white space
    /// dropped inside it could make one.
    /// </summary>
    /// <param name="pattern">The pattern, as the attribute holds it.</param>
    /// <returns>The pattern without its white space and comments.</returns>
    private static string WithoutPatternWhitespace(string pattern)
    {
        var builder = new StringBuilder(pattern.Length);
        var index = 0;
        while (index < pattern.Length)
        {
            var end = index + 1;
            switch (pattern[index])
            {
                case '\\':
                    // An escape, \p{…} and \P{…} through their brace, where a '{' starts no quantifier.
                    end = string.Compare(pattern, index + 1, "p", 0, 1, StringComparison.OrdinalIgnoreCase) == 0
                        ? pattern.IndexOf('}', index) + 1
                        : index + 2;
                    break;
                case '[':
                    end = ClassEnd(pattern, index);
                    break;
                case '(' when string.CompareOrdinal(pattern, index, "(?#", 0, 3) == 0:
                    // A comment of any mode, which the reader refuses as it does without the option.
                    end = pattern.IndexOf(')', index) + 1;
                    break;
                case '#':
                    end = pattern.IndexOf('\n', index);
                    index = end < 0 ? pattern.Length : end;
                    continue;
                case '\t' or '\n' or '\f' or '\r' or ' ':
                    index = end;
                    continue;
                case '{' when !StartsQuantifier(pattern, index):
                    builder.Append('\\');
                    break;
            }

            // An escape, a brace or a comment left open runs to the end, where .NET refuses the pattern.
            end = end <= index ? pattern.Length : Math.Min(end, pattern.Length);
            builder.Append(pattern, index, end - index);
            index = end;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Finds the end of the class a <c>[</c> opens, which .NET reads as it stands, white space and <c>#</c> included: a
    /// <c>]</c> right after the bracket or its <c>^</c> is a literal, and a subtraction, <c>-[…]</c>, is a class of its
    /// own.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="index">Where the class opens.</param>
    /// <returns>The index past its <c>]</c>, or past the pattern for a class left open.</returns>
    private static int ClassEnd(string pattern, int index)
    {
        index++;
        if (index < pattern.Length && pattern[index] == '^')
        {
            index++;
        }

        if (index < pattern.Length && pattern[index] == ']')
        {
            index++;
        }

        while (index < pattern.Length)
        {
            switch (pattern[index])
            {
                case ']':
                    return index + 1;
                case '\\':
                    index += 2;
                    break;
                case '-' when string.CompareOrdinal(pattern, index, "-[", 0, 2) == 0:
                    index = ClassEnd(pattern, index + 1);
                    break;
                default:
                    index++;
                    break;
            }
        }

        return pattern.Length;
    }

    /// <summary>
    /// Determines whether a <c>{</c> starts a quantifier, as .NET reads one: <c>{n}</c>, <c>{n,}</c> or <c>{n,m}</c>, white
    /// space left out.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="index">Where the brace is.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool StartsQuantifier(string pattern, int index)
    {
        var end = SkipDigits(pattern, index + 1);
        if (end == index + 1 || end == pattern.Length)
        {
            return false;
        }

        if (pattern[end] == ',')
        {
            end = SkipDigits(pattern, end + 1);
        }

        return end < pattern.Length && pattern[end] == '}';
    }

    private static int SkipDigits(string pattern, int index)
    {
        while (index < pattern.Length && pattern[index] is >= '0' and <= '9')
        {
            index++;
        }

        return index;
    }

    private static void Report(SymbolAnalysisContext context, INamedTypeSymbol type, Location location, (string Reason, string Remedy) why)
        => context.ReportDiagnostic(Diagnostic.Create(
            NonUnicodeTextAdmitsOtherCharacters,
            location,
            type.ToDisplayString(TypeFormat),
            why.Reason,
            why.Remedy));

    /// <summary>Gets where to report: the <c>Unicode = false</c> argument, the first when it is written twice (CS0643).</summary>
    private static Location UnicodeArgument(AttributeData attribute, CancellationToken cancellationToken)
        => ((AttributeSyntax)attribute.ApplicationSyntaxReference!.GetSyntax(cancellationToken)).ArgumentList!.Arguments
            .Select(static argument => argument.NameEquals)
            .OfType<NameEqualsSyntax>()
            .First(static name => name.Name.Identifier.ValueText == "Unicode")
            .Parent!
            .GetLocation();

    /// <summary>The options a pattern is read under.</summary>
    /// <param name="IgnoreCase">Whether <c>RegexOptions.IgnoreCase</c> is set.</param>
    /// <param name="CultureInvariant">Whether <c>RegexOptions.CultureInvariant</c> is set.</param>
    /// <param name="EcmaScript">Whether <c>RegexOptions.ECMAScript</c> is set.</param>
    private readonly record struct Reading(bool IgnoreCase, bool CultureInvariant, bool EcmaScript);
}
