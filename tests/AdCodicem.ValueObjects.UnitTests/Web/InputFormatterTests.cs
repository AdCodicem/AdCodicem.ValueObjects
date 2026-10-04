using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.Formatters;
using AdCodicem.ValueObjects.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// The System.Text.Json input formatter <c>AddValueObjects()</c> puts in place of MVC's, read from the options an
/// application resolves, and read with beside the framework's own on the same bodies.
/// </summary>
public class InputFormatterTests
{
    /// <summary>
    /// The formatter takes the place of the framework's and reads with the very serializer options the framework's
    /// read with, the application's, which it leaves as the application left them.
    /// </summary>
    [Fact]
    public void The_framework_formatter_is_replaced_in_place_reading_with_the_application_s_own_serializer_options()
    {
        using var provider = Build(static mvc => mvc
            .AddJsonOptions(static options => options.JsonSerializerOptions.AllowTrailingCommas = true)
            .AddValueObjects());
        using var withoutPackage = Build(static _ => { });
        var framework = withoutPackage.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters;

        var formatters = provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters;
        var application = provider.GetRequiredService<IOptions<JsonOptions>>().Value;

        var index = framework.ToList().FindIndex(static formatter => formatter is SystemTextJsonInputFormatter);
        var formatter = formatters[index].Should().BeOfType<ValueObjectJsonInputFormatter>().Subject;
        formatters.OfType<SystemTextJsonInputFormatter>().Should().ContainSingle();
        formatters.Should().HaveSameCount(framework);
        formatter.SerializerOptions.Should().BeSameAs(application.JsonSerializerOptions);
        formatter.SerializerOptions.AllowTrailingCommas.Should().BeTrue();
        application.AllowInputFormatterExceptionMessages.Should().BeTrue();
        var replaced = framework[index].Should().BeOfType<SystemTextJsonInputFormatter>().Subject;
        formatter.SupportedMediaTypes.Should().Equal(replaced.SupportedMediaTypes);
        formatter.SupportedEncodings.Should().Equal(replaced.SupportedEncodings);
    }

    /// <summary>
    /// A change the application makes to its serializer options once the formatter is in place, before the first body
    /// locks them, reaches the formatter, as it reaches the framework's: there is one instance.
    /// </summary>
    [Fact]
    public async Task A_change_made_to_the_serializer_options_later_on_reaches_the_formatter()
    {
        using var provider = Build(static mvc => mvc.AddValueObjects());
        var formatter = provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .OfType<ValueObjectJsonInputFormatter>().Single();

        provider.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions.AllowTrailingCommas = true;
        var (result, _, _) = await ReadAsync(formatter, """{"reference":"ABC-1",}""");

        result.HasError.Should().BeFalse();
        result.Model.Should().Be(new ProbeOrder(Ordering.OrderReference.Create("ABC-1")));
    }

    /// <summary>
    /// The media types and encodings an application gave the framework formatter, in the configuration of MVC, are the
    /// ones its replacement reads.
    /// </summary>
    [Fact]
    public void The_media_types_and_encodings_the_application_gave_the_framework_formatter_are_kept()
    {
        using var provider = Build(static mvc => mvc
            .AddMvcOptions(static options =>
            {
                var framework = options.InputFormatters.OfType<SystemTextJsonInputFormatter>().Single();
                framework.SupportedMediaTypes.Add("application/csp-report");
                framework.SupportedEncodings.Remove(framework.SupportedEncodings.Single(static encoding => encoding.CodePage == Encoding.Unicode.CodePage));
            })
            .AddValueObjects());

        var formatter = provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .OfType<ValueObjectJsonInputFormatter>().Single();

        formatter.SupportedMediaTypes.Should().Contain("application/csp-report");
        formatter.SupportedEncodings.Should().ContainSingle().Which.CodePage.Should().Be(Encoding.UTF8.CodePage);
    }

