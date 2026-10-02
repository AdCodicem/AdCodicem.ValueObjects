using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AdCodicem.ValueObjects.AspNetCore.ModelBinding;

/// <summary>
/// Binds a value object from the raw text of a route segment, query string value, header or form field.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// <para>
/// The binder is closed over the concrete value object type, so binding costs one <c>TryParse</c> and nothing
/// else: no reflection, no <c>TypeDescriptor</c> lookup, and no exception on the rejection path. The violated
/// rule travels with the model error so that <see cref="ValueObjectProblemDetails"/> can put its stable code in
/// the response body.
/// </para>
/// <para>
/// Text that is empty or white space says no more than no text, as it does to MVC's own binder for every simple type
/// but <see cref="string"/>. An optional value object binds to <see langword="null"/>. One that cannot be
/// <see langword="null"/> is refused as MVC refuses blank text for an <see cref="int"/>, with the framework's "The
/// value is invalid" message and <see cref="ValueObjectErrorCodes.Required"/>, rather than reaching the action as the
/// default instance no rule has checked.
/// </para>
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
        if (string.IsNullOrWhiteSpace(text))
        {
            // Blank text says no more than no text, as it does to MVC's own binder for an int? or a Guid?. An optional
            // value binds to null. A value object that cannot be null would bind to its default instance, which no rule
            // has checked: it is refused as MVC refuses blank text for an int.
            if (bindingContext.ModelMetadata.IsReferenceOrNullableType)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
                return Task.CompletedTask;
            }

            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                bindingContext.ModelMetadata.ModelBindingMessageProvider.ValueMustNotBeNullAccessor(provided.ToString()));

            ValueObjectProblemDetails.RecordErrorCode(
                bindingContext.HttpContext,
                bindingContext.ModelName,
                ValueObjectErrorCodes.Required);

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
