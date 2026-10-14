using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// What the tests of <c>VO0045</c> and <c>VO0046</c> compile against: a domain assembly of value objects, some classified
/// on their type, and the logging references, with Microsoft.Gen.Logging running.
/// </summary>
/// <remarks>
/// The domain reaches each snippet as metadata, as a domain project reaches the project that logs: the logging generator
/// sees the members of its value objects, which it never sees of a value object generated in the snippet itself.
/// </remarks>
internal static partial class LoggingSnippets
{
    /// <summary>The domain assembly's source, in the namespace <c>Shop</c>.</summary>
    public const string Domain = """
        using System;
        using AdCodicem.ValueObjects;
        using AdCodicem.ValueObjects.Annotations;
        using AdCodicem.ValueObjects.Identifiers;
        using Microsoft.Extensions.Compliance.Classification;

        namespace Shop;

        public static class Taxonomy
        {
            public static DataClassification Personal => new("Shop", nameof(Personal));

            public static DataClassification Financial => new("Shop", nameof(Financial));
        }

        public sealed class PersonalDataAttribute : DataClassificationAttribute
        {
            public PersonalDataAttribute() : base(Taxonomy.Personal) { }
        }

        public sealed class FinancialDataAttribute : DataClassificationAttribute
        {
            public FinancialDataAttribute() : base(Taxonomy.Financial) { }
        }

        [ValueObject<string>(MinLength = 15, MaxLength = 34)]
        public readonly partial struct Iban;

        [ValueObject<int>]
        public readonly partial struct Quantity;

        [PersonalData]
        [ValueObject<string>(MaxLength = 254)]
        public readonly partial struct RefEmail;

        [FinancialData]
        [ValueObject<decimal>]
        public readonly partial struct Salary;

        [PersonalData]
        [ValueObject<string>(MaxLength = 12)]
        public readonly partial struct Reference<TOwner>;

        public sealed class PurchaseOrder;

        [ValueObject<string>(MaxLength = 12)]
        public readonly partial struct Ticket<TOwner>;

        [UnknownDataClassification]
        [ValueObject<string>]
        public readonly partial struct Unknownish;

        [NoDataClassification, PersonalData]
        [ValueObject<string>]
        public readonly partial struct Both;

        [NoDataClassification]
        [ValueObject<string>]
        public readonly partial struct PublicCode;

        [PersonalData]
        [EntityId("pat")]
        public readonly partial struct RefPatientId;

        [EntityId("cus")]
        public readonly partial struct CustomerId;

        public sealed record RefHolder(RefEmail Email, Quantity Qty);

        public sealed record RefHolderClassified([property: PersonalData] Iban Iban, Quantity Qty);

        public sealed record RefVisit(
            CustomerId Customer,
            RefPatientId Patient,
            [property: PersonalData] CustomerId Classified,
            [property: NoDataClassification] Iban Public);

        /// <summary>A value object written by hand, classified on its type.</summary>
        [PersonalData]
        public readonly struct HandWrittenSecret : IValueObject<HandWrittenSecret, string>
        {
            private readonly string? _value;
            private HandWrittenSecret(string value) => _value = value;
            public static AdCodicem.ValueObjects.Metadata.ValueObjectSchema Schema => AdCodicem.ValueObjects.Metadata.ValueObjectSchema.Unconstrained;
            public string Value => _value ?? string.Empty;
            public bool IsDefault => _value is null;
            public static string Normalize(string value) => value;
            public static ValidationResult Validate(in string value) => ValidationResult.Success;
            public static HandWrittenSecret Create(string value) => new(value);
            public static bool TryCreate(string value, out HandWrittenSecret result) => TryCreate(value, out result, out _);
            public static bool TryCreate(string value, out HandWrittenSecret result, out ValidationResult validation) { validation = default; result = new(value); return true; }
            public static HandWrittenSecret CreateUnchecked(string value) => new(value);
            public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out HandWrittenSecret result, out ValidationResult validation) => TryCreate(text.ToString(), out result, out validation);
            public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out HandWrittenSecret result) => TryParse(s, provider, out result, out _);
            public static bool TryParse(string? s, IFormatProvider? provider, out HandWrittenSecret result) => TryParse(s.AsSpan(), provider, out result, out _);
            public static HandWrittenSecret Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());
            public static HandWrittenSecret Parse(string s, IFormatProvider? provider) => Create(s);
            public bool Equals(HandWrittenSecret other) => Value == other.Value;
            public override bool Equals(object? obj) => obj is HandWrittenSecret other && Equals(other);
            public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
            public int CompareTo(HandWrittenSecret other) => string.CompareOrdinal(Value, other.Value);
            public override string ToString() => Value;
            public string ToString(string? format, IFormatProvider? formatProvider) => Value;
            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) { charsWritten = 0; return Value.TryCopyTo(destination) && (charsWritten = Value.Length) >= 0; }
            public static bool operator ==(HandWrittenSecret left, HandWrittenSecret right) => left.Equals(right);
            public static bool operator !=(HandWrittenSecret left, HandWrittenSecret right) => !left.Equals(right);
            public static bool operator <(HandWrittenSecret left, HandWrittenSecret right) => left.CompareTo(right) < 0;
            public static bool operator >(HandWrittenSecret left, HandWrittenSecret right) => left.CompareTo(right) > 0;
            public static bool operator <=(HandWrittenSecret left, HandWrittenSecret right) => left.CompareTo(right) <= 0;
            public static bool operator >=(HandWrittenSecret left, HandWrittenSecret right) => left.CompareTo(right) >= 0;
        }

        """;

