using Microsoft.AspNetCore.JsonPatch.Internal;
using Newtonsoft.Json.Serialization;

namespace AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.Patching;

/// <summary>
/// JSON Patch's adapter of an object's members, reading a value object member's value through its rules.
/// </summary>
/// <remarks>
/// A member of any other type is left to JSON Patch, its conversion and its messages included.
/// </remarks>
/// <param name="conversion">The conversion of the document's values.</param>
internal sealed class ValueObjectPocoAdapter(ValueObjectPatchConversion conversion) : PocoAdapter
{
    /// <inheritdoc />
    public override bool TryAdd(object target, string segment, IContractResolver contractResolver, object value, out string errorMessage)
        => conversion.Explain(base.TryAdd(target, segment, contractResolver, value, out errorMessage), ref errorMessage);

    /// <inheritdoc />
    public override bool TryReplace(object target, string segment, IContractResolver contractResolver, object value, out string errorMessage)
        => conversion.Explain(base.TryReplace(target, segment, contractResolver, value, out errorMessage), ref errorMessage);

    /// <inheritdoc />
    /// <remarks>
    /// A value object member that cannot be <see langword="null"/> cannot be deleted, and JSON Patch would set it to its
    /// default instance, which the library treats as no value: it is refused, as a <c>null</c> read into it is.
    /// </remarks>
    public override bool TryRemove(object target, string segment, IContractResolver contractResolver, out string errorMessage)
    {
        if (TryGetJsonProperty(target, contractResolver, segment, out var property)
            && property.Writable
            && Nullable.GetUnderlyingType(property.PropertyType!) is null
            && ValueObjectPatchConversion.Handles(property.PropertyType!))
        {
            errorMessage = conversion.Refuse(
                ValueObjectErrorCodes.Required,
                $"The value cannot be removed: a valid {property.PropertyType!.Name} is required.");
            return false;
        }

        return base.TryRemove(target, segment, contractResolver, out errorMessage);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A value object member is compared through the value object's equality, its normalization and its comparison
    /// included, rather than through the text JSON Patch writes of each, and neither value is quoted when they differ.
    /// </remarks>
    public override bool TryTest(object target, string segment, IContractResolver contractResolver, object value, out string errorMessage)
    {
        if (!TryGetJsonProperty(target, contractResolver, segment, out var property)
            || !property.Readable
            || !ValueObjectPatchConversion.Handles(property.PropertyType!))
        {
            return base.TryTest(target, segment, contractResolver, value, out errorMessage);
        }

        if (!conversion.TryConvert(value, property.PropertyType!, out var expected))
        {
            errorMessage = conversion.Message!;
            return false;
        }

        if (Equals(property.ValueProvider!.GetValue(target), expected))
        {
            errorMessage = null!;
            return true;
        }

        errorMessage = $"The current value at path '{segment}' is not equal to the test value.";
        return false;
    }

    /// <inheritdoc />
    protected override bool TryConvertValue(object value, Type propertyType, IContractResolver contractResolver, out object convertedValue)
    {
        if (!ValueObjectPatchConversion.Handles(propertyType))
        {
            return base.TryConvertValue(value, propertyType, contractResolver, out convertedValue);
        }

        var converted = conversion.TryConvert(value, propertyType, out var result);
        convertedValue = result!;
        return converted;
    }
}
