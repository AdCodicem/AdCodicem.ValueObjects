using System.Text;
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
/// keeps the message alone; turned off, every error of a body is answered "The input was not valid.". The method
/// that reads the body and catches the exception is sealed, and reads with the serializer options of the
/// <see cref="JsonOptions"/> it was built with, which no other instance can share, since
/// <see cref="JsonOptions.JsonSerializerOptions"/> cannot be replaced.
/// </para>
/// <para>
/// This formatter is therefore built with the application's own <see cref="JsonOptions"/>, the instance the
/// framework's formatter holds, and reads the body itself, as the framework's formatter of ASP.NET Core 10 reads it:
/// with the same <see cref="SystemTextJsonInputFormatter.SerializerOptions"/>, through the request's pipe, or its
/// stream under the <c>Microsoft.AspNetCore.UseStreamBasedJsonParsing</c> switch or another encoding than UTF-8.
/// Once the serializer refuses the body, it records the code the exception carries, read through
/// <see cref="ValueObjectErrors.TryGetCode"/>, under the key of its error, the JSON path, and adds the error the
/// framework adds, with or without the message as <see cref="JsonOptions.AllowInputFormatterExceptionMessages"/> says
/// at that moment. The model state holds what it holds without this formatter, the logs are the framework's, and the
/// codes ride on the request for <see cref="ValueObjectMvcExtensions.AddValueObjectProblemDetails"/>.
/// </para>
/// <para>
/// It derives from <see cref="SystemTextJsonInputFormatter"/>, so the media types and encodings it reads, what API
/// descriptions say of a body, and code looking for the framework's formatter by its type, find what they expect.
/// </para>
/// </remarks>
internal sealed partial class ValueObjectJsonInputFormatter : SystemTextJsonInputFormatter
{
    /// <summary>
    /// The switch under which the framework's formatter reads a UTF-8 body from the request's stream.
    /// </summary>
    internal const string StreamBasedParsingSwitch = "Microsoft.AspNetCore.UseStreamBasedJsonParsing";

    private readonly JsonOptions _options;
    private readonly ILogger<SystemTextJsonInputFormatter> _logger;
    private readonly bool _readsStream;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectJsonInputFormatter"/> class.
    /// </summary>
    /// <param name="options">The application's options, which the framework's formatter reads with.</param>
    /// <param name="logger">The framework formatter's logger.</param>
    public ValueObjectJsonInputFormatter(JsonOptions options, ILogger<SystemTextJsonInputFormatter> logger)
        : base(options, logger)
    {
        _options = options;
        _logger = logger;
        _readsStream = AppContext.TryGetSwitch(StreamBasedParsingSwitch, out var enabled) && enabled;
    }

    /// <inheritdoc />
    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var encoding = SelectCharacterEncoding(context);
        if (encoding is null)
        {
            // The framework's answer to a charset it does not read: an unsupported media type.
            return await base.ReadRequestBodyAsync(context).ConfigureAwait(false);
        }

        // What follows is adapted from the sealed SystemTextJsonInputFormatter.ReadRequestBodyAsync(context, encoding)
        // of ASP.NET Core 10 (Copyright (c) .NET Foundation and Contributors, MIT licence), so that it keeps the
        // exception: a change to that method upstream has to be carried here.
        var request = context.HttpContext.Request;
        object? model;
        Stream? transcoded = null;
        try
        {
            if (encoding.CodePage != Encoding.UTF8.CodePage)
            {
                transcoded = Encoding.CreateTranscodingStream(request.Body, encoding, Encoding.UTF8, leaveOpen: true);
                model = await JsonSerializer.DeserializeAsync(transcoded, context.ModelType, SerializerOptions).ConfigureAwait(false);
            }
            else if (_readsStream)
            {
                model = await JsonSerializer.DeserializeAsync(request.Body, context.ModelType, SerializerOptions).ConfigureAwait(false);
            }
            else
            {
                model = await JsonSerializer.DeserializeAsync(request.BodyReader, context.ModelType, SerializerOptions).ConfigureAwait(false);
            }
        }
        catch (JsonException exception)
        {
            var path = exception.Path ?? string.Empty;
            if (ValueObjectErrors.TryGetCode(exception, out var code))
            {
                ValueObjectProblemDetails.RecordErrorCode(context.HttpContext, path, code);
            }

            // An InputFormatterException tells the model state its message is safe to answer with.
            context.ModelState.TryAddModelError(
                path,
                _options.AllowInputFormatterExceptionMessages ? new InputFormatterException(exception.Message, exception) : exception,
                context.Metadata);
            LogRefusal(_logger, exception.Message);

            return InputFormatterResult.Failure();
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            // System.Text.Json never throws either; a converter of the application may, and the framework answers it so.
            context.ModelState.TryAddModelError(string.Empty, exception, context.Metadata);
            LogRefusal(_logger, exception.Message);

            return InputFormatterResult.Failure();
        }
        finally
        {
            if (transcoded is not null)
            {
                await transcoded.DisposeAsync().ConfigureAwait(false);
            }
        }

        if (model is null && !context.TreatEmptyInputAsDefaultValue)
        {
            // A body such as null or white space holds no value, which the body model binder reports.
            return InputFormatterResult.NoValue();
        }

        LogSuccess(_logger, context.ModelType.FullName);

        return InputFormatterResult.Success(model);
    }

    [LoggerMessage(1, LogLevel.Debug, "JSON input formatter threw an exception: {Message}", EventName = "SystemTextJsonInputException")]
    private static partial void LogRefusal(ILogger logger, string message);

    [LoggerMessage(2, LogLevel.Debug, "JSON input formatter succeeded, deserializing to type '{TypeName}'", EventName = "SystemTextJsonInputSuccess")]
    private static partial void LogSuccess(ILogger logger, string? typeName);
}
