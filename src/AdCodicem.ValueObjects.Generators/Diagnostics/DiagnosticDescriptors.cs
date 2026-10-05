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
        "'{0}' must be declared as a 'readonly partial struct', not as a class, a record struct or a ref struct. A "
        + "record struct is rejected on purpose because its 'with' expression and field-wise equality would bypass "
        + "both validation and the configured comparison, and a ref struct because it can be neither boxed nor a "
        + "type argument, which the generated members require.");

    public static readonly DiagnosticDescriptor UnsupportedUnderlyingType = Error(
        "VO0003",
        "Unsupported underlying type",
        "'{0}' cannot be the underlying type of a value object. Supported types are {1}.");

    public static readonly DiagnosticDescriptor ClosedSetWithoutValues = Error(
        "VO0005",
        "Closed value set declares no value",
        "'{0}' declares a closed value set but no known value, so no value could ever be valid");

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

    public static readonly DiagnosticDescriptor UnsupportedDeclaration = Error(
        "VO0019",
        "Unsupported value object declaration",
        "'{0}' {1}, which the generator does not support. {2}.");

    public static readonly DiagnosticDescriptor UndefinedEnumValue = Error(
        "VO0020",
        "Option set to an undefined enum value",
        "'{0}' sets {1} to {2}, which '{3}' does not define. Use one of its named members.");

    // VO0021 is the identifier of the [Obsolete] on ValueObjectAttribute<T>.Pattern, which the compiler reports
    // itself: no descriptor here declares it. VO0004, VO0006, VO0013, VO0014, VO0022 and VO0029 reported text options
    // that can no longer compile (docs/adr/0011-declare-known-values-and-examples-as-typed-members.md), and are not
    // reused.
    public static readonly DiagnosticDescriptor PatternRequiresString = Error(
        "VO0023",
        "A pattern only applies to strings",
        "'{0}' implements IValueObjectPatternValidator, but its underlying type '{1}' is not a string, so the "
        + "pattern would never run. Remove the interface, or validate the value in IValueObjectValidator<T>.");

    public static readonly DiagnosticDescriptor EntityIdOwnsPattern = Error(
        "VO0024",
        "Entity identifier owns its format",
        "'{0}' implements IValueObjectPatternValidator, but [EntityId] validates its format itself and publishes "
        + "its own OpenAPI pattern. Remove the interface, or drop [EntityId] and declare the type as a value object.");

    public static readonly DiagnosticDescriptor PatternOptionsNotPublished = Warning(
        "VO0025",
        "Pattern options are not published",
        "The [GeneratedRegex] behind '{0}.Pattern' sets {1}, which the OpenAPI pattern cannot carry: clients would "
        + "check the pattern without it and disagree with the validation. Write the rule into the pattern itself, "
        + "such as [A-Za-z] for IgnoreCase.");

    public static readonly DiagnosticDescriptor PatternWithoutTimeout = Warning(
        "VO0026",
        "Pattern has no match timeout",
        "The [GeneratedRegex] behind '{0}.Pattern' sets no matchTimeoutMilliseconds, so a pathological input can "
        + "hold a request thread for as long as the match runs. Set one, such as matchTimeoutMilliseconds: 1000.");

    public static readonly DiagnosticDescriptor EntityIdTakesNoKnownValue = Error(
        "VO0027",
        "Entity identifier takes no known value",
        "'{0}' marks '{1}' as a known value, but [EntityId] has no known values and reads no [KnownValue]. Remove the "
        + "attribute: the member stays a well-known identifier of the type, "
        + "public static readonly {0} {1} = Parse(\"...\", null), for instance.");

    // VO0028 is the identifier of the [Obsolete] on ValueObjectAttribute<T>.Minimum and Maximum, which the compiler
    // reports itself: no descriptor here declares it.
    public static readonly DiagnosticDescriptor BoundHookCannotBound = Error(
        "VO0030",
        "A bound hook that cannot bound the type",
        "'{0}' implements {1}, which cannot bound a value object over '{2}'. {3}.");

    public static readonly DiagnosticDescriptor DeclaredValueRefused = Error(
        "VO0031",
        "Declared value refused by its own type",
        "The {0} '{1}' declared on '{2}' is refused by its own type ({3}): {4}");

    // VO0034 and VO0035 are the identifiers of the [Obsolete] on the constructor of [KnownValue] that takes a name and a
    // value, and on the Example options of [ValueObject<T>] and [EntityId], which the compiler reports itself.
    public static readonly DiagnosticDescriptor InvalidKnownValueMember = Error(
        "VO0036",
        "Invalid known value member",
        "'{0}' is marked [KnownValue] on '{1}', but {2}. Declare it as public static readonly {1} {0} = Known(...);, or "
        + "as a static property with a getter alone initialized the same way.");

    // VO0037 is reported by KnownValueAnalyzer, which sees the generated Known factory the generator cannot.
    public static readonly DiagnosticDescriptor ExampleHookOverAnotherType = Error(
        "VO0038",
        "An example hook over another type",
        "'{0}' implements {1}, but an example is an instance of the value object itself: implement "
        + "IValueObjectExample<{0}> instead");

    private static DiagnosticDescriptor Error(string id, string title, string messageFormat)
        => new(id, title, messageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static DiagnosticDescriptor Warning(string id, string title, string messageFormat)
        => new(id, title, messageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
