using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Generators.Diagnostics;
using AdCodicem.ValueObjects.Generators.Emit;
using AdCodicem.ValueObjects.Generators.Internal;
using AdCodicem.ValueObjects.Generators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AdCodicem.ValueObjects.Generators;

/// <summary>
/// Generates the full implementation of the value objects annotated with <c>[ValueObject&lt;T&gt;]</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ValueObjectGenerator : IIncrementalGenerator
{
    private const string ValueObjectAttributeName = "AdCodicem.ValueObjects.Annotations.ValueObjectAttribute`1";
    private const string KnownValueAttributeName = "AdCodicem.ValueObjects.Annotations.KnownValueAttribute";
    private const string JsonRegistryTypeName = "AdCodicem.ValueObjects.Json.ValueObjectJsonRegistry";

    /// <summary>
    /// Fully qualified names without the C# keyword shorthand, so that <c>string</c> reads as
    /// <c>global::System.String</c> and matches the underlying type table.
    /// </summary>
    private const string HookNamespace = "AdCodicem.ValueObjects";

    private static readonly SymbolDisplayFormat QualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private static readonly string[] LineSeparators = ["\r\n", "\n"];

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var parsed = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ValueObjectAttributeName,
                // Any type declaration is admitted so that a value object written as a class or a record struct
                // reaches VO0002. Narrowing to a struct here would make the attribute silently do nothing.
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (attributeContext, _) => Parse(attributeContext))
            .WithTrackingName("ValueObjects");

        context.RegisterSourceOutput(parsed, static (production, result) =>
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                production.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            if (result.Model is not null)
            {
                production.AddSource(result.Model.HintName, SourceText.From(ValueObjectEmitter.Emit(result.Model), Encoding.UTF8));
            }
        });

        var models = parsed
            .Select(static (result, _) => result.Model)
            .Where(static model => model is not null)
            .Collect();

        // Reduced to a bool so that the compilation changing on every keystroke does not invalidate the output.
        var jsonPackageReferenced = context.CompilationProvider.Select(static (compilation, _) =>
            compilation.GetTypeByMetadataName(JsonRegistryTypeName) is not null);

        context.RegisterSourceOutput(models.Combine(jsonPackageReferenced), static (production, input) =>
        {
            var (collected, withJson) = input;
            if (collected.Length == 0)
            {
                return;
            }

            var source = RegistrationEmitter.Emit(
                collected.Select(static model => model!).ToImmutableArray(),
                withJson);

            production.AddSource("ValueObjectRegistration.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static ParseResult Parse(GeneratorAttributeSyntaxContext context)
    {
        var diagnostics = new List<DiagnosticInfo>();

        if (context.TargetSymbol is not INamedTypeSymbol symbol || context.TargetNode is not TypeDeclarationSyntax declaration)
        {
            return new ParseResult(null, EquatableArray<DiagnosticInfo>.Empty);
        }

        var location = declaration.Identifier.GetLocation();

        if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            diagnostics.Add(DiagnosticInfo.Create(DiagnosticDescriptors.MustBePartial, location, symbol.Name));
        }

        // A class, an interface and a record struct all reach here so that each is told why it was rejected,
        // rather than being handed a type missing every member the attribute promised.
        if (declaration is not StructDeclarationSyntax || !symbol.IsReadOnly || symbol.IsRecord)
        {
            diagnostics.Add(DiagnosticInfo.Create(DiagnosticDescriptors.MustBeReadOnlyStruct, location, symbol.Name));

            return new ParseResult(null, EquatableArray<DiagnosticInfo>.From(diagnostics));
        }

        var containingTypes = new List<string>();
        for (var containing = symbol.ContainingType; containing is not null; containing = containing.ContainingType)
        {
            if (!containing.DeclaringSyntaxReferences
                    .Select(static reference => reference.GetSyntax())
                    .OfType<TypeDeclarationSyntax>()
                    .Any(static syntax => syntax.Modifiers.Any(SyntaxKind.PartialKeyword)))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.ContainingTypeMustBePartial, location, symbol.Name, containing.Name));
            }

            containingTypes.Insert(0, $"{DeclarationKeyword(containing)} {containing.Name}");
        }

        var attribute = context.Attributes[0];
        var underlyingSymbol = attribute.AttributeClass?.TypeArguments.FirstOrDefault();
        var underlyingName = underlyingSymbol?.ToDisplayString(QualifiedFormat) ?? string.Empty;

        if (!UnderlyingType.TryResolve(underlyingName, out var underlying))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.UnsupportedUnderlyingType,
                location,
                underlyingSymbol?.ToDisplayString() ?? "?",
                string.Join(", ", UnderlyingType.SupportedNames)));

            return new ParseResult(null, EquatableArray<DiagnosticInfo>.From(diagnostics));
        }

        var arguments = attribute.NamedArguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        var arithmetic = GetBool(arguments, "Arithmetic");
        if (arithmetic && !underlying.IsNumeric)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.ArithmeticRequiresNumeric, location, symbol.Name, underlying.Keyword));
            arithmetic = false;
        }

        var minLength = GetInt32(arguments, "MinLength");
        var maxLength = GetInt32(arguments, "MaxLength");
        if (!underlying.IsString && (minLength >= 0 || maxLength >= 0))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.LengthRequiresString, location, symbol.Name, underlying.Keyword));
            minLength = -1;
            maxLength = -1;
        }

        var pattern = GetString(arguments, "Pattern");
        if (pattern is not null && !IsValidRegex(pattern, out var regexError))
        {
            diagnostics.Add(DiagnosticInfo.Create(DiagnosticDescriptors.InvalidPattern, location, symbol.Name, regexError));
            pattern = null;
        }

        var minimumText = GetString(arguments, "Minimum");
        var maximumText = GetString(arguments, "Maximum");
        var minimumLiteral = ParseBound(underlying, minimumText, "Minimum", symbol, location, diagnostics);
        var maximumLiteral = ParseBound(underlying, maximumText, "Maximum", symbol, location, diagnostics);

        var isClosed = GetEnumName(arguments, "ValueSet") == "Closed";
        var knownValues = ParseKnownValues(symbol, underlying, location, diagnostics);

        if (isClosed && knownValues.Count == 0)
        {
            diagnostics.Add(DiagnosticInfo.Create(DiagnosticDescriptors.ClosedSetWithoutValues, location, symbol.Name));
            isClosed = false;
        }

        var summary = ExtractSummary(symbol, declaration);

        var model = new ValueObjectModel
        {
            Namespace = symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString(),
            TypeName = symbol.Name,
            QualifiedName = symbol.ToDisplayString(QualifiedFormat),
            ContainingTypes = EquatableArray<string>.From(containingTypes),
            Kind = underlying.Kind,
            UnderlyingFullName = underlying.FullName,
            HintName = BuildHintName(symbol),
            XmlSummary = summary,
            ComparisonName = GetEnumName(arguments, "Comparison") ?? "Ordinal",
            ImplicitConversionToValue = GetBool(arguments, "ImplicitConversionToValue"),
            ExplicitConversionFromValue = GetBool(arguments, "ExplicitConversionFromValue"),
            Arithmetic = arithmetic,
            IsClosedValueSet = isClosed,
            AllowEmpty = GetBool(arguments, "AllowEmpty"),
            Pattern = pattern,
            MinLength = minLength,
            MaxLength = maxLength,
            MinimumLiteral = minimumLiteral,
            MaximumLiteral = maximumLiteral,
            MinimumText = minimumLiteral is null ? null : minimumText,
            MaximumText = maximumLiteral is null ? null : maximumText,
            SchemaFormat = GetString(arguments, "SchemaFormat") ?? underlying.SchemaFormat,
            Example = GetString(arguments, "Example"),
            Description = GetString(arguments, "Description") ?? summary,
            HasNormalizeHook = ImplementsHook(symbol, "IValueObjectNormalizer`1"),
            HasSpanNormalizeHook = underlying.IsString && ImplementsHook(symbol, "IValueObjectSpanNormalizer"),
            HasValidateHook = ImplementsHook(symbol, "IValueObjectValidator`1"),
            HasTryFormatHook = ImplementsHook(symbol, "IValueObjectFormatter`1"),
            HasFormatHook = ImplementsHook(symbol, "IValueObjectStringFormatter`1"),
            KnownValues = EquatableArray<KnownValueModel>.From(knownValues),
        };

        return new ParseResult(model, EquatableArray<DiagnosticInfo>.From(diagnostics));
    }

    private static List<KnownValueModel> ParseKnownValues(
        INamedTypeSymbol symbol,
        UnderlyingType underlying,
        Location location,
        List<DiagnosticInfo> diagnostics)
    {
        var knownValues = new List<KnownValueModel>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != KnownValueAttributeName.Replace("`1", string.Empty))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length < 2)
            {
                continue;
            }

            var name = attribute.ConstructorArguments[0].Value as string;
            var rawValue = attribute.ConstructorArguments[1].Value;

            if (string.IsNullOrEmpty(name) || !SyntaxFacts.IsValidIdentifier(name) || !names.Add(name!))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.InvalidKnownValueName, location, name ?? "?", symbol.Name));
                continue;
            }

            if (!LiteralFactory.TryCreate(underlying, rawValue, out var literal))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.InvalidKnownValueLiteral,
                    location,
                    rawValue?.ToString() ?? "null",
                    symbol.Name,
                    underlying.Keyword));
                continue;
            }

            var description = attribute.NamedArguments
                .FirstOrDefault(pair => string.Equals(pair.Key, "Description", StringComparison.Ordinal))
                .Value.Value as string;

            knownValues.Add(new KnownValueModel(name!, literal, description));
        }

        return knownValues;
    }

    private static string? ParseBound(
        UnderlyingType underlying,
        string? text,
        string boundName,
        INamedTypeSymbol symbol,
        Location location,
        List<DiagnosticInfo> diagnostics)
    {
        if (text is null)
        {
            return null;
        }

        if (LiteralFactory.TryCreate(underlying, text, out var literal))
        {
            return literal;
        }

        diagnostics.Add(DiagnosticInfo.Create(
            DiagnosticDescriptors.InvalidBound, location, text, boundName, underlying.Keyword));

        _ = symbol;
        return null;
    }

    /// <summary>
    /// Whether the value object implements one of the hook interfaces.
    /// </summary>
    /// <remarks>
    /// Hooks are declared by implementing an interface rather than by naming a member, so the compiler checks
    /// the signature and a misspelled or mis-signed rule fails the build instead of being silently ignored.
    /// </remarks>
    /// <param name="symbol">The value object.</param>
    /// <param name="metadataName">Unqualified metadata name of the hook interface, arity included.</param>
    /// <returns><see langword="true"/> when the interface is implemented.</returns>
    private static bool ImplementsHook(INamedTypeSymbol symbol, string metadataName)
        => symbol.AllInterfaces.Any(candidate =>
            string.Equals(candidate.MetadataName, metadataName, StringComparison.Ordinal)
            && candidate.ContainingNamespace.ToDisplayString() == HookNamespace);

    private static string DeclarationKeyword(INamedTypeSymbol symbol) => symbol switch
    {
        { IsRecord: true, TypeKind: TypeKind.Struct } => "partial record struct",
        { IsRecord: true } => "partial record",
        { TypeKind: TypeKind.Struct } => "partial struct",
        _ => "partial class",
    };

    private static string BuildHintName(INamedTypeSymbol symbol)
    {
        var qualified = symbol.ToDisplayString(QualifiedFormat).Replace("global::", string.Empty);

        var builder = new StringBuilder(qualified.Length + 8);
        foreach (var character in qualified)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '.' ? character : '_');
        }

        return builder.Append(".g.cs").ToString();
    }

    private static bool IsValidRegex(string pattern, out string error)
    {
        try
        {
            _ = new Regex(pattern);
            error = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message.Replace("\r", " ").Replace("\n", " ");
            return false;
        }
    }

    /// <summary>
    /// Reads the summary of the declaring type.
    /// </summary>
    /// <param name="symbol">Declared value object.</param>
    /// <param name="declaration">Its syntax.</param>
    /// <returns>The summary text, or <see langword="null"/> when the type has none.</returns>
    /// <remarks>
    /// A project that does not produce a documentation file compiles with <c>DocumentationMode.None</c>, and
    /// <c>GetDocumentationCommentXml</c> then returns nothing at all. Since most consumers leave that setting
    /// off, the trivia is read directly as a fallback.
    /// </remarks>
    private static string? ExtractSummary(INamedTypeSymbol symbol, TypeDeclarationSyntax declaration)
    {
        var fromCompilation = ExtractSummary(symbol.GetDocumentationCommentXml());
        if (fromCompilation is not null)
        {
            return fromCompilation;
        }

        var builder = new StringBuilder();
        foreach (var line in declaration.GetLeadingTrivia().ToFullString().Split(LineSeparators, StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("///", StringComparison.Ordinal))
            {
                builder.Append(trimmed, 3, trimmed.Length - 3).Append(' ');
            }
        }

        return ExtractSummary(builder.ToString());
    }

    private static string? ExtractSummary(string? documentation)
    {
        if (string.IsNullOrWhiteSpace(documentation))
        {
            return null;
        }

        const string open = "<summary>";
        const string close = "</summary>";

        var start = documentation!.IndexOf(open, StringComparison.Ordinal);
        var end = documentation.IndexOf(close, StringComparison.Ordinal);
        if (start < 0 || end < start)
        {
            return null;
        }

        var summary = documentation.Substring(start + open.Length, end - start - open.Length);
        var collapsed = Regex.Replace(summary, @"\s+", " ").Trim();

        return collapsed.Length == 0 ? null : collapsed;
    }

    private static bool GetBool(Dictionary<string, TypedConstant> arguments, string name)
        => arguments.TryGetValue(name, out var value) && value.Value is bool boolean && boolean;

    private static int GetInt32(Dictionary<string, TypedConstant> arguments, string name)
        => arguments.TryGetValue(name, out var value) && value.Value is int number ? number : -1;

    private static string? GetString(Dictionary<string, TypedConstant> arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value.Value is not string text)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? GetEnumName(Dictionary<string, TypedConstant> arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value.Value is null || value.Type is not INamedTypeSymbol enumType)
        {
            return null;
        }

        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
        {
            if (member.HasConstantValue && Equals(member.ConstantValue, value.Value))
            {
                return member.Name;
            }
        }

        return null;
    }

    private readonly record struct ParseResult(ValueObjectModel? Model, EquatableArray<DiagnosticInfo> Diagnostics);
}