    /// <summary>The usings every snippet starts with, the experimental <c>Transitive</c> allowed, and its namespace.</summary>
    public const string Header = """
        using System;
        using System.Collections.Generic;
        using Microsoft.Extensions.Compliance.Classification;
        using Microsoft.Extensions.Logging;
        using AdCodicem.ValueObjects;
        using AdCodicem.ValueObjects.Annotations;
        using AdCodicem.ValueObjects.Identifiers;
        using Shop;

        #pragma warning disable EXTEXP0003 // LogPropertiesAttribute.Transitive is experimental.

        namespace Snippets;

        """;

    /// <summary>The domain, as the image of an assembly of its own.</summary>
    public static readonly MetadataReference Image = MetadataReference.CreateFromImage(
        GeneratorHarness.Emit(Domain, "Shop", GeneratorHarness.LibraryReferences));

    /// <summary>What a project logging through Microsoft.Extensions.Telemetry.Abstractions compiles against, with the domain.</summary>
    public static readonly ImmutableArray<MetadataReference> WithDomain = GeneratorHarness.LoggingReferences.Add(Image);

    /// <summary>What a project logging through Microsoft.Extensions.Logging alone compiles against, with the domain.</summary>
    public static readonly ImmutableArray<MetadataReference> PlainWithDomain = GeneratorHarness.PlainLoggingReferences.Add(Image);

    /// <summary>
    /// Compiles a snippet with the generator and Microsoft.Gen.Logging, asserting that it compiles cleanly, then runs the
    /// analyzer over it, its diagnostics in source order.
    /// </summary>
    /// <param name="snippet">The snippet, after <see cref="Header"/>.</param>
    /// <param name="compiles">Whether the snippet must compile cleanly, which one the logging generator refuses does not.</param>
    /// <returns>The analyzer's diagnostics, and what the logging generator wrote.</returns>
    public static async Task<(ImmutableArray<Diagnostic> Diagnostics, LoggedTags Tags)> AnalyzeAsync(string snippet, bool compiles = true)
    {
        var run = GeneratorHarness.Run(Header + snippet, references: WithDomain, withLoggingGenerator: true);
        if (compiles)
        {
            run.CompilationDiagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.GetMessage()}").Should().BeEmpty();
        }

        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<DataClassificationAnalyzer>(
            Header + snippet,
            WithDomain,
            withLoggingGenerator: true);

        return ([.. diagnostics.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start).ThenBy(diagnostic => diagnostic.Id)], LoggedTags.Read(run.LoggingSource));
    }

    /// <summary>Gets the text a diagnostic spans, and the line it starts on, trimmed.</summary>
    public static (string Text, string Line) Located(Diagnostic diagnostic)
    {
        var text = diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken);
        var span = diagnostic.Location.SourceSpan;

        return (text.ToString(span), text.Lines.GetLineFromPosition(span.Start).ToString().Trim());
    }

    /// <summary>Gets the line a location starts on, trimmed.</summary>
    public static string LineOf(Location location)
    {
        var text = location.SourceTree!.GetText(TestContext.Current.CancellationToken);

        return text.Lines.GetLineFromPosition(location.SourceSpan.Start).ToString().Trim();
    }

    /// <summary>Gets the message of a diagnostic, as the invariant culture writes it.</summary>
    public static string Message(Diagnostic diagnostic) => diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The tags the code Microsoft.Gen.Logging wrote hands the logger: <see cref="Plain"/>, which no redactor reads, and
    /// <see cref="Classified"/>, each with the classifications the redactor provider picks a redactor by.
    /// </summary>
    /// <param name="Plain">The names of the tags written as they are.</param>
    /// <param name="Classified">The names of the tags handed to a redactor, each followed by its classifications.</param>
    public sealed partial record LoggedTags(IReadOnlyList<string> Plain, IReadOnlyList<string> Classified)
    {
        /// <summary>Reads the tags out of what the logging generator wrote.</summary>
        /// <param name="source">The generated source, or <see langword="null"/> when the generator did not run.</param>
        /// <returns>The tags.</returns>
        public static LoggedTags Read(string? source)
        {
            source.Should().NotBeNull("Microsoft.Gen.Logging ran");

            return new(
                [.. PlainTag().Matches(source!).Select(match => match.Groups["tag"].Value)],
                [.. ClassifiedTag().Matches(source!).Select(match =>
                    $"{match.Groups["tag"].Value} <{string.Join(", ", match.Groups["set"].Value.Split(',').Select(Shorten))}>")]);
        }

        /// <summary>Gets a classification's static field, <c>_Shop_PersonalDataAttribute</c>, as its attribute's short name.</summary>
        private static string Shorten(string field)
        {
            var name = field.Trim();

            return name[(name.LastIndexOf('_') + 1)..];
        }

        [GeneratedRegex(@"state\.TagArray\[\d+\] = new\(""(?<tag>[^""{]+)""")]
        private static partial Regex PlainTag();

        [GeneratedRegex(@"state\.ClassifiedTagArray\[\d+\] = new\(""(?<tag>[^""]+)"", [^,]+, new [^(]+\((?<set>[^)]*)\)")]
        private static partial Regex ClassifiedTag();
    }
}
