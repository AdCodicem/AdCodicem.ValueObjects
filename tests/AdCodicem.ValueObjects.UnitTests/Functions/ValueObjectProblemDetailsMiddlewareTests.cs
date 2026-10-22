using System.Collections.Immutable;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AzureFunctions;
using AdCodicem.ValueObjects.AzureFunctions.AspNetCore;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.TestData;
using AdCodicem.ValueObjects.UnitTests.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static AdCodicem.ValueObjects.UnitTests.Functions.FunctionInvocation;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace AdCodicem.ValueObjects.UnitTests.Functions;

/// <summary>
/// The middleware that answers an HTTP trigger of the ASP.NET Core integration whose value objects the worker could not
/// bind: the worker's failure holds its text alone, so each value-object parameter the converter reads is parsed again
/// from the binding data, and the refusals are answered with the validation problem a minimal API writes.
/// </summary>
public sealed class ValueObjectProblemDetailsMiddlewareTests
{
    private const string Guid = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

    [Fact]
    public async Task A_function_that_is_no_HTTP_trigger_is_left_alone()
    {
        var middleware = FunctionsHttp.Middleware();
        var invocation = new FunctionInvocation("Queue", [Parameter<Quantity>("qty")], new Dictionary<string, object?> { ["qty"] = "5000" }, FunctionsHttp.Request(), trigger: "queueTrigger");
        var ran = 0;

        await middleware.Invoke(invocation, _ =>
        {
            ran++;
            return Task.CompletedTask;
        });
        var unbound = FunctionsHttp.Unbound();
        var refuse = () => middleware.RefuseAsync(invocation, unbound);

        ran.Should().Be(1);
        (await refuse.Should().ThrowAsync<FunctionInputConverterException>()).Which.Should().BeSameAs(unbound);
        invocation.Result.Should().BeNull();
        invocation.Logs.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task An_HTTP_trigger_that_binds_runs_untouched()
    {
        var middleware = FunctionsHttp.Middleware();
        var invocation = new FunctionInvocation("Line", [Parameter<Quantity>("qty")], new Dictionary<string, object?> { ["qty"] = "7" }, FunctionsHttp.Request());
        var ran = 0;

        await middleware.Invoke(invocation, _ =>
        {
            ran++;
            return Task.CompletedTask;
        });
        await middleware.Invoke(invocation, _ =>
        {
            ran++;
            return Task.CompletedTask;
        });

        ran.Should().Be(2);
        invocation.Result.Should().BeNull();
    }

    /// <summary>
    /// The answer reaches the client only from inside the integration's proxying middleware, which sets the request on the
    /// invocation before it calls the middleware inside it. Registered before the integration, or on the built-in HTTP
    /// model, the middleware finds no request when it starts: the failure keeps the worker's 500, and a warning says why,
    /// once.
    /// </summary>
    [Fact]
    public async Task Without_the_request_at_start_a_refusal_keeps_the_workers_failure_and_a_warning_says_why_once()
    {
        var middleware = FunctionsHttp.Middleware();
        var first = new FunctionInvocation("Line", [Parameter<Quantity>("qty")], new Dictionary<string, object?> { ["qty"] = "5000" });
        var second = new FunctionInvocation("Line", [Parameter<Quantity>("qty")], new Dictionary<string, object?> { ["qty"] = "5000" });
        var unbound = FunctionsHttp.Unbound();

        (await FluentActions.Awaiting(() => middleware.RefuseAsync(first, unbound)).Should().ThrowAsync<FunctionInputConverterException>())
            .Which.Should().BeSameAs(unbound);
        await FluentActions.Awaiting(() => middleware.RefuseAsync(second)).Should().ThrowAsync<FunctionInputConverterException>();
        var body = new InvalidOperationException("The value is not a valid Quantity.");
        (await FluentActions.Awaiting(() => middleware.RefuseAsync(second, body)).Should().ThrowAsync<InvalidOperationException>())
            .Which.Should().BeSameAs(body);

        var warning = first.Logs.Entries.Should().ContainSingle().Which;
        warning.Level.Should().Be(LogLevel.Warning);
        warning.EventName.Should().Be("ValueObjectProblemDetailsOutsideIntegration");
        warning.Category.Should().Be("AdCodicem.ValueObjects.AzureFunctions.AspNetCore.ValueObjectProblemDetailsMiddleware");
        warning.Message.Should().Contain("'Line'").And.Contain("ConfigureFunctionsWebApplication()").And.NotContain("5000");
        second.Logs.Entries.Should().BeEmpty("the warning is logged once");
        first.Result.Should().BeNull();
        second.Result.Should().BeNull();
    }

    /// <summary>
    /// Registered before the integration, the middleware finds the request only once the invocation has failed, set inside
    /// it by the integration's proxying middleware: a body MVC refused keeps the worker's failure there too, and the
    /// warning says why, once, while a body no recorded code explains is no value object's and warns of nothing.
    /// </summary>
    [Fact]
    public async Task Without_the_request_at_start_a_refused_body_keeps_the_workers_failure_and_a_warning_says_why_once()
    {
        var middleware = FunctionsHttp.Middleware();
        var order = Parameter<Quantity>("order", ImmutableDictionary<string, object>.Empty.Add("converterType", new FromBodyAttribute().ConverterType.AssemblyQualifiedName!));
        var uncoded = new FunctionInvocation("Body", [order]);
        var first = new FunctionInvocation("Body", [order]);
        var second = new FunctionInvocation("Body", [order]);
        var other = new InvalidOperationException("Something else failed.");
        var body = new InvalidOperationException("The value is not a valid Quantity: The value must be less than or equal to 1000.");

        (await FluentActions.Awaiting(() => RefuseInsideAsync(middleware, uncoded, other, codes: false))
            .Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(other);
        (await FluentActions.Awaiting(() => RefuseInsideAsync(middleware, first, body, codes: true))
            .Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(body);
        await FluentActions.Awaiting(() => RefuseInsideAsync(middleware, second, body, codes: true)).Should().ThrowAsync<InvalidOperationException>();

        uncoded.Logs.Entries.Should().BeEmpty("no value object explains that failure");
        var warning = first.Logs.Entries.Should().ContainSingle().Which;
        warning.Level.Should().Be(LogLevel.Warning);
        warning.EventName.Should().Be("ValueObjectProblemDetailsOutsideIntegration");
        warning.Message.Should().Contain("'Body'").And.Contain("ConfigureFunctionsWebApplication()");
        second.Logs.Entries.Should().BeEmpty("the warning is logged once");
        first.Result.Should().BeNull();
        second.Result.Should().BeNull();
    }

    [Fact]
    public async Task A_refused_route_value_is_answered_with_the_validation_problem_minimal_APIs_write()
    {
        var request = FunctionsHttp.Request();
        var invocation = new FunctionInvocation("Order", [Parameter<CustomerId>("id")], new Dictionary<string, object?> { ["id"] = "not-a-guid" }, request);

        await FunctionsHttp.Middleware().RefuseAsync(invocation);
        var body = await FunctionsHttp.ExecuteAsync(invocation, request);

        request.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        request.Response.ContentType.Should().Be("application/problem+json");
        var (message, code) = ProblemReading.Refusal<CustomerId, Guid>("not-a-guid");
        JsonSerializer.Serialize(JsonDocument.Parse(body).RootElement).Should().Be(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            ["title"] = "One or more validation errors occurred.",
            ["status"] = 400,
            ["errors"] = new Dictionary<string, string[]> { ["id"] = [message] },
            [ValueObjectProblemDetails.ExtensionName] = new Dictionary<string, string> { ["id"] = code },
        }));
        body.Should().NotContain("not-a-guid", "an answer never carries the refused text");
    }

    /// <summary>
    /// Every value-object parameter the converter reads is parsed again from the binding data, which holds the route
    /// values and every query value as text, and each refused one is listed with the rule that refused it, a generic
    /// construction and a value object written by hand included; one accepted, or accepted once its JSON string literal is
    /// decoded, is not.
    /// </summary>
    [Fact]
    public async Task Every_refused_value_object_is_listed_with_its_rule_and_an_accepted_one_is_not()
    {
        ValueObjectRegistry.TryGet(typeof(Reference<ValueObjectProblemDetailsMiddlewareTests>), out _).Should().BeFalse("no test has met it yet");
        ValueObjectRegistry.TryGet(typeof(Declared<MiddlewareTicket, string>), out _).Should().BeFalse("no test has met it yet");
        var request = FunctionsHttp.Request();
        var invocation = new FunctionInvocation(
            "Two",
            [
                Parameter<CustomerId>("id"), Parameter<Quantity>("qty"), Parameter<EmailAddress>("email"),
                Parameter<EmailAddress?>("cc"), Parameter<OccurredAt>("at"), Parameter<Amount?>("amount"),
                Parameter<Reference<ValueObjectProblemDetailsMiddlewareTests>>("reference"),
                Parameter<Declared<MiddlewareTicket, string>>("ticket"), Parameter<HandWrittenId<SilentFunctionsProfile>>("silent"),
            ],
            new Dictionary<string, object?>
            {
                ["ID"] = "not-a-guid",
                ["qty"] = "5000",
                ["email"] = "someone@example.com",
                ["cc"] = string.Empty,
                ["at"] = "\"2026-10-13T10:13:54+00:00\"",
                ["amount"] = "12.5",
                ["reference"] = "ABCDEFGHIJKLMNOP",
                ["ticket"] = "a1",
                ["silent"] = "fnz_anything",
            },
            request);

        await FunctionsHttp.Middleware().RefuseAsync(invocation);

        (await FunctionsHttp.AnswerAsync(invocation, request)).Should().Be(Problem(
            ("id", ProblemReading.Refusal<CustomerId, Guid>("not-a-guid")),
            ("qty", ("The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange)),
            ("cc", ProblemReading.Refusal<EmailAddress, string>(string.Empty)),
            ("reference", ("The value must be at most 12 characters long.", ValueObjectErrorCodes.TooLong)),
            ("ticket", ("A ticket holds ASCII letters only.", ValueObjectErrorCodes.InvalidFormat)),
            ("silent", ("The value is not a valid HandWrittenId<SilentFunctionsProfile>.", ValueObjectErrorCodes.NotParsable))));
        ValueObjectRegistry.TryGet(typeof(Reference<ValueObjectProblemDetailsMiddlewareTests>), out _).Should().BeTrue();
        ValueObjectRegistry.TryGet(typeof(Declared<MiddlewareTicket, string>), out _).Should().BeTrue();
    }

    /// <summary>
    /// A value object with no binding data fails the worker's binding only where it is required: the worker binds
    /// <see langword="null"/> to a nullable one and its default to an optional one, and fails any other with its own
    /// message, which the answer reports as the absent value the generated code refuses. Binding data that is not text is
    /// not the converter's to read.
    /// </summary>
    [Fact]
    public async Task An_absent_value_is_required_only_where_the_worker_requires_it()
    {
        var request = FunctionsHttp.Request();
        var invocation = new FunctionInvocation(
            "Query",
            [
                Parameter<EmailAddress>("customer"), Parameter<EmailAddress?>("optional"),
                Parameter<Quantity>("defaulted", hasDefaultValue: true), Parameter<CustomerId>("unset"), Parameter<Quantity>("count"),
            ],
            new Dictionary<string, object?> { ["unset"] = null, ["count"] = 7 },
            request);

        await FunctionsHttp.Middleware().RefuseAsync(invocation);

        (await FunctionsHttp.AnswerAsync(invocation, request)).Should().Be(Problem(
            ("customer", ("A value is required.", ValueObjectErrorCodes.Required)),
            ("unset", ("A value is required.", ValueObjectErrorCodes.Required))));
    }

    /// <summary>
    /// A parameter the converter does not read is not explained: one carrying a binding attribute, whose payload is the
    /// binding's, and one whose <c>[InputConverter]</c> names another converter, a <c>[FromBody]</c> value object, which has
    /// no binding data and would otherwise be reported required. One naming the value object converter is read.
    /// </summary>
    [Fact]
    public async Task A_parameter_the_converter_does_not_read_is_not_explained()
    {
        var request = FunctionsHttp.Request();
        var invocation = new FunctionInvocation(
            "Mixed",
            [
                Parameter<Quantity>("message", ImmutableDictionary<string, object>.Empty.Add("bindingAttribute", new object())),
                Parameter<Quantity>("order", ImmutableDictionary<string, object>.Empty.Add("converterType", new FromBodyAttribute().ConverterType.AssemblyQualifiedName!)),
                Parameter<Quantity>("raw", ImmutableDictionary<string, object>.Empty.Add("converterType", typeof(ValueObjectInputConverter).AssemblyQualifiedName!)),
                Parameter<int>("n"),
            ],
            new Dictionary<string, object?> { ["message"] = "5000", ["raw"] = "5000", ["n"] = "007" },
            request);

        await FunctionsHttp.Middleware().RefuseAsync(invocation);

        (await FunctionsHttp.AnswerAsync(invocation, request)).Should().Be(Problem(
            ("raw", ("The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange))));
    }

    /// <summary>
    /// A failure no value object explains, a plain <see cref="int"/> the worker's JSON conversion refuses among them, keeps
    /// the worker's 500: the middleware rethrows the very exception, and logs nothing.
    /// </summary>
    [Fact]
    public async Task A_failure_no_value_object_explains_keeps_the_workers_failure()
    {
        var invocation = new FunctionInvocation(
            "Mixed",
            [Parameter<Quantity>("qty"), Parameter<int>("n")],
            new Dictionary<string, object?> { ["qty"] = "7", ["n"] = "007" },
            FunctionsHttp.Request());
        var unbound = FunctionsHttp.Unbound();

        (await FluentActions.Awaiting(() => FunctionsHttp.Middleware().RefuseAsync(invocation, unbound))
            .Should().ThrowAsync<FunctionInputConverterException>()).Which.Should().BeSameAs(unbound);
        invocation.Result.Should().BeNull();
        invocation.Logs.Entries.Should().BeEmpty();
    }

    /// <summary>
    /// The host counts an answered invocation as succeeded, as ASP.NET Core counts a 400, so each answer is logged once
    /// through the function's logger: the parameters and their codes, never the values.
    /// </summary>
    [Fact]
    public async Task Each_answer_is_logged_once_with_the_parameters_and_their_codes_and_never_the_values()
    {
        var invocation = new FunctionInvocation(
            "Two",
            [Parameter<CustomerId>("id"), Parameter<Quantity>("qty")],
            new Dictionary<string, object?> { ["id"] = "not-a-guid", ["qty"] = "5000" },
            FunctionsHttp.Request());

        await FunctionsHttp.Middleware().RefuseAsync(invocation);

        var entry = invocation.Logs.Entries.Should().ContainSingle().Which;
        entry.Level.Should().Be(LogLevel.Information);
        entry.EventName.Should().Be("ValueObjectsRefused");
        entry.Category.Should().Be("AdCodicem.ValueObjects.AzureFunctions.AspNetCore.ValueObjectProblemDetailsMiddleware");
        entry.Message.Should().Be("Function 'Two' answered 400 for refused value objects: id (value_object.not_parsable), qty (value_object.out_of_range).");
    }

    /// <summary>
    /// A <c>[FromBody]</c> value object MVC's JSON input formatter refused, with the MVC package's <c>AddValueObjects()</c>,
    /// surfaces as the integration's <see cref="InvalidOperationException"/>, holding MVC's messages a line each, beside
    /// the codes the formatter recorded on the request: it is answered under its JSON path, before the refused route
    /// values.
    /// </summary>
    [Fact]
    public async Task A_body_MVC_refused_is_answered_under_its_JSON_path_beside_the_refused_route_values()
    {
        const string Message = "The value is not a valid Quantity: The value must be less than or equal to 1000.";
        var request = FunctionsHttp.Request();
        ValueObjectProblemDetails.RecordErrorCode(request, "$.quantity", ValueObjectErrorCodes.OutOfRange);
        var invocation = new FunctionInvocation(
            "BodyAndRoute",
            [Parameter<Quantity>("order", ImmutableDictionary<string, object>.Empty.Add("converterType", new FromBodyAttribute().ConverterType.AssemblyQualifiedName!)), Parameter<Quantity>("qty")],
            new Dictionary<string, object?> { ["qty"] = "5000" },
            request);

        await FunctionsHttp.Middleware().RefuseAsync(invocation, new InvalidOperationException(Message + Environment.NewLine));

        var problem = await FunctionsHttp.AnswerAsync(invocation, request);
        problem.Should().Be(Problem(
            ("$.quantity", (Message, ValueObjectErrorCodes.OutOfRange)),
            ("qty", ("The value must be less than or equal to 1000.", ValueObjectErrorCodes.OutOfRange))));
        problem.Messages.Keys.Should().Equal("$.quantity", "qty");
    }

    /// <summary>
    /// The integration joins MVC's messages in one text, a line each, and the codes are recorded by member apart from
    /// them: with several codes, each member is given every line, since nothing tells which belongs to which, and keeps its
    /// own code. System.Text.Json stops at the first refusal, so there is one of each.
    /// </summary>
    [Fact]
    public async Task Several_codes_beside_several_lines_each_get_every_line()
    {
        var request = FunctionsHttp.Request();
        ValueObjectProblemDetails.RecordErrorCode(request, "quantity", ValueObjectErrorCodes.OutOfRange);
        ValueObjectProblemDetails.RecordErrorCode(request, "customer", ValueObjectErrorCodes.InvalidFormat);
        var invocation = new FunctionInvocation("Body", [], httpContext: request);

        await FunctionsHttp.Middleware().RefuseAsync(invocation, new InvalidOperationException("first\r\n\r\n second \nlast\n"));

        (await FunctionsHttp.AnswerAsync(invocation, request)).Should().Be(new ReadProblem(
            new() { ["quantity"] = ["first", "second", "last"], ["customer"] = ["first", "second", "last"] },
            new() { ["quantity"] = ValueObjectErrorCodes.OutOfRange, ["customer"] = ValueObjectErrorCodes.InvalidFormat }));
    }

    /// <summary>
    /// A member refused by the body and again as a route value of the same name is listed once, with both messages, the
    /// body's first, and keeps the code of its first refusal, as the minimal API's problem details keep it.
    /// </summary>
    [Fact]
    public async Task A_member_refused_twice_keeps_the_code_of_its_first_refusal()
    {
        var request = FunctionsHttp.Request();
        ValueObjectProblemDetails.RecordErrorCode(request, "qty", ValueObjectErrorCodes.InvalidFormat);
        var invocation = new FunctionInvocation("BodyAndRoute", [Parameter<Quantity>("qty")], new Dictionary<string, object?> { ["qty"] = "5000" }, request);

        await FunctionsHttp.Middleware().RefuseAsync(invocation, new InvalidOperationException("The body's quantity is refused."));

        (await FunctionsHttp.AnswerAsync(invocation, request)).Should().Be(new ReadProblem(
            new() { ["qty"] = ["The body's quantity is refused.", "The value must be less than or equal to 1000."] },
            new() { ["qty"] = ValueObjectErrorCodes.InvalidFormat }));
    }

    /// <summary>
    /// An <see cref="InvalidOperationException"/> no recorded code explains is not a value object's, and one whose codes
    /// come with no message has nothing to answer with: both keep the worker's failure, the very exception.
    /// </summary>
    [Fact]
    public async Task An_InvalidOperationException_without_a_recorded_code_or_a_message_passes_through()
    {
        var uncoded = new FunctionInvocation("Body", [Parameter<Quantity>("qty")], new Dictionary<string, object?> { ["qty"] = "5000" }, FunctionsHttp.Request());
        var other = new InvalidOperationException("Something else failed.");

        (await FluentActions.Awaiting(() => FunctionsHttp.Middleware().RefuseAsync(uncoded, other))
            .Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(other);

        var request = FunctionsHttp.Request();
        ValueObjectProblemDetails.RecordErrorCode(request, "$.quantity", ValueObjectErrorCodes.OutOfRange);
        var silent = new FunctionInvocation("Body", [Parameter<Quantity>("qty")], new Dictionary<string, object?> { ["qty"] = "7" }, request);
        var empty = new InvalidOperationException(Environment.NewLine);

        (await FluentActions.Awaiting(() => FunctionsHttp.Middleware().RefuseAsync(silent, empty))
            .Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(empty);
        uncoded.Result.Should().BeNull();
        silent.Result.Should().BeNull();
    }

    /// <summary>The codes are keyed as the serializer keys <c>errors</c>, through the application's dictionary key policy.</summary>
    [Fact]
    public async Task The_codes_are_keyed_as_the_serializer_keys_the_messages()
    {
        var request = FunctionsHttp.Request(services => services.Configure<HttpJsonOptions>(
            options => options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase));
        var invocation = new FunctionInvocation("Order", [Parameter<CustomerId>("CustomerId")], new Dictionary<string, object?> { ["CustomerId"] = "not-a-guid" }, request);

        await FunctionsHttp.Middleware().RefuseAsync(invocation);

        var problem = await FunctionsHttp.AnswerAsync(invocation, request);
        problem.Messages.Keys.Should().Equal("customerId");
        problem.Codes.Keys.Should().Equal("customerId");
    }

    /// <summary>
    /// Runs the middleware around an invocation whose request is set only inside it, as the integration's proxying
    /// middleware sets it when it runs inside this one, and which then fails as the integration fails a body MVC refused.
    /// </summary>
    private static Task RefuseInsideAsync(ValueObjectProblemDetailsMiddleware middleware, FunctionInvocation invocation, Exception failure, bool codes)
        => middleware.Invoke(invocation, context =>
        {
            var request = FunctionsHttp.Request();
            if (codes)
            {
                ValueObjectProblemDetails.RecordErrorCode(request, "$.quantity", ValueObjectErrorCodes.OutOfRange);
            }

            context.Items["HttpRequestContext"] = request;
            throw failure;
        });

    private static ReadProblem Problem(params (string Member, (string Message, string Code) Refusal)[] refusals)
        => new(
            refusals.ToDictionary(static refusal => refusal.Member, static refusal => new[] { refusal.Refusal.Message }),
            refusals.ToDictionary(static refusal => refusal.Member, static refusal => refusal.Refusal.Code));
}
