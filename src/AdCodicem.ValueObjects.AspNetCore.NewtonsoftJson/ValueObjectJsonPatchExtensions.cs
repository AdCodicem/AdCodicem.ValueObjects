using AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.Patching;
using AdCodicem.ValueObjects.NewtonsoftJson;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;

namespace AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson;

/// <summary>
/// Applies JSON Patch documents whose values each value object reads through its rules.
/// </summary>
/// <remarks>
/// <para>
/// MVC reads a <see cref="JsonPatchDocument{TModel}"/> body through its JSON Patch formatter, which holds each value as
/// JSON until the document is applied. JSON Patch's own <c>ApplyTo</c> then converts it by writing it to text and
/// reading it back under settings of its own, and reports any refusal in its own words, which quote the value and name
/// no rule; MVC's <c>ApplyTo(objectToApplyTo, ModelState)</c> puts every error under the type name of the object. These
/// methods apply the same document through the value objects' rules, with their messages and codes.
/// </para>
/// <para>
/// <c>add</c>, <c>replace</c> and <c>test</c> read a value into a value object member, list element or dictionary value
/// as the document holds it, a string, a number, a boolean, a <c>null</c> or a <c>JToken</c>, never written to text
/// and read again: through <see cref="ValueObjectConverter"/>, under the settings
/// <see cref="ValueObjectJsonSerializerSettingsExtensions.AddValueObjects"/> gives it and the document's contract
/// resolver. Its digits, its offset and its text are the client's when MVC read the document under those settings,
/// which it does once <c>AddValueObjectsNewtonsoftJson()</c> is in place. A refused value is reported with the
/// converter's message, which names the type and the rule and never the value, and the code of the rule; a <c>null</c>
/// read into a value object that cannot be <see langword="null"/> is <see cref="ValueObjectErrorCodes.Required"/>. An
/// instance of the value object, in a document built in code or taken from the object by <c>copy</c> or <c>move</c>,
/// is kept as it is, but for a default instance its type refuses, which no writer writes either.
/// </para>
/// <para>
/// A dictionary keyed by a value object reads each key through the value object's rules, from its text in the invariant
/// culture, as a route value is read: a refused key reads "The key is not a valid CountryCode: …", with its code.
/// </para>
/// <para>
/// A <c>test</c> of a value object compares through the value object's equality, so <c>12.5</c> matches a stored
/// <c>12.50</c>, an instant matches the same instant at another offset, and a closed set compared ignoring case matches
/// another case. One that does not match names neither value, and carries no code.
/// </para>
/// <para>
/// A value object member that cannot be <see langword="null"/> cannot be removed, which JSON Patch would do by setting
/// it to its default instance: <c>remove</c> is refused with <see cref="ValueObjectErrorCodes.Required"/>, and so is a
/// <c>move</c> from it, which then adds nothing at its target. Declare the member <c>T?</c> to remove it. A <c>move</c>
/// to the location it comes from changes nothing.
/// </para>
/// <para>
/// A member of another type, a value object inside an object value, which JSON Patch converts as a whole, and every
/// error no value explains are JSON Patch's, with its messages, which may quote the value. An operation that names no
/// <c>path</c>, or a <c>copy</c> or a <c>move</c> that names no <c>from</c>, is an error, where JSON Patch throws, and
/// so is <c>-</c>, the end of a list, where it holds no element to read, remove, replace or test.
/// </para>
/// <para>
/// Every operation is applied in turn, after one that failed too, as MVC's <c>ApplyTo</c> does: on
/// <see langword="false"/> the object is partly patched, and is not to be saved. JSON Patch stops at an operation it
/// cannot read, an unknown <c>op</c> or a path with a bad escape, which is reported. Nothing is thrown for what the client
/// sent; the messages are English, as those of a body Newtonsoft.Json reads.
/// </para>
/// </remarks>
public static class ValueObjectJsonPatchExtensions
{
    /// <summary>
    /// Applies the operations and records each error in the model state under the operation's path, with the code of
    /// the rule a value broke for the problem details.
    /// </summary>
    /// <typeparam name="TModel">The type the document patches.</typeparam>
    /// <param name="patchDocument">The document, as MVC read it.</param>
    /// <param name="objectToApplyTo">The object it patches.</param>
    /// <param name="modelState">Where each error is recorded.</param>
    /// <param name="httpContext">
    /// The request the codes are recorded on, for <c>AddValueObjectProblemDetails()</c> or
    /// <see cref="ValueObjectProblemDetails.GetErrorCodes"/>.
    /// </param>
    /// <param name="prefix">Put before the path of each error's key, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when every operation applied.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="patchDocument"/>, <paramref name="objectToApplyTo"/>, <paramref name="modelState"/> or
    /// <paramref name="httpContext"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Each error is keyed by the operation's <c>path</c>, as the client sent it, <c>/quantity</c> or <c>/ibans/-</c>,
    /// after <paramref name="prefix"/>, joined with no separator since the path starts with <c>/</c>: <c>patch</c> gives
    /// <c>patch/quantity</c>. A <c>copy</c> or a <c>move</c> refused at its source, a <c>from</c> JSON Patch cannot read
    /// included, is keyed by its <c>from</c>, and an operation that names no <c>path</c> by the prefix alone. A key
    /// refused more than once keeps every message and its first code, as
    /// <see cref="ValueObjectProblemDetails.RecordErrorCode"/> keeps it, and an error past
    /// <see cref="MvcOptions.MaxModelValidationErrors"/> records no code, so that every code has its message.
    /// </para>
    /// <para>
    /// The codes reach the response <see cref="ApiBehaviorOptions.InvalidModelStateResponseFactory"/> builds, to which
    /// <c>AddValueObjectProblemDetails()</c> adds them, when the action answers with it: the automatic 400 runs before
    /// the action, and never sees the errors recorded here. An action answering with <c>ValidationProblem()</c> goes
    /// through MVC's <c>ProblemDetailsFactory</c> instead, and passes them itself, in its <c>extensions</c>:
    /// <see cref="ValueObjectProblemDetails.GetErrorCodes"/> of the request, under
    /// <see cref="ValueObjectProblemDetails.ExtensionName"/>.
    /// </para>
    /// </remarks>
    public static bool TryApplyTo<TModel>(
        this JsonPatchDocument<TModel> patchDocument,
        TModel objectToApplyTo,
        ModelStateDictionary modelState,
        HttpContext httpContext,
        string? prefix = null)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(patchDocument);
        ArgumentNullException.ThrowIfNull(objectToApplyTo);
        ArgumentNullException.ThrowIfNull(modelState);
        ArgumentNullException.ThrowIfNull(httpContext);

