using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;

namespace AdCodicem.ValueObjects.UnitTests.WolverineHttp;

/// <summary>
/// Requests answered through the code Wolverine generates and compiles at startup, in the fixture application
/// <c>tests/Fixtures/AdCodicem.ValueObjects.Fixtures.WolverineHttp</c>, which runs Wolverine's dynamic mode with its
/// runtime compiler in a process of its own: lenient, Wolverine's default; strict, with
/// <c>RejectUnparseableQueryValues = true</c>; and with French messages.
/// </summary>
public sealed class WolverineHttpEndToEndTests(WolverineHttpApplication application) : IClassFixture<WolverineHttpApplication>
{
    [Fact]
    public void A_refused_route_value_of_every_family_is_answered_400_with_its_code_not_404()
    {
        var lenient = application.Lenient;

        lenient["GET /orders/3f2504e0-4f89-41d3-9a0c-0305e82c3301"].ShouldAnswer("order 3f2504e0-4f89-41d3-9a0c-0305e82c3301");
        lenient["GET /orders/not-a-guid"].ShouldRefuse(("id", ValueObjectErrorCodes.NotParsable, "The text is not a valid System.Guid."));
        lenient["GET /lines/7"].ShouldAnswer("line 7");
        lenient["GET /lines/007"].ShouldAnswer("line 7");
        lenient["GET /lines/500"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
        lenient["GET /lines/x"].ShouldRefuse(("qty", ValueObjectErrorCodes.NotParsable, "The text is not a valid int."));
        lenient["GET /refs/po-1042"].ShouldAnswer("reference PO-1042");
        lenient["GET /refs/ABCDEFGHIJKLMNOP"].ShouldRefuse(("reference", ValueObjectErrorCodes.TooLong, "The value must be at most 12 characters long."));
        lenient["GET /hand/abc"].ShouldAnswer("hand ABC");
        lenient["GET /hand/a1"].ShouldRefuse(("code", ValueObjectErrorCodes.InvalidFormat, "A code holds ASCII letters only."));
        lenient["GET /due/2026-10-17"].ShouldAnswer("due 2026-10-17");
        lenient["GET /due/17-10-2026"].ShouldRefuse(("due", ValueObjectErrorCodes.NotParsable, "The text is not a valid System.DateOnly."));
        lenient["GET /renamed/500"].ShouldRefuse(("n", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
    }

    [Fact]
    public void A_route_constraint_still_answers_text_it_refuses_404_before_any_binding()
    {
        application.Lenient["GET /constrained/5"].ShouldAnswer("constrained 5");
        application.Lenient["GET /constrained/500"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
        application.Lenient["GET /constrained/x"].Status.Should().Be(404);
    }

    [Fact]
    public void A_query_value_is_refused_when_its_rules_refuse_it_when_its_text_is_empty_and_when_it_is_required_and_absent()
    {
        var lenient = application.Lenient;

        lenient["GET /items?qty=5"].ShouldAnswer("items 5");
        lenient["GET /items?QTY=5"].ShouldAnswer("items 5");
        lenient["GET /items?qty=500"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
        lenient["GET /items?qty=x"].ShouldRefuse(("qty", ValueObjectErrorCodes.NotParsable, "The text is not a valid int."));
        lenient["GET /items?qty="].ShouldRefuse(("qty", ValueObjectErrorCodes.NotParsable, "The text is not a valid int."));
        lenient["GET /items"].ShouldRefuse(("qty", ValueObjectErrorCodes.Required, "A value is required."));
        lenient["GET /items?qty=5&qty=6"].ShouldRefuse(("qty", ValueObjectErrorCodes.NotParsable, "The text is not a valid int."));
        lenient["GET /named?q=5"].ShouldAnswer("named 5");
        lenient["GET /named?q=500"].ShouldRefuse(("q", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
        lenient["GET /named?quantity=5"].ShouldRefuse(("q", ValueObjectErrorCodes.Required, "A value is required."));
        lenient["GET /quoted?a%22b%5Cc=5"].ShouldAnswer("quoted 5");
        lenient["GET /quoted?a%22b%5Cc=500"].ShouldRefuse(("a\"b\\c", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
    }

    [Fact]
    public void An_optional_value_binds_null_or_its_default_when_absent_and_is_refused_when_its_text_is_refused()
    {
        var lenient = application.Lenient;

        lenient["GET /optional?country=fr"].ShouldAnswer("optional FR");
        lenient["GET /optional?country=XX"].ShouldRefuse(("country", ValueObjectErrorCodes.NotAKnownValue, "The value is not one of the accepted values."));
        lenient["GET /optional?country="].ShouldRefuse(("country", ValueObjectErrorCodes.Required, "The value must not be empty."));
        lenient["GET /optional"].ShouldAnswer("optional null");
        lenient["GET /defaulted?qty=5"].ShouldAnswer("defaulted 5");
        lenient["GET /defaulted?qty=500"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
        lenient["GET /defaulted"].ShouldAnswer("defaulted default");
        lenient["GET /optional-segment/5"].ShouldAnswer("optional-segment 5");
        lenient["GET /optional-segment"].ShouldRefuse(("qty", ValueObjectErrorCodes.Required, "A value is required."));
        lenient["GET /optional-segment/500"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
        lenient["GET /nullable-segment/5"].ShouldAnswer("nullable-segment 5");
        lenient["GET /nullable-segment"].ShouldAnswer("nullable-segment null");
        lenient["GET /nullable-segment/500"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
    }

    [Fact]
    public void Every_element_of_a_collection_is_read_through_its_rules()
    {
        var lenient = application.Lenient;

        lenient["GET /many?qty=1&qty=2"].ShouldAnswer("many [1,2]");
        lenient["GET /many?qty=1&qty=500&qty=x"].ShouldRefuse(
            "qty",
            ValueObjectErrorCodes.OutOfRange,
            "The value must be less than or equal to 100.",
            "The text is not a valid int.");
        lenient["GET /many?qty="].ShouldRefuse(("qty", ValueObjectErrorCodes.NotParsable, "The text is not a valid int."));
        lenient["GET /many"].ShouldAnswer("many []");
        lenient["GET /list?qty=1&qty=2"].ShouldAnswer("list [1,2] List`1");
        lenient["GET /list?qty=500"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
        lenient["GET /list"].ShouldAnswer("list [] List`1");
        lenient["GET /sequence?qty=3"].ShouldAnswer("sequence [3]");
        lenient["GET /sequence?qty=0"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "The value must be greater than or equal to 1."));
        lenient["GET /readonly?qty=4"].ShouldAnswer("readonly [4]");
        lenient["GET /readonly?qty=x"].ShouldRefuse(("qty", ValueObjectErrorCodes.NotParsable, "The text is not a valid int."));
    }

    [Fact]
    public void Every_refusal_of_a_request_is_answered_at_once_its_codes_recorded_on_the_request()
    {
        var lenient = application.Lenient;

        lenient["GET /when?due=2026-10-17&at=2026-10-17T10:00:00Z&consent=true&amount=12.5"]
            .ShouldAnswer("when 2026-10-17 2026-10-17T10:00:00.0000000+00:00 True 12.5");
        var refused = lenient["GET /when?due=x&at=x&consent=x&amount=-1"];
        refused.ShouldRefuse(
            ("due", ValueObjectErrorCodes.NotParsable, "The text is not a valid System.DateOnly."),
            ("at", ValueObjectErrorCodes.NotParsable, "The text is not a valid System.DateTimeOffset."),
            ("consent", ValueObjectErrorCodes.NotParsable, "The text is not a valid bool."),
            ("amount", ValueObjectErrorCodes.OutOfRange, "The value must be greater than or equal to 0."));
        refused.Notes.Should().Equal(
            "recorded due=value_object.not_parsable,at=value_object.not_parsable,consent=value_object.not_parsable,amount=value_object.out_of_range");
    }

    [Fact]
    public void A_header_is_read_through_its_rules_a_header_sent_twice_parsed_as_its_joined_text()
    {
        var lenient = application.Lenient;

        lenient["GET /header [X-Customer: ABC-12345]"].ShouldAnswer("header ABC-12345");
        lenient["GET /header [X-Customer: abc]"].ShouldRefuse(("X-Customer", ValueObjectErrorCodes.InvalidFormat, "The value does not match the expected format."));
        lenient["GET /header"].ShouldRefuse(("X-Customer", ValueObjectErrorCodes.Required, "A value is required."));
        lenient["GET /header [X-Customer: ABC-12345,DEF-12345]"]
            .ShouldRefuse(("X-Customer", ValueObjectErrorCodes.InvalidFormat, "The value does not match the expected format."));
        lenient["GET /nullable-header"].ShouldAnswer("nullable-header null");
        lenient["GET /nullable-header [X-Country: de]"].ShouldAnswer("nullable-header DE");
        lenient["GET /nullable-header [X-Country: XX]"]
            .ShouldRefuse(("X-Country", ValueObjectErrorCodes.NotAKnownValue, "The value is not one of the accepted values."));
        lenient["GET /route-or-header/5 [qty: 500]"].ShouldAnswer("route-or-header 5");
    }

    [Fact]
    public void A_form_field_is_read_through_its_rules()
    {
        var lenient = application.Lenient;

        lenient["POST /form qty=5"].ShouldAnswer("form 5");
        lenient["POST /form qty=500"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
        lenient["POST /form nothing"].ShouldRefuse(("qty", ValueObjectErrorCodes.Required, "A value is required."));
        lenient["POST /form json"].ShouldRefuse(("qty", ValueObjectErrorCodes.Required, "A value is required."));
        lenient["POST /form-many qty=5&qty=6"].ShouldAnswer("form-many [5,6]");
        lenient["POST /form-many qty=5&qty=500"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
        lenient["POST /form-many nothing"].ShouldAnswer("form-many []");
    }

    [Fact]
    public void A_compound_endpoint_s_Before_method_never_runs_for_a_refused_request()
    {
        application.Lenient["GET /compound?qty=5"].ShouldAnswer("compound 5");
        application.Lenient["GET /compound?qty=5"].Notes.Should().Equal("before 5");
        application.Lenient["GET /compound?qty=500"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
        application.Lenient["GET /compound?qty=500"].Notes.Should().NotContain(static note => note.StartsWith("before", StringComparison.Ordinal));
    }

    [Fact]
    public void A_value_Wolverine_looks_up_again_by_its_name_is_the_package_s_read()
    {
        var lenient = application.Lenient;

        lenient["GET /after-route/5"].ShouldAnswer("after-route 5");
        lenient["GET /after-route/5"].Notes.Should().Equal("after 5");
        lenient["GET /after-route/500"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
        lenient["GET /after-header [X-Customer: ABC-12345]"].ShouldAnswer("after-header ABC-12345");
        lenient["GET /after-header [X-Customer: ABC-12345]"].Notes.Should().Equal("after ABC-12345");
        lenient["GET /after-header [X-Customer: abc]"]
            .ShouldRefuse(("X-Customer", ValueObjectErrorCodes.InvalidFormat, "The value does not match the expected format."));
        lenient["GET /finally/5"].ShouldAnswer("finally 5");
        lenient["GET /finally/5"].Notes.Should().Equal("finally 5");
        lenient["GET /finally/500"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
        lenient["GET /entity/3f2504e0-4f89-41d3-9a0c-0305e82c3301"].ShouldAnswer("entity view of 3f2504e0-4f89-41d3-9a0c-0305e82c3301");
        lenient["GET /entity/not-a-guid"].ShouldRefuse(("id", ValueObjectErrorCodes.NotParsable, "The text is not a valid System.Guid."));
        lenient["GET /entity?id=3f2504e0-4f89-41d3-9a0c-0305e82c3301"].ShouldAnswer("entity view of 3f2504e0-4f89-41d3-9a0c-0305e82c3301");
        lenient["GET /entity?id=not-a-guid"].ShouldRefuse(("id", ValueObjectErrorCodes.NotParsable, "The text is not a valid System.Guid."));
        foreach (var refused in new[] { "GET /after-route/500", "GET /after-header [X-Customer: abc]", "GET /finally/500" })
        {
            lenient[refused].Notes.Should().ContainSingle("{0} is answered before the handler, its After and its Finally run", refused);
        }
    }

    [Fact]
    public void A_value_object_of_a_middleware_method_a_later_policy_adds_is_answered_before_that_method_runs()
    {
        var lenient = application.Lenient;

        lenient["GET /late?qty=5 [X-Late: ABC-12345]"].ShouldAnswer("late 5");
        lenient["GET /late?qty=5 [X-Late: ABC-12345]"].Notes.Should().Equal("late ABC-12345");
        lenient["GET /late?qty=500 [X-Late: abc]"].ShouldRefuse(
            ("qty", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."),
            ("X-Late", ValueObjectErrorCodes.InvalidFormat, "The value does not match the expected format."));
        lenient["GET /late?qty=500 [X-Late: abc]"].Notes.Should().NotContain(static note => note.StartsWith("late", StringComparison.Ordinal));
        lenient["GET /late-only [X-Late: ABC-12345]"].ShouldAnswer("late-only");
        lenient["GET /late-only [X-Late: abc]"]
            .ShouldRefuse(("X-Late", ValueObjectErrorCodes.InvalidFormat, "The value does not match the expected format."));
        lenient["GET /late-only [X-Late: abc]"].Notes.Should().NotContain(static note => note.StartsWith("late", StringComparison.Ordinal));
    }

    [Fact]
    public void A_class_parameter_after_a_form_value_leaves_the_form_request_to_the_endpoint()
        => application.Lenient["POST /form-payload qty=5"].ShouldAnswer("form-payload 5 True");

    [Fact]
    public void AsParameters_members_and_the_JSON_body_stay_Wolverine_s_and_the_strict_flag_changes_nothing_else()
    {
        application.Lenient["GET /as-parameters?customer=ABC-123&quantity=5&country=fr"].ShouldAnswer("as-parameters ABC-123 5 FR");
        application.Lenient["GET /as-parameters?customer=abc&quantity=500&country=XX"].ShouldAnswer("as-parameters  0 null");

        var strict = application.Strict["GET /as-parameters?customer=abc&quantity=500&country=XX"];
        strict.Status.Should().Be(400);
        var body = JsonDocument.Parse(strict.Body).RootElement;
        body.GetProperty("title").GetString().Should().Be("Invalid query string value");
        body.TryGetProperty(ValueObjectProblemDetails.ExtensionName, out _).Should().BeFalse("Wolverine's own 400 carries no code");

        foreach (var (request, answer) in application.Lenient.Where(static line => !line.Key.StartsWith("GET /as-parameters", StringComparison.Ordinal)))
        {
            application.Strict[request].Should().Be(answer, "the strict flag leaves {0} to the package", request);
        }

        var order = application.Lenient["POST /orders quantity 500"];
        order.Status.Should().Be(400);
        var problem = JsonDocument.Parse(order.Body).RootElement;
        problem.GetProperty("title").GetString().Should().Be("Invalid JSON format");
        problem.GetProperty("detail").GetString().Should().Be("The value is not a valid Quantity: The value must be less than or equal to 100.");
        problem.TryGetProperty(ValueObjectProblemDetails.ExtensionName, out _).Should().BeFalse("the body's refusal reaches the problem details without its exception");
    }

    [Fact]
    public void The_messages_of_the_application_s_resource_answer_in_the_language_of_the_request()
    {
        var french = application.French;

        french["GET /items?qty=500"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "« qty » doit être compris entre 1 et 100."));
        french["GET /items"].ShouldRefuse(("qty", ValueObjectErrorCodes.Required, "« qty » est obligatoire."));
        french["GET /optional?country=XX"].ShouldRefuse(("country", ValueObjectErrorCodes.NotAKnownValue, "« country » doit être l'une des valeurs suivantes : FR; DE."));
        french["GET /when?due=x&at=x&consent=x&amount=-1"].ShouldRefuse(
            ("due", ValueObjectErrorCodes.NotParsable, "« due » n'a pas la forme attendue."),
            ("at", ValueObjectErrorCodes.NotParsable, "« at » n'a pas la forme attendue."),
            ("consent", ValueObjectErrorCodes.NotParsable, "« consent » n'a pas la forme attendue."),
            ("amount", ValueObjectErrorCodes.OutOfRange, "« amount » doit valoir au moins 0."));
        french["GET /lines/7"].ShouldAnswer("line 7");
        french["GET /items?qty=500 in English"].ShouldRefuse(("qty", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 100."));
    }
}

/// <summary>
/// Runs the fixture application once per configuration, each in a process of its own, and keeps the answers it writes,
/// by request.
/// </summary>
public sealed class WolverineHttpApplication : IAsyncLifetime
{
    /// <summary>Gets the answers under Wolverine's default, <c>RejectUnparseableQueryValues = false</c>.</summary>
    public IReadOnlyDictionary<string, WolverineHttpAnswer> Lenient { get; private set; } = null!;

    /// <summary>Gets the answers with <c>RejectUnparseableQueryValues = true</c>.</summary>
    public IReadOnlyDictionary<string, WolverineHttpAnswer> Strict { get; private set; } = null!;

    /// <summary>Gets the answers in French.</summary>
    public IReadOnlyDictionary<string, WolverineHttpAnswer> French { get; private set; } = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var runs = await Task.WhenAll(RunAsync("lenient"), RunAsync("strict"), RunAsync("french"));
        (Lenient, Strict, French) = (runs[0], runs[1], runs[2]);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<IReadOnlyDictionary<string, WolverineHttpAnswer>> RunAsync(string configuration)
    {
        var cancellation = TestContext.Current.CancellationToken;
        var folder = typeof(WolverineHttpApplication).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(static metadata => metadata.Key == "WolverineHttpApplication").Value!;
        var application = Path.Combine(folder, "AdCodicem.ValueObjects.Fixtures.WolverineHttp.dll");
        File.Exists(application).Should().BeTrue("building this project builds the fixture application at {0}", application);

        var output = Path.Combine(Directory.CreateTempSubdirectory("adcodicem-wolverine-http-").FullName, $"{configuration}.jsonl");
        try
        {
            var start = new ProcessStartInfo(WithoutDynamicCode.Host())
            {
                ArgumentList = { application, configuration, output },
                WorkingDirectory = folder,
                RedirectStandardOutput = true,
                RedirectStandardError = true,

                // The coverage collector follows a child through the CLR profiler its environment enables. The
                // application's coverage adds nothing the tests in process do not cover, and merged with this process's
                // report it loses hits of the packages both load (the request body's refusal of the minimal API package):
                // the child runs without the profiler.
                Environment = { ["CORECLR_ENABLE_PROFILING"] = "0" },
            };

            using var child = Process.Start(start)!;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromMinutes(3));
            var standardOutput = child.StandardOutput.ReadToEndAsync(timeout.Token);
            var standardError = child.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await child.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                child.Kill(entireProcessTree: true);
                throw;
            }

            var report = await standardOutput + await standardError;
            child.ExitCode.Should().Be(0, "the {0} run answers its script:{1}{2}", configuration, Environment.NewLine, report);

            var answers = new Dictionary<string, WolverineHttpAnswer>(StringComparer.Ordinal);
            foreach (var line in await File.ReadAllLinesAsync(output, cancellation))
            {
                var answer = JsonSerializer.Deserialize<WolverineHttpAnswer>(line)!;
                answers.Add(answer.Request, answer);
            }

            return answers;
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(output)!, recursive: true);
        }
    }
}

/// <summary>The answer the fixture application wrote for one request.</summary>
/// <param name="Request">The request's label.</param>
/// <param name="Status">The status code, or <see langword="null"/> when the request threw.</param>
/// <param name="Body">The body, or the exception the request threw.</param>
/// <param name="Notes">What the endpoints and the problem details wrote while it ran.</param>
public sealed record WolverineHttpAnswer(string Request, int? Status, string Body, string[] Notes)
{
    /// <inheritdoc />
    public bool Equals(WolverineHttpAnswer? other)
        => other is not null && Request == other.Request && Status == other.Status && Body == other.Body && Notes.SequenceEqual(other.Notes);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Request, Status, Body);

    /// <summary>Asserts that the endpoint ran, and answered the text given.</summary>
    /// <param name="text">The endpoint's answer.</param>
    public void ShouldAnswer(string text)
    {
        Status.Should().Be(200, "{0} is answered by its endpoint: {1}", Request, Body);
        Body.Should().Be(text);
    }

    /// <summary>Asserts that the request was answered with the validation problem of the refusals given, each one message.</summary>
    /// <param name="refusals">The members refused, each with its code and its message.</param>
    public void ShouldRefuse(params (string Member, string Code, string Message)[] refusals)
    {
        var problem = Web.MinimalApiProblemDetailsTests.ProblemOf(Status ?? 0, Body);
        problem.Should().Be(new Web.ReadProblem(
            refusals.ToDictionary(static refusal => refusal.Member, static refusal => new[] { refusal.Message }),
            refusals.ToDictionary(static refusal => refusal.Member, static refusal => refusal.Code)));
        Notes.Should().Contain($"recorded {string.Join(",", refusals.Select(static refusal => $"{refusal.Member}={refusal.Code}"))}");
        JsonDocument.Parse(Body).RootElement.GetProperty("title").GetString().Should().Be("One or more validation errors occurred.");
    }

    /// <summary>Asserts that the request was answered with the validation problem of one member refused several times.</summary>
    /// <param name="member">The member.</param>
    /// <param name="code">The code of its first refusal.</param>
    /// <param name="messages">Its messages, in order.</param>
    public void ShouldRefuse(string member, string code, params string[] messages)
        => Web.MinimalApiProblemDetailsTests.ProblemOf(Status ?? 0, Body).Should().Be(new Web.ReadProblem(
            new() { [member] = messages },
            new() { [member] = code }));
}
