using System.Globalization;
using AdCodicem.ValueObjects.Shared;
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
/// <para>
/// A refusal carries the English message of the rule, unless the application configured its messages through
/// <c>AddValueObjects(messages =&gt; …)</c>: the message the resource holds under the key of the rule's code then takes
/// its place, quoting the display name of the member as <c>{0}</c> and the arguments the type's rules supply from
/// <c>{1}</c>. Blank text keeps the framework's message, which the application localises through MVC's own message
/// provider.
/// </para>
/// </remarks>
public sealed class ValueObjectModelBinder<TSelf, TValue> : IModelBinder
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    private readonly ValueObjectMessageOptions? _messages;

    /// <summary>
    /// Initializes a binder that answers a refusal with the English message of the rule.
    /// </summary>
    public ValueObjectModelBinder()
        : this(null)
    {
    }

    /// <summary>
    /// Initializes a binder that answers a refusal with the messages of its provider.
    /// </summary>
    /// <param name="messages">The messages, or <see langword="null"/> for the English ones of the rules.</param>
    internal ValueObjectModelBinder(ValueObjectMessageOptions? messages) => _messages = messages;

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

        var code = validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable;
        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName,
            ValueObjectLocalizedMessages.Find<TSelf, TValue>(
                _messages,
                bindingContext.HttpContext.RequestServices,
                code,
                bindingContext.ModelMetadata.GetDisplayName())
            ?? validation.ErrorMessage
            ?? $"The value is not a valid {typeof(TSelf).Name}.");

        ValueObjectProblemDetails.RecordErrorCode(bindingContext.HttpContext, bindingContext.ModelName, code);

        return Task.CompletedTask;
    }
}
