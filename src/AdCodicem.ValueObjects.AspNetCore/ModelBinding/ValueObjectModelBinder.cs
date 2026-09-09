using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AdCodicem.ValueObjects.AspNetCore.ModelBinding;

/// <summary>
/// Binds a value object from the raw text of a route segment, query string value, header or form field.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// The binder is closed over the concrete value object type, so binding costs one <c>TryParse</c> and nothing
/// else: no reflection, no <c>TypeDescriptor</c> lookup, and no exception on the rejection path. The violated
/// rule travels with the model error so that <see cref="ValueObjectProblemDetails"/> can put its stable code in
/// the response body.
/// </remarks>
public sealed class ValueObjectModelBinder<TSelf, TValue> : IModelBinder
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <inheritdoc />
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var provided = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (provided == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, provided);

        var text = provided.FirstValue;
        if (string.IsNullOrEmpty(text))
        {
            // An absent optional value binds to null; a required one is reported by the framework as missing.
            bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        if (TSelf.TryParse(text, CultureInfo.InvariantCulture, out var parsed, out var validation))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            validation.ErrorMessage ?? $"The value is not a valid {typeof(TSelf).Name}.");

        ValueObjectProblemDetails.RecordErrorCode(
            bindingContext.HttpContext,
            bindingContext.ModelName,
            validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable);

        return Task.CompletedTask;
    }
}