    /// <summary>
    /// Only the framework's formatter is replaced. One the application built with options of its own, though of the
    /// exact type, one it derived, though reading with the application's options, and a formatter of another kind are
    /// left where and as they are.
    /// </summary>
    [Fact]
    public void A_formatter_the_application_built_or_derived_itself_is_left_as_it_is()
    {
        var own = new SystemTextJsonInputFormatter(new JsonOptions(), NullLogger());
        DerivedFormatter? derived = null;
        using var provider = Build(mvc =>
        {
            mvc.AddXmlSerializerFormatters();
            mvc.Services.AddOptions<MvcOptions>().Configure<IOptions<JsonOptions>>((options, json) =>
            {
                derived = new DerivedFormatter(json.Value, NullLogger());
                options.InputFormatters.Insert(0, own);
                options.InputFormatters.Insert(1, derived);
            });
            mvc.AddValueObjects();
        });

        var formatters = provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters;

        formatters[0].Should().BeSameAs(own);
        formatters[1].Should().BeSameAs(derived);
        derived!.SerializerOptions.Should().BeSameAs(provider.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions);
        formatters.OfType<XmlSerializerInputFormatter>().Should().ContainSingle();
        formatters.OfType<ValueObjectJsonInputFormatter>().Should().ContainSingle()
            .Which.SerializerOptions.Should().BeSameAs(provider.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions);
    }

    [Fact]
    public void Adding_value_objects_twice_replaces_the_formatter_once()
    {
        using var provider = Build(static mvc => mvc.AddValueObjects().AddValueObjects());

        provider.GetServices<IPostConfigureOptions<MvcOptions>>().OfType<ValueObjectInputFormatterSetup>().Should().ContainSingle();
        provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .OfType<ValueObjectJsonInputFormatter>().Should().ContainSingle();
    }

    /// <summary>
    /// An application that took the framework formatter out, as Newtonsoft.Json does, gets none back: there is no
    /// System.Text.Json formatter to replace.
    /// </summary>
    [Fact]
    public void An_application_without_the_framework_formatter_gets_none()
    {
        using var provider = Build(static mvc => mvc
            .AddMvcOptions(static options => options.InputFormatters.RemoveType<SystemTextJsonInputFormatter>())
            .AddValueObjects());

        provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .OfType<SystemTextJsonInputFormatter>().Should().BeEmpty();
    }

    /// <summary>
    /// A post-configuration registered after <c>AddValueObjects()</c> finds the replacement, which
    /// <c>RemoveType&lt;SystemTextJsonInputFormatter&gt;()</c>, matching the exact type, leaves in place; removing every
    /// formatter that is a <see cref="SystemTextJsonInputFormatter"/>, as the documentation says, takes it out.
    /// </summary>
    [Fact]
    public void A_removal_registered_after_value_objects_takes_every_formatter_that_is_the_framework_s()
    {
        using var exact = Build(static mvc =>
        {
            mvc.AddValueObjects();
            mvc.Services.PostConfigure<MvcOptions>(static options => options.InputFormatters.RemoveType<SystemTextJsonInputFormatter>());
        });
        using var derived = Build(static mvc =>
        {
            mvc.AddValueObjects();
            mvc.Services.PostConfigure<MvcOptions>(static options =>
            {
                foreach (var formatter in options.InputFormatters.OfType<SystemTextJsonInputFormatter>().ToList())
                {
                    options.InputFormatters.Remove(formatter);
                }
            });
        });

        exact.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .OfType<ValueObjectJsonInputFormatter>().Should().ContainSingle();
        derived.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .OfType<SystemTextJsonInputFormatter>().Should().BeEmpty();
    }

