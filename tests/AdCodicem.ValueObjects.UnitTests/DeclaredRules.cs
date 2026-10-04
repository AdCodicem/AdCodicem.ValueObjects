using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// Reads the rules a value object written by hand states outside its schema - its <c>[ValueObject&lt;T&gt;]</c>
/// annotation, its <c>[KnownValue]</c> attributes and the hooks it implements - into a schema, so that a test can hold
/// the schema the type declares to them.
/// </summary>
/// <remarks>
/// The registry read a hand-written type this way until <see cref="IValueObject{TSelf, TValue}.Schema"/> joined the
/// contract; it now reads the schema alone. Known values are normalized through the type, or parsed when the attribute
/// had to take them as text, as the generator publishes them, and keep their names and descriptions, a blank description
/// being none.
/// </remarks>
internal static class DeclaredRules
{
    public static ValueObjectSchema Read(Type valueObjectType, Type valueType)
    {
        var attribute = valueObjectType.GetCustomAttributes(inherit: false)
            .SingleOrDefault(candidate => candidate.GetType().IsGenericType
                                          && candidate.GetType().GetGenericTypeDefinition() == typeof(ValueObjectAttribute<>));

        var pattern = typeof(IValueObjectPatternValidator).IsAssignableFrom(valueObjectType)
            ? Invoke(typeof(ValueObjectPattern), nameof(ValueObjectPattern.Of), [valueObjectType])!.ToString()
            : Option(attribute, "Pattern");
        var minimum = typeof(IValueObjectMinimum<>).MakeGenericType(valueType).IsAssignableFrom(valueObjectType)
            ? ValueObjectBound.Text(Invoke(typeof(ValueObjectBound), nameof(ValueObjectBound.Minimum), [valueObjectType, valueType]))
            : Option(attribute, "Minimum");
        var maximum = typeof(IValueObjectMaximum<>).MakeGenericType(valueType).IsAssignableFrom(valueObjectType)
            ? ValueObjectBound.Text(Invoke(typeof(ValueObjectBound), nameof(ValueObjectBound.Maximum), [valueObjectType, valueType]))
            : Option(attribute, "Maximum");

        var known = valueObjectType.GetCustomAttributes<KnownValueAttribute>(inherit: false)
            .Select(declared => new KnownValueInfo(
                Normalize(valueObjectType, declared.Value),
                declared.Name,
                string.IsNullOrWhiteSpace(declared.Description) ? null : declared.Description))
            .ToImmutableArray();

        return new ValueObjectSchema
        {
            Pattern = pattern,
            MinLength = Length(attribute, nameof(ValueObjectAttribute<object>.MinLength)),
            MaxLength = Length(attribute, nameof(ValueObjectAttribute<object>.MaxLength)),
            Minimum = minimum,
            Maximum = maximum,
            Format = Option(attribute, nameof(ValueObjectAttribute<object>.SchemaFormat)),
            Description = Option(attribute, nameof(ValueObjectAttribute<object>.Description)),
            Example = Option(attribute, nameof(ValueObjectAttribute<object>.Example)),
            IsClosedValueSet = Option(attribute, nameof(ValueObjectAttribute<object>.ValueSet)) == nameof(ValueSetKind.Closed),
            KnownValues = [.. known.Select(declared => declared.Value)],
            KnownValueDetails = known,
        };
    }

    /// <summary>Reads an option of the annotation by name, the deprecated ones included, which naming would report.</summary>
    private static string? Option(object? attribute, string name)
        => attribute?.GetType().GetProperty(name)!.GetValue(attribute)?.ToString();

    private static int? Length(object? attribute, string name)
        => attribute?.GetType().GetProperty(name)!.GetValue(attribute) is int length and >= 0 ? length : null;

    private static object? Invoke(Type owner, string method, Type[] typeArguments)
        => owner.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(typeArguments).Invoke(null, null);

    /// <summary>Turns a declared known value into the value the type holds, or leaves what the type refuses as written.</summary>
    private static object Normalize(Type valueObjectType, object declared)
    {
        ValueObjectRegistry.TryResolve(valueObjectType, out var descriptor).Should().BeTrue();

        var accepted = declared.GetType() == descriptor!.ValueType
            ? descriptor.TryCreate(declared, out var created, out _)
            : descriptor.TryParse(Convert.ToString(declared, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, out created, out _);

        return accepted ? descriptor.GetValue(created!)! : declared;
    }
}
