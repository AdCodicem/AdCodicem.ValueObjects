using System.Collections.Immutable;
using System.Reflection;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// Reads the rules a value object written by hand states outside its schema - its <c>[ValueObject&lt;T&gt;]</c>
/// annotation, its members marked <c>[KnownValue]</c> and the hooks it implements - into a schema, so that a test can
/// hold the schema the type declares to them.
/// </summary>
/// <remarks>
/// The registry read a hand-written type this way until <see cref="IValueObject{TSelf, TValue}.Schema"/> joined the
/// contract; it now reads the schema alone. Known values are the values the members hold, in declaration order, and keep
/// their names and descriptions, a blank description being none. The example is the underlying value of the instance the
/// hook returns, as the generator publishes it.
/// </remarks>
internal static class DeclaredRules
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static ValueObjectSchema Read(Type valueObjectType, Type valueType)
    {
        var attribute = valueObjectType.GetCustomAttributes(inherit: false)
            .SingleOrDefault(candidate => candidate.GetType().IsGenericType
                                          && candidate.GetType().GetGenericTypeDefinition() == typeof(ValueObjectAttribute<>));

        var pattern = typeof(IValueObjectPatternValidator).IsAssignableFrom(valueObjectType)
            ? Invoke(typeof(ValueObjectPattern), nameof(ValueObjectPattern.Of), [valueObjectType])!.ToString()
            : null;
        var minimum = typeof(IValueObjectMinimum<>).MakeGenericType(valueType).IsAssignableFrom(valueObjectType)
            ? ValueObjectBound.Text(Invoke(typeof(ValueObjectBound), nameof(ValueObjectBound.Minimum), [valueObjectType, valueType]))
            : null;
        var maximum = typeof(IValueObjectMaximum<>).MakeGenericType(valueType).IsAssignableFrom(valueObjectType)
            ? ValueObjectBound.Text(Invoke(typeof(ValueObjectBound), nameof(ValueObjectBound.Maximum), [valueObjectType, valueType]))
            : null;
        var example = typeof(IValueObjectExample<>).MakeGenericType(valueObjectType).IsAssignableFrom(valueObjectType)
            ? ((IValueObject)Invoke(typeof(ValueObjectExample), nameof(ValueObjectExample.Of), [valueObjectType])!).GetBoxedValue()
            : null;

        var known = valueObjectType.GetMembers(Static)
            .Where(member => member is FieldInfo or PropertyInfo)
            .Select(member => (Member: member, Attribute: member.GetCustomAttribute<KnownValueAttribute>()))
            .Where(declared => declared.Attribute is not null)
            .Select(declared => new KnownValueInfo(
                ((IValueObject)(declared.Member is FieldInfo field ? field.GetValue(null) : ((PropertyInfo)declared.Member).GetValue(null))!).GetBoxedValue()!,
                declared.Member.Name,
                string.IsNullOrWhiteSpace(declared.Attribute!.Description) ? null : declared.Attribute.Description))
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
            Example = example,
            IsClosedValueSet = Option(attribute, nameof(ValueObjectAttribute<object>.ValueSet)) == nameof(ValueSetKind.Closed),
            KnownValues = [.. known.Select(declared => declared.Value)],
            KnownValueDetails = known,
        };
    }

    /// <summary>Reads an option of the annotation by name.</summary>
    private static string? Option(object? attribute, string name)
        => attribute?.GetType().GetProperty(name)!.GetValue(attribute)?.ToString();

    private static int? Length(object? attribute, string name)
        => attribute?.GetType().GetProperty(name)!.GetValue(attribute) is int length and >= 0 ? length : null;

    private static object? Invoke(Type owner, string method, Type[] typeArguments)
        => owner.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(typeArguments).Invoke(null, null);
}
