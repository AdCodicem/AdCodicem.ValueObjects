using System.Buffers;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ErrorEventArgs = Newtonsoft.Json.Serialization.ErrorEventArgs;

namespace AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.Formatters;

/// <summary>
/// MVC's Newtonsoft.Json input formatter, which also records the code a value object's refusal carries.
/// </summary>
/// <remarks>
/// <para>
/// The framework's formatter handles every error the serializer raises, adds it to the model state and reads on. The
/// model state keeps the message of the exception by default, and drops the exception; under
/// <see cref="MvcNewtonsoftJsonOptions.AllowInputFormatterExceptionMessages"/> turned off, it keeps the exception,
/// which the response writes as "The input was not valid.". Either way, nothing reads the code of the rule that the
/// <see cref="JsonSerializationException"/> the value object converter throws carries in <see cref="Exception.Data"/>,
/// where <see cref="ValueObjectErrors.TryGetCode"/> reads it.
/// </para>
/// <para>
/// This formatter subscribes a handler of its own to the serializer's <see cref="JsonSerializer.Error"/> event for the
/// time of each read, which runs after the handler the serializer settings hold and before the framework's: it skips an
/// error either has handled, records the code of any other under the key the framework gives its model state error,
/// and leaves the error to the framework's handler, so that the model state, the logs and the result are the
/// framework's. The serializers stay pooled as the framework pools them.
/// </para>
/// <para>
/// It derives from <see cref="NewtonsoftJsonInputFormatter"/>, so the media types and encodings it reads, what API
/// descriptions say of a body, and code looking for the framework's formatter by its type, find what they expect.
/// </para>
/// </remarks>
internal sealed class ValueObjectNewtonsoftJsonInputFormatter : NewtonsoftJsonInputFormatter
{
    private readonly ConditionalWeakTable<JsonSerializer, EventHandler<ErrorEventArgs>> _handlers = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectNewtonsoftJsonInputFormatter"/> class, with what the
    /// framework builds its own with.
    /// </summary>
    /// <param name="logger">The framework formatter's logger.</param>
    /// <param name="serializerSettings">The application's serializer settings.</param>
    /// <param name="charPool">The pool of the buffers the reader reads into.</param>
    /// <param name="objectPoolProvider">The provider of the pool of serializers.</param>
    /// <param name="options">The MVC options being configured.</param>
    /// <param name="jsonOptions">The application's Newtonsoft.Json options.</param>
    public ValueObjectNewtonsoftJsonInputFormatter(
        ILogger logger,
        JsonSerializerSettings serializerSettings,
        ArrayPool<char> charPool,
        ObjectPoolProvider objectPoolProvider,
        MvcOptions options,
        MvcNewtonsoftJsonOptions jsonOptions)
        : base(logger, serializerSettings, charPool, objectPoolProvider, options, jsonOptions)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// The framework's formatter gives a formatter that derives from it
    /// <see cref="InputFormatterExceptionPolicy.AllExceptions"/>, under which MVC turns any exception a read throws
    /// into a model state error. This one keeps the policy of the formatter it replaces, so that an exception no JSON
    /// explains propagates as it does without it.
    /// </remarks>
    public override InputFormatterExceptionPolicy ExceptionPolicy => InputFormatterExceptionPolicy.MalformedInputExceptions;

    /// <summary>
    /// Gets the serializer settings a Newtonsoft.Json input formatter reads with.
    /// </summary>
    /// <remarks>
    /// The property is protected, which a derived class reads on its own instances only (CS1540); the accessor reads it
    /// without reflection, and fails with a <see cref="MissingMethodException"/> should the framework drop it.
    /// </remarks>
    /// <param name="formatter">The formatter.</param>
    /// <returns>The settings it was built with.</returns>
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_" + nameof(SerializerSettings))]
    internal static extern JsonSerializerSettings SettingsOf(NewtonsoftJsonInputFormatter formatter);

    /// <summary>
    /// Gives the key the framework's formatter gives the model state error of a refusal.
    /// </summary>
    /// <remarks>
    /// Adapted from the error handler of <c>NewtonsoftJsonInputFormatter.ReadRequestBodyAsync</c> in ASP.NET Core 10
    /// (Copyright (c) .NET Foundation and Contributors, MIT licence), so that a code and its error share a key: a change
    /// to that handler upstream has to be carried here. The framework appends the member to the path for a
    /// <see cref="JsonSerializationException"/> unless the path already ends with it, testing three cases by the lengths
    /// of the two; written as one condition, the case of a path shorter than its member, which only a missing required
    /// property raises and which carries no code, falls out of the comparisons, which are all false there.
    /// </remarks>
    /// <param name="modelName">The name of the model the body is bound to.</param>
    /// <param name="error">The error the serializer raised.</param>
    /// <returns>The key.</returns>
    internal static string KeyOf(string modelName, ErrorContext error)
    {
        var path = error.Path;
        var member = error.Member as string;
        var addMember = !string.IsNullOrEmpty(member)
            && error.Error is JsonSerializationException
            && !(string.Equals(path, member, StringComparison.Ordinal)
                || (member[0] == '['
                    ? path.EndsWith(member, StringComparison.Ordinal)
                    : path.EndsWith($".{member}", StringComparison.Ordinal)
                        || path.EndsWith($"['{member}']", StringComparison.Ordinal)
                        || path.EndsWith($"[{member}]", StringComparison.Ordinal)));

        if (addMember)
        {
            path = ModelNames.CreatePropertyModelName(path, member);
        }

        return ModelNames.CreatePropertyModelName(modelName, path);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The framework subscribes its own handler to the serializer this returns, so the one subscribed here runs first.
    /// </remarks>
    protected override JsonSerializer CreateJsonSerializer(InputFormatterContext context)
    {
        var serializer = base.CreateJsonSerializer(context);
        EventHandler<ErrorEventArgs> handler = (_, arguments) => Record(context, arguments.ErrorContext);
        serializer.Error += handler;
        _handlers.AddOrUpdate(serializer, handler);

        return serializer;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The serializer goes back to the pool without the handler of the read it served, as without the framework's.
    /// </remarks>
    protected override void ReleaseJsonSerializer(JsonSerializer serializer)
    {
        _handlers.TryGetValue(serializer, out var handler);
        serializer.Error -= handler;
        _handlers.Remove(serializer);

        base.ReleaseJsonSerializer(serializer);
    }

    /// <summary>
    /// Records the code of a refusal the framework's handler is about to add to the model state.
    /// </summary>
    /// <remarks>
    /// The framework's handler skips an error a handler before it handled, and the model state refuses an error once it
    /// holds as many as <see cref="ModelStateDictionary.MaxAllowedErrors"/> lets it, less the one it keeps to say so;
    /// Newtonsoft.Json reads on past a refusal, so a body refusing more values than that records no code past them.
    /// </remarks>
    /// <param name="context">The context of the read.</param>
    /// <param name="error">The error the serializer raised.</param>
    private static void Record(InputFormatterContext context, ErrorContext error)
    {
        if (error.Handled
            || context.ModelState.ErrorCount >= context.ModelState.MaxAllowedErrors - 1
            || !ValueObjectErrors.TryGetCode(error.Error, out var code))
        {
            return;
        }

        ValueObjectProblemDetails.RecordErrorCode(context.HttpContext, KeyOf(context.ModelName, error), code);
    }
}