    /// <summary>
    /// A body read by the replacement and by the framework's formatter, built with the same options, gives the same
    /// result, the same model state and the same logs, whatever the application says of exception messages; the
    /// replacement also records the code of a value object's refusal under the JSON path.
    /// </summary>
    /// <param name="body">The body, written in the request's charset.</param>
    /// <param name="charset">The charset of the request.</param>
    /// <param name="model">The type the body is read as: an order, or a shape whose converter throws.</param>
    /// <param name="emptyIsDefault">Whether the parameter takes an empty body as its default.</param>
    /// <param name="refused">Whether the framework refuses the body, so that the row compares what it means to.</param>
    /// <param name="code">The code the replacement records under the JSON path, if any.</param>
    [Theory]
    [InlineData("""{"reference":"ABC-1"}""", "utf-8", "order", false, false, null)]
    [InlineData("""{"reference":"no"}""", "utf-8", "order", false, true, ValueObjectErrorCodes.TooShort)]
    [InlineData("""{"reference":42}""", "utf-8", "order", false, true, ValueObjectErrorCodes.NotParsable)]
    [InlineData("""{"reference":null}""", "utf-8", "order", false, true, ValueObjectErrorCodes.Required)]
    [InlineData("""{"reference":""", "utf-8", "order", false, true, null)]
    [InlineData("null", "utf-8", "order", false, false, null)]
    [InlineData("null", "utf-8", "order", true, false, null)]
    [InlineData("""{"reference":"no"}""", "utf-16", "order", false, true, ValueObjectErrorCodes.TooShort)]
    [InlineData("""{"reference":"ABC-1"}""", "utf-16", "order", false, false, null)]
    [InlineData("""{"reference":"ABC-1"}""", "iso-8859-2", "order", false, true, null)]
    [InlineData("\"format\"", "utf-8", "shape", false, true, null)]
    [InlineData("\"overflow\"", "utf-8", "shape", false, true, null)]
    [InlineData("\"square\"", "utf-8", "shape", false, false, null)]
    public async Task A_body_reads_as_the_framework_reads_it_and_a_refusal_records_its_code(
        string body,
        string charset,
        string model,
        bool emptyIsDefault,
        bool refused,
        string? code)
    {
        var type = model == "order" ? typeof(ProbeOrder) : typeof(Shape);
        foreach (var allowMessages in new[] { true, false })
        {
            var options = new JsonOptions { AllowInputFormatterExceptionMessages = allowMessages };
            options.JsonSerializerOptions.AddValueObjects();
            var frameworkLogger = new RecordingLogger();
            var replacementLogger = new RecordingLogger();
            var framework = new SystemTextJsonInputFormatter(options, frameworkLogger);
            var replacement = new ValueObjectJsonInputFormatter(options, replacementLogger);

            var (expected, expectedState, expectedHttp) = await ReadAsync(framework, body, charset, type, emptyIsDefault);
            var (actual, actualState, actualHttp) = await ReadAsync(replacement, body, charset, type, emptyIsDefault);

            expected.HasError.Should().Be(refused);
            expectedState.IsValid.Should().Be(!refused);
            actual.HasError.Should().Be(expected.HasError);
            actual.IsModelSet.Should().Be(expected.IsModelSet);
            actual.Model.Should().Be(expected.Model);
            Describe(actualState).Should().Equal(Describe(expectedState), "the model state of {0} in {1}", body, charset);
            replacementLogger.Entries.Should().Equal(frameworkLogger.Entries);
            ValueObjectProblemDetails.GetErrorCodes(expectedHttp).Should().BeEmpty();
            ValueObjectProblemDetails.GetErrorCodes(actualHttp).Should().Equal(code is null
                ? []
                : new Dictionary<string, string> { ["$.reference"] = code });
        }
    }

