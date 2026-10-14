using System.Reflection;
using AdCodicem.ValueObjects.Annotations;
using AdCodicem.ValueObjects.Metadata;
using Namotion.Reflection;
using NJsonSchema;
using NJsonSchema.Annotations;
using NJsonSchema.Generation;

namespace AdCodicem.ValueObjects.NJsonSchema;

/// <summary>
/// Fills in the definition NJsonSchema files for a value object, and describes text a member or a parameter holds to a
/// value object's rules under <see cref="ParsesAsAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// NJsonSchema hands a schema processor every schema it generates, with the type it stands for. The definition of a value
/// object, which <see cref="ValueObjectReflectionService"/> made an object without members, is described as the value
/// object's underlying value; a schema that refers to it is left alone.
/// </para>
/// <para>
/// A <c>string</c>, or a collection of them, carrying <see cref="ParsesAsAttribute"/> is described as the text of that
/// value object, whatever its underlying type, as the OpenAPI packages describe it. NJsonSchema applies a member's or a
/// parameter's validation attributes after the processors have run: the members of an object are described again once
/// the object's schema is complete, the stricter of the member's rules and the value object's kept, and the attribute is
/// found on the constructor parameter of the same name too, where MVC requires it on a record, and on a derived type's
/// own members, which NJsonSchema's inheritance puts in a part of <c>allOf</c>. A parameter's own validation attributes
/// replace the value object's lengths and pattern.
/// </para>
/// </remarks>
internal sealed class ValueObjectSchemaProcessor : ISchemaProcessor
{
    private ValueObjectSchemaProcessor()
    {
    }

    /// <summary>Gets the processor, which holds nothing.</summary>
    internal static ValueObjectSchemaProcessor Instance { get; } = new();

    /// <inheritdoc />
    public void Process(SchemaProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var schema = context.Schema;
        var settings = context.Settings;
        var contextualType = context.ContextualType;

        // A reference to a definition, which is described.
        if (schema.HasReference)
        {
            return;
        }

        if (Annotated(contextualType))
        {
            return;
        }

        if (ValueObjectWire.TryGet(contextualType.OriginalType, settings, out var wire))
        {
            ValueObjectJsonSchemaDescription.Describe(schema, wire, nullable: false, settings);
            return;
        }

        // Text in place: a parameter, or a member before NJsonSchema applies its validation attributes.
        if (ParsedAs(contextualType.GetContextAttributes<ParsesAsAttribute>(true).FirstOrDefault()
                     ?? (contextualType.Context is ContextualPropertyInfo property ? ConstructorParameterOf(property.PropertyInfo) : null)) is { } parsed)
        {
            DescribeText(schema, parsed, settings.ReflectionService.IsNullable(contextualType, settings.DefaultReferenceTypeNullHandling), settings);
        }

        // Under NJsonSchema's inheritance, a derived type's own members sit in an inline part of allOf, which no processor
        // is handed; a reference to its base holds none.
        foreach (var members in schema.AllOf.Prepend(schema).Where(static part => part.Properties.Count > 0))
        {
            DescribeMembers(members, contextualType, settings);
        }
    }

    /// <summary>
    /// Describes again each member of an object holding text to a value object's rules, once NJsonSchema applied the
    /// member's validation attributes, keeping the stricter of their rules and the value object's.
    /// </summary>
    /// <remarks>
    /// A member a derived type hides with <see langword="new"/> is the derived type's alone, as the serializers and MVC
    /// bind it: of the members a name stands for, the one declared deepest in the hierarchy is read.
    /// </remarks>
    private static void DescribeMembers(JsonSchema schema, ContextualType contextualType, JsonSchemaGeneratorSettings settings)
    {
        var members = contextualType.Properties.Cast<ContextualAccessorInfo>().Concat(contextualType.Fields)
            .GroupBy(member => settings.ReflectionService.GetPropertyName(member, settings), StringComparer.Ordinal);
        foreach (var group in members)
        {
            var member = group.MaxBy(static member => Depth(member.MemberInfo.DeclaringType))!;
            var attribute = member.GetAttributes<ParsesAsAttribute>(true).FirstOrDefault()
                            ?? (member.MemberInfo is PropertyInfo property ? ConstructorParameterOf(property) : null);
            if (ParsedAs(attribute) is not { } parsed || !schema.Properties.TryGetValue(group.Key, out var described))
            {
                continue;
            }

            DescribeText(described, parsed, nullable: false, settings);
        }

        static int Depth(Type? type)
        {
            var depth = 0;
            for (; type is not null; type = type.BaseType)
            {
                depth++;
            }

            return depth;
        }
    }

    /// <summary>
    /// Describes text as a value object's, or each element of a collection of text, keeping the stricter rules a
    /// declaration put on it.
    /// </summary>
    private static void DescribeText(JsonSchema schema, ValueObjectDescriptor descriptor, bool nullable, JsonSchemaGeneratorSettings settings)
    {
        if ((schema.Type & JsonObjectType.Array) != 0)
        {
            if (schema.Item is not { } item)
            {
                return;
            }

            schema = item;
            nullable = false;
        }

        // A member of another type is a misuse the attribute refuses as it validates.
        if ((schema.Type & JsonObjectType.String) == 0)
        {
            return;
        }

        var declared = ValueObjectJsonSchemaDescription.RulesOf(schema);
        var wire = new ValueObjectWire(descriptor, ValueObjectWire.Unconfigured, AsText: true, AsKey: true);
        ValueObjectJsonSchemaDescription.Describe(schema, wire, nullable, settings);
        ValueObjectJsonSchemaDescription.KeepStricterRules(schema, declared);
    }

    /// <summary>
    /// Tells whether a member or a type says itself how NJsonSchema describes it, with NJsonSchema's
    /// <see cref="JsonSchemaTypeAttribute"/> or <see cref="JsonSchemaAttribute"/>, which the application's choice keeps.
    /// </summary>
    internal static bool Annotated(ContextualType contextualType)
        => contextualType.GetContextOrTypeAttribute<JsonSchemaTypeAttribute>(true) is not null
           || contextualType.GetContextOrTypeAttribute<JsonSchemaAttribute>(true) is not null;

    /// <summary>Finds the value object text is held to.</summary>
    private static ValueObjectDescriptor? ParsedAs(ParsesAsAttribute? attribute)
        => attribute is not null && ValueObjectRegistry.TryResolve(attribute.ValueObjectType, out var descriptor) ? descriptor : null;

    /// <summary>
    /// Gets the <see cref="ParsesAsAttribute"/> a constructor parameter named after a property carries, as a record's
    /// does, the property taking the name of its parameter.
    /// </summary>
    private static ParsesAsAttribute? ConstructorParameterOf(PropertyInfo property)
        => property.DeclaringType!.GetConstructors()
            .SelectMany(static constructor => constructor.GetParameters())
            .Where(parameter => string.Equals(parameter.Name, property.Name, StringComparison.OrdinalIgnoreCase))
            .Select(static parameter => parameter.GetCustomAttributes(typeof(ParsesAsAttribute), inherit: true).OfType<ParsesAsAttribute>().FirstOrDefault())
            .FirstOrDefault(static attribute => attribute is not null);
}
