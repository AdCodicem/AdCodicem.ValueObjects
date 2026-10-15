using AdCodicem.ValueObjects.Metadata;
using Microsoft.AspNetCore.JsonPatch.Adapters;
using Microsoft.AspNetCore.JsonPatch.Internal;
using Newtonsoft.Json.Serialization;

namespace AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.Patching;

/// <summary>
/// Creates JSON Patch's adapters, and in their place, for an object, a list or a dictionary keyed by value objects or
/// holding them, ones reading value objects through their rules.
/// </summary>
/// <remarks>
/// A dictionary's adapter is closed over the dictionary's two types with <see cref="Type.MakeGenericType"/>, as JSON
/// Patch's own factory closes its own over the same types: a value object's visitor would name the value object, never
/// the dictionary's other type. MVC's JSON Patch runs in no native binary. A <c>JObject</c>, a dynamic object and a
/// dictionary holding no value object keep JSON Patch's adapters.
/// </remarks>
/// <param name="conversion">The conversion of the document's values.</param>
internal sealed class ValueObjectAdapterFactory(ValueObjectPatchConversion conversion) : AdapterFactory
{
    /// <inheritdoc />
    public override IAdapter Create(object target, IContractResolver contractResolver)
    {
        var adapter = base.Create(target, contractResolver);
        var type = adapter.GetType();
        if (type == typeof(PocoAdapter))
        {
            return new ValueObjectPocoAdapter(conversion);
        }

        if (type == typeof(ListAdapter))
        {
            return new ValueObjectListAdapter(conversion);
        }

        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        if (definition == typeof(DictionaryAdapter<,>))
        {
            var arguments = type.GetGenericArguments();
            ValueObjectRegistry.TryResolve(arguments[0], out var keyDescriptor);
            if (keyDescriptor is not null || ValueObjectPatchConversion.Handles(arguments[1]))
            {
                return (IAdapter)Activator.CreateInstance(
                    typeof(ValueObjectDictionaryAdapter<,>).MakeGenericType(arguments),
                    conversion,
                    keyDescriptor)!;
            }
        }

        return adapter;
    }
}
