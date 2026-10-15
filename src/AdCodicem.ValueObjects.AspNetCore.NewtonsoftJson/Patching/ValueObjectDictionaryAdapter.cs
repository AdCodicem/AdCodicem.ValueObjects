using AdCodicem.ValueObjects.Metadata;
using Microsoft.AspNetCore.JsonPatch.Internal;
using Newtonsoft.Json.Serialization;

namespace AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.Patching;

/// <summary>
/// JSON Patch's adapter of a dictionary keyed by a value object, or holding them, reading each key and each value
/// object value through its rules.
/// </summary>
/// <remarks>
/// <see cref="ValueObjectAdapterFactory"/> creates it through <see cref="Activator.CreateInstance(Type, object[])"/>,
/// which finds a public constructor alone.
/// </remarks>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
/// <param name="conversion">The conversion of the document's values.</param>
/// <param name="keyDescriptor">The value object the keys are, or <see langword="null"/> for keys of another type.</param>
internal sealed class ValueObjectDictionaryAdapter<TKey, TValue>(ValueObjectPatchConversion conversion, ValueObjectDescriptor? keyDescriptor)
    : DictionaryAdapter<TKey, TValue>
{
    /// <inheritdoc />
    /// <remarks>
    /// A value object value is compared through the value object's equality, a <c>null</c> held by an optional one
    /// included, and neither value is quoted when they differ. A key refused or not held is JSON Patch's answer.
    /// </remarks>
    public override bool TryTest(object target, string segment, IContractResolver contractResolver, object value, out string errorMessage)
    {
        var dictionary = (IDictionary<TKey, TValue>)target;
        var key = ((JsonDictionaryContract)contractResolver.ResolveContract(target.GetType())).DictionaryKeyResolver!(segment);
        if (!ValueObjectPatchConversion.Handles(typeof(TValue))
            || !TryConvertKey(key, out var convertedKey, out _)
            || !dictionary.TryGetValue(convertedKey, out var current))
        {
            return base.TryTest(target, segment, contractResolver, value, out errorMessage);
        }

        if (!TryConvertValue(value, contractResolver, out var expected, out errorMessage))
        {
            return false;
        }

        if (Equals(current, expected))
        {
            errorMessage = null!;
            return true;
        }

        errorMessage = $"The current value at path '{segment}' is not equal to the test value.";
        return false;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A value object key is read from its text through the value object's rules, in the invariant culture, as a route
    /// value is read.
    /// </remarks>
    protected override bool TryConvertKey(string key, out TKey convertedKey, out string errorMessage)
    {
        if (keyDescriptor is null)
        {
            return base.TryConvertKey(key, out convertedKey, out errorMessage);
        }

        var converted = conversion.TryParseKey(key, keyDescriptor, out var result);
        convertedKey = converted ? (TKey)result! : default!;
        errorMessage = converted ? null! : conversion.Message!;
        return converted;
    }

    /// <inheritdoc />
    protected override bool TryConvertValue(object value, IContractResolver contractResolver, out TValue convertedValue, out string errorMessage)
    {
        if (!ValueObjectPatchConversion.Handles(typeof(TValue)))
        {
            return base.TryConvertValue(value, contractResolver, out convertedValue, out errorMessage);
        }

        var converted = conversion.TryConvert(value, typeof(TValue), out var result);
        convertedValue = converted ? (TValue)result! : default!;
        errorMessage = converted ? null! : conversion.Message!;
        return converted;
    }
}
