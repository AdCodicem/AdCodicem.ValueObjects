using System.Collections;
using Microsoft.AspNetCore.JsonPatch.Internal;
using Newtonsoft.Json.Serialization;

namespace AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.Patching;

/// <summary>
/// JSON Patch's adapter of a list, reading a value object element through its rules.
/// </summary>
/// <remarks>
/// A list of any other element type is left to JSON Patch, its conversion and its messages included, but for the end of
/// the list, <c>-</c>, where it holds no element: JSON Patch reads the element at index -1 there and throws, which this
/// reports as JSON Patch reports an index out of bounds, for every list.
/// </remarks>
/// <param name="conversion">The conversion of the document's values.</param>
internal sealed class ValueObjectListAdapter(ValueObjectPatchConversion conversion) : ListAdapter
{
    /// <inheritdoc />
    public override bool TryGet(object target, string segment, IContractResolver contractResolver, out object value, out string errorMessage)
    {
        value = null!;
        return !IsEndOfEmptyList(target, segment, out errorMessage)
            && base.TryGet(target, segment, contractResolver, out value, out errorMessage);
    }

    /// <inheritdoc />
    public override bool TryRemove(object target, string segment, IContractResolver contractResolver, out string errorMessage)
        => !IsEndOfEmptyList(target, segment, out errorMessage)
            && base.TryRemove(target, segment, contractResolver, out errorMessage);

    /// <inheritdoc />
    public override bool TryReplace(object target, string segment, IContractResolver contractResolver, object value, out string errorMessage)
        => !IsEndOfEmptyList(target, segment, out errorMessage)
            && base.TryReplace(target, segment, contractResolver, value, out errorMessage);

    /// <inheritdoc />
    /// <remarks>
    /// A value object element is compared through the value object's equality, and neither value is quoted when they
    /// differ. The end of the list, <c>-</c>, holds no element to test, in any list.
    /// </remarks>
    public override bool TryTest(object target, string segment, IContractResolver contractResolver, object value, out string errorMessage)
    {
        var list = (IList)target;
        if (!TryGetListTypeArgument(list, out var elementType, out errorMessage)
            || !TryGetPositionInfo(list, segment, OperationType.Replace, out var position, out errorMessage))
        {
            return false;
        }

        if (position.Type == PositionType.EndOfList)
        {
            errorMessage = OutOfBounds(segment);
            return false;
        }

        if (!ValueObjectPatchConversion.Handles(elementType))
        {
            return base.TryTest(target, segment, contractResolver, value, out errorMessage);
        }

        if (!TryConvertValue(value, elementType, segment, contractResolver, out var expected, out errorMessage))
        {
            return false;
        }

        if (Equals(list[position.Index], expected))
        {
            errorMessage = null!;
            return true;
        }

        errorMessage = $"The current value at position '{position.Index}' is not equal to the test value.";
        return false;
    }

    /// <inheritdoc />
    protected override bool TryConvertValue(
        object originalValue,
        Type listTypeArgument,
        string segment,
        IContractResolver contractResolver,
        out object convertedValue,
        out string errorMessage)
    {
        if (!ValueObjectPatchConversion.Handles(listTypeArgument))
        {
            return base.TryConvertValue(originalValue, listTypeArgument, segment, contractResolver, out convertedValue, out errorMessage);
        }

        var converted = conversion.TryConvert(originalValue, listTypeArgument, out var result);
        convertedValue = result!;
        errorMessage = converted ? null! : conversion.Message!;
        return converted;
    }

    /// <summary>
    /// Tells the end of an empty list, which holds no element to get, remove or replace.
    /// </summary>
    /// <param name="target">The list.</param>
    /// <param name="segment">The last segment of the path.</param>
    /// <param name="errorMessage">JSON Patch's message for an index out of bounds, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for <c>-</c> in an empty list.</returns>
    private static bool IsEndOfEmptyList(object target, string segment, out string errorMessage)
    {
        if (segment == "-" && ((IList)target).Count == 0)
        {
            errorMessage = OutOfBounds(segment);
            return true;
        }

        errorMessage = null!;
        return false;
    }

    /// <summary>
    /// Gives JSON Patch's message for an index out of bounds.
    /// </summary>
    /// <param name="segment">The segment.</param>
    /// <returns>The message.</returns>
    private static string OutOfBounds(string segment) => $"The index value provided by path segment '{segment}' is out of bounds of the array size.";
}