    /// <summary>
    /// Under the switch the framework reads a UTF-8 body from the request's stream rather than its pipe, so a converter
    /// that cannot read a sequence keeps working; the replacement reads the same way. The pipe and the stream here hold
    /// different bodies, so the one read shows.
    /// </summary>
    /// <param name="streamBased">Whether the switch is on.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_UTF_8_body_is_read_from_the_stream_under_the_framework_s_switch_and_from_the_pipe_otherwise(bool streamBased)
    {
        var options = new JsonOptions();
        SystemTextJsonInputFormatter framework;
        ValueObjectJsonInputFormatter replacement;
        AppContext.SetSwitch(ValueObjectJsonInputFormatter.StreamBasedParsingSwitch, streamBased);
        try
        {
            framework = new SystemTextJsonInputFormatter(options, NullLogger());
            replacement = new ValueObjectJsonInputFormatter(options, NullLogger());
        }
        finally
        {
            AppContext.SetSwitch(ValueObjectJsonInputFormatter.StreamBasedParsingSwitch, false);
        }

        foreach (var formatter in new SystemTextJsonInputFormatter[] { framework, replacement })
        {
            var http = Request("""{"reference":"STREAM"}""", "utf-8");
            http.Features.Set<IRequestBodyPipeFeature>(new PipeOf("""{"reference":"PIPE"}"""));

            var result = await formatter.ReadAsync(Context(http, typeof(ProbeOrder), new ModelStateDictionary(), false));

            result.Model.Should().Be(new ProbeOrder(Ordering.OrderReference.Create(streamBased ? "STREAM" : "PIPE")), formatter.GetType().Name);
        }
    }

    /// <summary>
    /// A <see cref="JsonException"/> the serializer did not throw, here one the request's pipe fails with, carries no
    /// path: the framework keys its error with the empty key, and the replacement records the code there too.
    /// </summary>
    [Fact]
    public async Task A_JSON_exception_without_a_path_is_keyed_as_the_framework_keys_it()
    {
        var options = new JsonOptions { AllowInputFormatterExceptionMessages = false };
        var framework = new SystemTextJsonInputFormatter(options, NullLogger());
        var replacement = new ValueObjectJsonInputFormatter(options, NullLogger());
        var states = new List<ModelStateDictionary>();
        var contexts = new List<HttpContext>();

        foreach (var formatter in new SystemTextJsonInputFormatter[] { framework, replacement })
        {
            var http = Request("{}", "utf-8");
            http.Features.Set<IRequestBodyPipeFeature>(new RefusingPipe(
                new ValueObjectJsonException("The pipe refused the body.", typeof(Ordering.OrderReference), ValueObjectErrorCodes.InvalidFormat)));
            var modelState = new ModelStateDictionary();

            var result = await formatter.ReadAsync(Context(http, typeof(ProbeOrder), modelState, false));

            result.HasError.Should().BeTrue();
            states.Add(modelState);
            contexts.Add(http);
        }

        Describe(states[1]).Should().Equal(Describe(states[0])).And.ContainSingle().Which.Should().StartWith(" | ");
        ValueObjectProblemDetails.GetErrorCodes(contexts[1]).Should().Equal(new Dictionary<string, string>
        {
            [string.Empty] = ValueObjectErrorCodes.InvalidFormat,
        });
    }

