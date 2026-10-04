using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.AspNetCore.Formatters;

/// <summary>
/// MVC's System.Text.Json input formatter, which also records the code a value object's refusal carries.
/// </summary>
/// <remarks>
/// <para>
/// MVC drops the exception a request body fails with. Under
/// <see cref="JsonOptions.AllowInputFormatterExceptionMessages"/>, its default, the formatter wraps the
/// <see cref="JsonException"/> in an <see cref="InputFormatterException"/>, of which <see cref="ModelStateDictionary"/>
/// keeps the message alone. Turned off, the exception survives in the model state, but every error of a body, a value
/// object's or any other, is answered "The input was not valid.".
/// </para>
/// <para>
/// This formatter is the framework's, given options of its own with exception messages turned off, so that the
/// exception reaches the model state. Once a body fails, it records the code each such exception carries, read through
/// <see cref="ValueObjectErrors.TryGetCode"/>, under the key of its error, the JSON path. Where the application keeps
/// exception messages, as its own options say, it then puts back in place of the exception the message the framework
/// would have recorded. The model state holds what it holds without this formatter, and the codes ride on the request
/// for <see cref="ValueObjectMvcExtensions.AddValueObjectProblemDetails"/>.
/// </para>
/// <para>
/// It derives from <see cref="SystemTextJsonInputFormatter"/>, so the media types and encodings it reads, what API
/// descriptions say of a body and the logs it writes are the framework's.
/// </para>
/// </remarks>
internal sealed class ValueObjectJsonInputFormatter : SystemTextJsonInputFormatter
{
    private readonly bool _exposeMessages;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectJsonInputFormatter"/> class.
    /// </summary>
    /// <param name="options">
    /// Options of its own, configured as the application's, with exception messages turned off.
    /// </param>
    /// <param name="exposeMessages">
    /// Whether the application keeps exception messages, as its
    /// <see cref="JsonOptions.AllowInputFormatterExceptionMessages"/> says.
    /// </param>
    /// <param name="logger">The framework formatter's logger.</param>
    public ValueObjectJsonInputFormatter(
        JsonOptions options,
        bool exposeMessages,
        ILogger<SystemTextJsonInputFormatter> logger)
        : base(options, logger)
    {
        _exposeMessages = exposeMessages;
    }

    /// <inheritdoc />
    public override async Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = await base.ReadAsync(context).ConfigureAwait(false);
        if (result.HasError)
        {
            RecordCodes(context);
        }

        return result;
    }

    /// <summary>
    /// Records the code of every error the body failed with, and puts back the message the framework would have kept.
    /// </summary>
    /// <param name="context">The context the body was read in.</param>
    private void RecordCodes(InputFormatterContext context)
    {
        foreach (var (key, entry) in context.ModelState)
        {
            var errors = entry.Errors;
            for (var index = 0; index < errors.Count; index++)
            {
                if (errors[index].Exception is not JsonException exception)
                {
                    continue;
                }

                if (ValueObjectErrors.TryGetCode(exception, out var code))
                {
                    ValueObjectProblemDetails.RecordErrorCode(context.HttpContext, key, code);
                }

                // What ModelStateDictionary records for the InputFormatterException the framework would have added.
                if (_exposeMessages)
                {
                    errors[index] = new ModelError(exception.Message);
                }
            }
        }
    }
}
