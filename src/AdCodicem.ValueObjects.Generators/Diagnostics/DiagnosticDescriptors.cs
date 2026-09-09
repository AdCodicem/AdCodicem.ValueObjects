using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.Generators.Diagnostics;

/// <summary>
/// The diagnostics reported while generating value objects.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "AdCodicem.ValueObjects";

    public static readonly DiagnosticDescriptor MustBePartial = Error(
        "VO0001",
        "Value object must be partial",
        "'{0}' is annotated with [ValueObject<T>] but is not declared partial, so the generated members cannot be added to it");

    public static readonly DiagnosticDescriptor MustBeReadOnlyStruct = Error(
        "VO0002",
        "Value object must be a readonly struct",
        "'{0}' must be declared as a 'readonly partial struct'. A record struct is rejected on purpose because its "
        + "'with' expression and field-wise equality would bypass both validation and the configured comparison.");

    public static readonly DiagnosticDescriptor UnsupportedUnderlyingType = Error(
        "VO0003",
        "Unsupported underlying type",
        "'{0}' cannot be the underlying type of a value object. Supported types are {1}.");

    public static readonly DiagnosticDescriptor InvalidBound = Error(
        "VO0004",
        "Invalid bound",
        "'{0}' is not a valid {1} for underlying type '{2}'. Write it in invariant culture.");

    public static readonly DiagnosticDescriptor ClosedSetWithoutValues = Error(
        "VO0005",
        "Closed value set declares no value",
        "'{0}' declares a closed value set but no known value, so no value could ever be valid");

    public static readonly DiagnosticDescriptor InvalidKnownValueName = Error(
        "VO0006",
        "Invalid known value name",
        "'{0}' is not usable as the name of a generated member on '{1}'");

    public static readonly DiagnosticDescriptor ArithmeticRequiresNumeric = Error(
        "VO0007",
        "Arithmetic requires a numeric underlying type",
        "'{0}' requests arithmetic operators but its underlying type '{1}' is not numeric");

    public static readonly DiagnosticDescriptor LengthRequiresString = Warning(
        "VO0008",
        "Length constraints only apply to strings",
        "MinLength and MaxLength are ignored on '{0}' because its underlying type '{1}' is not a string");

    public static readonly DiagnosticDescriptor ContainingTypeMustBePartial = Error(
        "VO0009",
        "Containing type must be partial",
        "'{0}' is nested in '{1}', which is not declared partial");

    public static readonly DiagnosticDescriptor InvalidKnownValueLiteral = Error(
        "VO0013",
        "Invalid known value",
        "The known value '{0}' declared on '{1}' cannot be converted to the underlying type '{2}'");

    public static readonly DiagnosticDescriptor InvalidPattern = Error(
        "VO0014",
        "Invalid pattern",
        "The pattern declared on '{0}' is not a valid regular expression: {1}");

    public static readonly DiagnosticDescriptor InvalidEntityIdPrefix = Error(
        "VO0015",
        "Invalid entity identifier prefix",
        "The prefix '{0}' declared on '{1}' is unusable because {2}. Write one or more lowercase segments "
        + "separated by '_', each opening on a letter, such as \"acc\" or \"sk_live\".");

    public static readonly DiagnosticDescriptor DuplicateEntityIdPrefix = Error(
        "VO0016",
        "Duplicate entity identifier prefix",
        "'{0}' and '{1}' both declare the prefix '{2}'. A prefix identifies one type and one only, otherwise "
        + "an identifier of one kind parses as another and the confusion the prefix exists to prevent is back.");

    public static readonly DiagnosticDescriptor EntityIdOwnsNormalization = Error(
        "VO0017",
        "Entity identifier owns its normalization",
        "'{0}' declares a normalization hook, but [EntityId] generates the normalization of the format itself "
        + "and would never call it. Remove the hook, or drop [EntityId] and declare the type as a value object.");

    public static readonly DiagnosticDescriptor ConflictingValueObjectAnnotations = Error(
        "VO0018",
        "Conflicting value object annotations",
        "'{0}' carries both [EntityId] and [ValueObject<T>]. Each of them generates a whole implementation, so "
        + "keep the one that describes the type.");

    private static DiagnosticDescriptor Error(string id, string title, string messageFormat)
        => new(id, title, messageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static DiagnosticDescriptor Warning(string id, string title, string messageFormat)
        => new(id, title, messageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