    [Fact]
    public async Task Reading_no_context_is_refused()
    {
        using var provider = Build(static mvc => mvc.AddValueObjects());
        var formatter = provider.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters
            .OfType<ValueObjectJsonInputFormatter>().Single();

        await FluentActions.Awaiting(() => formatter.ReadAsync(null!)).Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => formatter.ReadRequestBodyAsync(null!)).Should().ThrowAsync<ArgumentNullException>();
        FluentActions.Invoking(() => new ValueObjectInputFormatterSetup(
                provider.GetRequiredService<IOptions<JsonOptions>>(),
                provider.GetRequiredService<ILoggerFactory>())
            .PostConfigure(null, null!))
            .Should().Throw<ArgumentNullException>();
    }

    private static ServiceProvider Build(Action<IMvcBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services.AddControllers());

        return services.BuildServiceProvider();
    }

    private static Microsoft.Extensions.Logging.Abstractions.NullLogger<SystemTextJsonInputFormatter> NullLogger()
        => Microsoft.Extensions.Logging.Abstractions.NullLogger<SystemTextJsonInputFormatter>.Instance;

    private static Task<(InputFormatterResult Result, ModelStateDictionary ModelState, HttpContext Http)> ReadAsync(
        IInputFormatter formatter,
        string body)
        => ReadAsync(formatter, body, "utf-8", typeof(ProbeOrder), false);

    private static async Task<(InputFormatterResult Result, ModelStateDictionary ModelState, HttpContext Http)> ReadAsync(
        IInputFormatter formatter,
        string body,
        string charset,
        Type type,
        bool emptyIsDefault)
    {
        var http = Request(body, charset);
        var modelState = new ModelStateDictionary();

        var result = await formatter.ReadAsync(Context(http, type, modelState, emptyIsDefault));

        return (result, modelState, http);
    }

    private static DefaultHttpContext Request(string body, string charset)
    {
        // A charset the formatter does not read is refused before the body is, whatever its bytes.
        var bytes = (charset == "utf-16" ? Encoding.Unicode : Encoding.UTF8).GetBytes(body);

        return new DefaultHttpContext
        {
            Request =
            {
                Body = new MemoryStream(bytes),
                ContentLength = bytes.Length,
                ContentType = $"application/json; charset={charset}",
            },
        };
    }

    private static InputFormatterContext Context(HttpContext http, Type type, ModelStateDictionary modelState, bool emptyIsDefault)
        => new(
            http,
            string.Empty,
            modelState,
            new EmptyModelMetadataProvider().GetMetadataForType(type),
            static (stream, encoding) => new StreamReader(stream, encoding),
            emptyIsDefault);

    /// <summary>Every error of a model state, as text a client could tell apart.</summary>
    private static List<string> Describe(ModelStateDictionary modelState)
        => [.. modelState
            .OrderBy(static entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(static entry => entry.Value!.Errors.Select(error =>
                $"{entry.Key} | {error.ErrorMessage} | {error.Exception?.GetType().FullName} | {error.Exception?.Message}"))];

    /// <summary>A shape, read by a converter that throws what System.Text.Json never does.</summary>
    /// <param name="Name">The name of the shape.</param>
    [JsonConverter(typeof(ShapeConverter))]
    public sealed record Shape(string Name);

    /// <summary>Reads a shape, refusing two names with a <see cref="FormatException"/> and an <see cref="OverflowException"/>.</summary>
    private sealed class ShapeConverter : JsonConverter<Shape>
    {
        public override Shape Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetString() switch
            {
                "format" => throw new FormatException("A shape has no such format."),
                "overflow" => throw new OverflowException("A shape cannot be that large."),
                var name => new Shape(name!),
            };

        public override void Write(Utf8JsonWriter writer, Shape value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Name);
    }

    private sealed class DerivedFormatter(JsonOptions options, ILogger<SystemTextJsonInputFormatter> logger)
        : SystemTextJsonInputFormatter(options, logger);

    /// <summary>A request pipe holding a body of its own.</summary>
    /// <param name="body">The body the pipe holds.</param>
    private sealed class PipeOf(string body) : IRequestBodyPipeFeature
    {
        public PipeReader Reader { get; } = PipeReader.Create(new MemoryStream(Encoding.UTF8.GetBytes(body)));
    }

    /// <summary>A request pipe that fails every read with an exception.</summary>
    /// <param name="exception">The exception every read fails with.</param>
    private sealed class RefusingPipe(Exception exception) : PipeReader, IRequestBodyPipeFeature
    {
        public PipeReader Reader => this;

        public override void AdvanceTo(SequencePosition consumed)
        {
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
        }

        public override void CancelPendingRead()
        {
        }

        public override void Complete(Exception? exception = null)
        {
        }

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromException<ReadResult>(exception);

        public override bool TryRead(out ReadResult result) => throw exception;
    }

    /// <summary>Records what a formatter logs, as event and message.</summary>
    private sealed class RecordingLogger : ILogger<SystemTextJsonInputFormatter>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add($"{logLevel} {eventId.Id} {eventId.Name}: {formatter(state, exception)}");
    }
}