        return ValueObjectJsonPatch.Apply(patchDocument, objectToApplyTo, (error, location, code) =>
        {
            var key = prefix + location;
            if (modelState.TryAddModelError(key, error.ErrorMessage) && code is not null)
            {
                ValueObjectProblemDetails.RecordErrorCode(httpContext, key, code);
            }
        });
    }

    /// <summary>
    /// Applies the operations and hands each error the code of the rule a value broke, or <see langword="null"/>.
    /// </summary>
    /// <typeparam name="TModel">The type the document patches.</typeparam>
    /// <param name="patchDocument">The document.</param>
    /// <param name="objectToApplyTo">The object it patches.</param>
    /// <param name="logError">
    /// Called with each error, and the code of the rule a value broke, or <see langword="null"/> for an error no rule
    /// explains.
    /// </param>
    /// <returns><see langword="true"/> when every operation applied.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="patchDocument"/>, <paramref name="objectToApplyTo"/> or <paramref name="logError"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// The error's <see cref="JsonPatchError.Operation"/> is the operation that failed. A document read outside MVC is
    /// best read under <c>new JsonSerializerSettings().AddValueObjects()</c>: under Newtonsoft.Json's defaults, a string
    /// that looks like a date and the digits of a <see cref="decimal"/> are lost when the document is read, before any
    /// value object sees them, and the first is refused, with
    /// <see cref="ValueObjectErrorCodes.NotParsable"/>, where it no longer says what the text said
    /// (<see cref="DateParseHandling"/>).
    /// </remarks>
    public static bool TryApplyTo<TModel>(
        this JsonPatchDocument<TModel> patchDocument,
        TModel objectToApplyTo,
        Action<JsonPatchError, string?> logError)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(patchDocument);
        ArgumentNullException.ThrowIfNull(objectToApplyTo);
        ArgumentNullException.ThrowIfNull(logError);

        return ValueObjectJsonPatch.Apply(patchDocument, objectToApplyTo, (error, _, code) => logError(error, code));
    }
}
