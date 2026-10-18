using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.Wolverine.Http;
using AdCodicem.ValueObjects.Wolverine.Http.Binding;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using JasperFx.CodeGeneration.Model;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Attributes;
using Wolverine.Http;
using Wolverine.Http.CodeGen;

namespace AdCodicem.ValueObjects.UnitTests.WolverineHttp;

/// <summary>
/// The parameter strategy over the chains Wolverine builds: what it claims and declines, where it reads each value from
/// and under which name, one read per value in an endpoint, and the refusal frame the policy puts first.
/// </summary>
[Collection(nameof(WolverineHttpHosts))]
public sealed class WolverineHttpStrategyTests
{
    [Fact]
    public async Task Every_shape_of_a_value_object_is_read_from_the_query_string()
    {
        await using var host = WolverineHttpHost.Map(typeof(WolverineShapeEndpoints));

        Read(host, "/single").Should().Be(Expected<Quantity, short>(ValueObjectHttpShape.Single, ValueObjectHttpSource.Query, "qty", required: true));
        Read(host, "/nullable").Should().Be(Expected<Quantity, short>(ValueObjectHttpShape.Nullable, ValueObjectHttpSource.Query, "qty", required: false));
        Read(host, "/array").Should().Be(Expected<Quantity, short>(ValueObjectHttpShape.Array, ValueObjectHttpSource.Query, "qty", required: false));
        Read(host, "/list").Should().Be(Expected<Quantity, short>(ValueObjectHttpShape.List, ValueObjectHttpSource.Query, "qty", required: false));
        Read(host, "/ilist").Should().Be(Expected<Quantity, short>(ValueObjectHttpShape.Sequence, ValueObjectHttpSource.Query, "qty", required: false));
        Read(host, "/readonly").Should().Be(Expected<Quantity, short>(ValueObjectHttpShape.Sequence, ValueObjectHttpSource.Query, "qty", required: false));
        Read(host, "/sequence").Should().Be(Expected<Quantity, short>(ValueObjectHttpShape.Sequence, ValueObjectHttpSource.Query, "qty", required: false));
        Read(host, "/defaulted").Required.Should().BeFalse("a default value makes the parameter optional, as in a minimal API");
        Read(host, "/generic").Should().Be(Expected<Reference<WolverineOwner>, string>(ValueObjectHttpShape.Single, ValueObjectHttpSource.Query, "reference", required: true));
        Read(host, "/hand").Should().Be(Expected<HandWrittenCode, string>(ValueObjectHttpShape.Single, ValueObjectHttpSource.Query, "code", required: true));
    }

    [Fact]
    public async Task Each_value_is_read_from_the_source_Wolverine_reads_it_from_under_the_name_it_reads()
    {
        await using var host = WolverineHttpHost.Map(typeof(WolverineSourceEndpoints));

        Read(host, "/form").Should().Be(Expected<Quantity, short>(ValueObjectHttpShape.Single, ValueObjectHttpSource.Form, "q", required: true));
        Read(host, "/form-many").Should().Be(Expected<Quantity, short>(ValueObjectHttpShape.Array, ValueObjectHttpSource.Form, "qty", required: false));
        Read(host, "/renamed/{N}").Should().Be(
            Expected<Quantity, short>(ValueObjectHttpShape.Single, ValueObjectHttpSource.Route, "N", required: true),
            "Wolverine compares the name a [FromRoute] gives without regard to case, and reads the template's");
        Read(host, "/route/{qty}").Should().Be(
            Expected<Quantity, short>(ValueObjectHttpShape.Single, ValueObjectHttpSource.Route, "qty", required: true),
            "a route parameter of the parameter's name wins over [FromHeader], as Wolverine's route strategy runs first");
        Read(host, "/route-attribute/{qty}").Source.Should().Be(ValueObjectHttpSource.Route);
        Read(host, "/header").Should().Be(Expected<Quantity, short>(ValueObjectHttpShape.Single, ValueObjectHttpSource.Header, "X-Quantity", required: true));
        Read(host, "/header-default").Should().Be(Expected<Quantity, short>(ValueObjectHttpShape.Single, ValueObjectHttpSource.Header, "qty", required: true));
        Read(host, "/query").Should().Be(Expected<Quantity, short>(ValueObjectHttpShape.Single, ValueObjectHttpSource.Query, "q", required: true));
        Read(host, "/query-default").Should().Be(
            Expected<Quantity, short>(ValueObjectHttpShape.Single, ValueObjectHttpSource.Query, "qty", required: true),
            "[FromQuery] on a value object maps, where Wolverine alone fails at startup with 'Sequence contains no elements'");
        Read(host, "/optional/{qty?}").Should().Be(
            Expected<Quantity, short>(ValueObjectHttpShape.Single, ValueObjectHttpSource.Route, "qty", required: true),
            "an optional segment leaves a value object that cannot be null required, so that the handler never sees a default instance");
        Read(host, "/optional-nullable/{qty?}").Required.Should().BeFalse();
    }

    [Fact]
    public void Wolverine_alone_fails_at_startup_on_a_value_object_marked_FromQuery()
    {
        var mapping = () => WolverineHttpHost.Map(typeof(WolverineSourceEndpoints), static _ => { });

        mapping.Should().Throw<InvalidOperationException>().WithMessage("Sequence contains no elements");
    }

    [Fact]
    public async Task A_compound_endpoint_reads_a_value_once_for_its_middleware_and_its_handler()
    {
        await using var host = WolverineHttpHost.Map(typeof(WolverineCompoundEndpoint));

        host.Reads("/compound").Should().ContainSingle("a second read of qty would declare the local twice (CS0128)");
        (await host.GenerateCodeAsync()).Should().Contain("WolverineCompoundEndpoint.Before(qty)").And.Contain("WolverineCompoundEndpoint.Get(qty)");
    }

    [Fact]
    public void One_name_bound_to_two_value_objects_in_an_endpoint_fails_the_mapping()
    {
        var mapping = () => WolverineHttpHost.Map(typeof(WolverineClashingEndpoint));

        mapping.Should().Throw<InvalidOperationException>()
            .WithMessage("The query value 'qty' is bound to both AdCodicem.ValueObjects.UnitTests.Domain.Quantity and AdCodicem.ValueObjects.UnitTests.Domain.CountryCode in GET_clash.");
    }

    [Fact]
    public void A_route_name_the_template_lacks_is_left_to_Wolverine_which_reports_it()
    {
        var mapping = () => WolverineHttpHost.Map(typeof(WolverineMissingRouteEndpoint));

        mapping.Should().Throw<InvalidOperationException>().WithMessage("Unable to find a route argument 'absent'*");
    }

    [Fact]
    public async Task The_refusal_frame_comes_first_in_an_endpoint_reading_a_value_object_and_nowhere_else()
    {
        await using var host = WolverineHttpHost.Map([typeof(WolverineCompoundEndpoint), typeof(WolverinePlainEndpoint)]);

        host.Chain("/compound").Middleware[0].Should().BeOfType<ValueObjectRefusalFrame>("no middleware method runs before the refusals are answered");
        host.Chain("/compound").Middleware.OfType<ValueObjectRefusalFrame>().Should().ContainSingle();
        host.Chain("/plain").Middleware.OfType<ValueObjectRefusalFrame>().Should().BeEmpty();
        host.Reads("/plain").Should().BeEmpty();
    }

    [Fact]
    public async Task Called_twice_UseValueObjects_installs_one_strategy_and_one_policy_and_keeps_the_messages_named_last()
    {
        await using var host = WolverineHttpHost.Map(typeof(WolverineCompoundEndpoint), static options =>
        {
            options.UseValueObjects(static messages => messages.UseResource<Web.ValidationMessages>());
            options.UseValueObjects();
        });

        host.Application.Services.GetRequiredService<WolverineHttpOptions>().Policies.OfType<ValueObjectRefusalPolicy>().Should().ContainSingle();
        host.Policy.Messages!.ResourceType.Should().Be<Web.ValidationMessages>("a call naming no messages keeps those named before");
        host.Chain("/compound").Middleware.OfType<ValueObjectRefusalFrame>().Should().ContainSingle();
        host.Reads("/compound").Should().ContainSingle();

        await using var renamed = WolverineHttpHost.Map(typeof(WolverineCompoundEndpoint), static options =>
        {
            options.UseValueObjects(static messages => messages.UseResource<Web.ValidationMessages>());
            options.UseValueObjects(static messages => messages.UseResource<Web.SparseMessages>());
        });

        renamed.Policy.Messages!.ResourceType.Should().Be<Web.SparseMessages>("the messages a call names last win");
    }

    [Fact]
    public async Task A_form_value_keeps_its_endpoint_marked_as_a_form_which_antiforgery_requires_a_token_of()
    {
        await using var host = WolverineHttpHost.Map(
            typeof(WolverineSourceEndpoints),
            static options =>
            {
                options.AutoAntiforgeryOnFormEndpoints();
                options.UseValueObjects();
            },
            static services => services.AddAntiforgery());

        var form = host.Chain("/form");
        form.IsFormData.Should().BeTrue("Wolverine's own [FromForm] strategy marks the endpoint, through an internal setter");
        form.RequestType.Should().Be(typeof(void));
        host.Chain("/form-many").IsFormData.Should().BeTrue();
        host.Chain("/query").IsFormData.Should().BeFalse();

        var endpoints = ((IEndpointRouteBuilder)host.Application).DataSources.SelectMany(static source => source.Endpoints).OfType<RouteEndpoint>().ToList();
        endpoints.Single(static endpoint => endpoint.RoutePattern.RawText == "/form").Metadata.GetMetadata<IAntiforgeryMetadata>()!
            .RequiresValidation.Should().BeTrue();
        endpoints.Single(static endpoint => endpoint.RoutePattern.RawText == "/query").Metadata.GetMetadata<IAntiforgeryMetadata>()
            .Should().BeNull();
    }

    /// <summary>
    /// Each read takes over the variable Wolverine's own factory created for its source and name, so that Wolverine's later
    /// lookups, for an <c>After</c> method, a middleware method's route value or a parameter attribute's identity, find the
    /// package's read rather than create a second one.
    /// </summary>
    [Fact]
    public async Task Wolverine_finds_the_package_s_read_when_it_looks_a_value_up_by_its_source_and_name()
    {
        await using var host = WolverineHttpHost.Map([typeof(WolverineSourceEndpoints), typeof(WolverineNullableRenamedEndpoint)]);
        Variable Read(string route) => host.Policy.ReadsOf(host.Chain(route)).Single().Variable;
        var header = typeof(WolverineSourceEndpoints).GetMethod(nameof(WolverineSourceEndpoints.Header))!.GetParameters().Single();

        host.Chain("/route-attribute/{qty}").FindRouteVariable(typeof(Quantity), "qty", out var route).Should().BeTrue();
        route.Should().BeSameAs(Read("/route-attribute/{qty}"));
        route!.Creator.Should().BeOfType<ValueObjectReadFrame>("Wolverine's own frame for the value is never written");
        host.Chain("/renamed/{N}").TryFindVariable("n", ValueSource.Anything, typeof(Quantity), out var renamed).Should().BeTrue();
        renamed.Should().BeSameAs(Read("/renamed/{N}"));
        renamed.Usage.Should().Be("n", "a route value's variable keeps the name Wolverine gave it, which it compares a parameter's name to");
        host.Chain("/query").TryFindOrCreateQuerystringValue(typeof(Quantity), "quantity", "q").Should().BeSameAs(Read("/query"));
        host.Chain("/header").GetOrCreateHeaderVariable(header.GetCustomAttribute<FromHeaderAttribute>()!, header).Should().BeSameAs(Read("/header"));
        host.Chain("/form").TryFindOrCreateFormValue(typeof(Quantity), "quantity", "q").Should().BeSameAs(Read("/form"));
        Read("/form").Usage.Should().Be("quantity", "Wolverine names a form value's variable after its key, which need not be an identifier");
        Read("/form-many").Creator.Should().BeOfType<ValueObjectReadFrame>();
        Read("/renamed-nullable/{n}").Should().NotBeAssignableTo<HttpElementVariable>("Wolverine creates no variable for that value, which keeps one of the package's own");
        Read("/renamed-nullable/{n}").Usage.Should().Be("quantity");
    }

    /// <summary>
    /// A policy registered after <c>UseValueObjects()</c> that adds a middleware method and matches its parameters gets a
    /// read of the package for its value object, which the refusal frame of the endpoint declares, an endpoint with no
    /// value object of its own getting one, so that its refusal is answered before that method runs.
    /// </summary>
    [Fact]
    public async Task A_value_object_of_a_middleware_method_a_later_policy_adds_is_read_before_the_answer()
    {
        await using var host = WolverineHttpHost.Map(typeof(WolverineLateEndpoints), static options =>
        {
            options.UseValueObjects();
            options.ConfigureEndpoints(static chain =>
            {
                var before = new MethodCall(typeof(WolverineLateMiddleware), nameof(WolverineLateMiddleware.Before));
                chain.Middleware.Add(before);
                chain.ApplyParameterMatching(before);
            });
        });

        foreach (var (route, reads) in new[] { ("/late", 2), ("/late-only", 1) })
        {
            var chain = host.Chain(route);
            chain.Middleware.OfType<ValueObjectRefusalFrame>().Should().ContainSingle();
            var answer = chain.Middleware[0].Should().BeOfType<ValueObjectRefusalFrame>().Subject;
            answer.FindVariables(null!).Should().Equal(host.Policy.ReadsOf(chain).Select(static read => read.Variable)).And.HaveCount(reads);
        }

        var code = await host.GenerateCodeAsync();
        foreach (var handler in new[] { "GET_late", "GET_late_only" })
        {
            var body = WolverineHttpCodeTests.Handler(code, handler);
            var answer = body.IndexOf("TryAnswerRefusalsAsync", StringComparison.Ordinal);
            body.IndexOf("Header(httpContext, \"X-Late\")", StringComparison.Ordinal).Should().BePositive().And.BeLessThan(answer);
            body.IndexOf("WolverineLateMiddleware.Before(late)", StringComparison.Ordinal).Should().BeGreaterThan(answer);
        }
    }

    /// <summary>
    /// A form value sets the request type, as Wolverine's own <c>[FromForm]</c> strategy does, so that Wolverine's JSON
    /// body strategy, which claims a class parameter of an endpoint whose request type nothing set, leaves the class
    /// parameter after it alone: claimed as a body, it would have the form request answered 415.
    /// </summary>
    [Fact]
    public async Task A_class_parameter_after_a_form_value_is_no_JSON_body()
    {
        await using var host = WolverineHttpHost.Map(typeof(WolverineFormPayloadEndpoint));

        var chain = host.Chain("/form-payload");
        chain.RequestType.Should().Be(typeof(void), "Wolverine's JSON body strategy would have made the class the request body");
        chain.HasRequestType.Should().BeFalse();
        chain.IsFormData.Should().BeTrue();
    }

    [Fact]
    public async Task Wolverine_warns_of_its_lenient_query_binding_once_the_package_describes_a_query_value_unless_it_is_strict()
    {
        await using (var lenient = WolverineHttpHost.Map(typeof(WolverineQueryOnlyEndpoint)))
        {
            lenient.Warnings.Should().ContainSingle(static warning => warning.Contains("RejectUnparseableQueryValues is false", StringComparison.Ordinal));
        }

        await using var strict = WolverineHttpHost.Map(typeof(WolverineQueryOnlyEndpoint), static options =>
        {
            options.RejectUnparseableQueryValues = true;
            options.UseValueObjects();
        });

        strict.Warnings.Should().NotContain(static warning => warning.Contains("RejectUnparseableQueryValues", StringComparison.Ordinal));
    }

    [Fact]
    public async Task One_name_read_from_two_sources_is_two_reads()
    {
        await using var host = WolverineHttpHost.Map(typeof(WolverineTwoSourcesEndpoint));

        host.Reads("/two-sources").Select(static read => (read.Source, read.Key)).Should().Equal(
            (ValueObjectHttpSource.Header, "q"),
            (ValueObjectHttpSource.Query, "q"));
    }

    /// <summary>
    /// Without reflection-based serialization, as in a native binary, HTTP JSON options that know neither the validation
    /// problem nor the <see cref="JsonElement"/> of its codes, or only the first, fail the mapping, naming
    /// <c>AddProblemDetails()</c>, rather than answer each refusal with a 500.
    /// </summary>
    [Fact]
    public void Json_options_that_cannot_write_the_validation_problem_fail_the_mapping()
    {
        foreach (var context in (JsonSerializerContext?[])[null, Web.ProblemOnlyJsonContext.Default])
        {
            var mapping = () => WolverineHttpHost.Map(typeof(WolverineCompoundEndpoint), services: services =>
                services.ConfigureHttpJsonOptions(json => json.SerializerOptions.TypeInfoResolver = context is null
                    ? JsonTypeInfoResolver.Combine()
                    : JsonTypeInfoResolver.Combine(context)));

            mapping.Should().Throw<InvalidOperationException>()
                .WithMessage("*no resolver of theirs knows HttpValidationProblemDetails or JsonElement*builder.Services.AddProblemDetails()*");
        }
    }

    [Fact]
    public async Task AddProblemDetails_lets_JSON_options_without_reflection_write_the_validation_problem()
    {
        await using var host = WolverineHttpHost.Map([typeof(WolverineCompoundEndpoint), typeof(WolverineQueryOnlyEndpoint)], services: static services =>
        {
            services.ConfigureHttpJsonOptions(static json => json.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine());
            services.AddProblemDetails();
        });

        host.Chain("/compound").Middleware[0].Should().BeOfType<ValueObjectRefusalFrame>();
        host.Chain("/query-only").Middleware[0].Should().BeOfType<ValueObjectRefusalFrame>();
    }

    [Fact]
    public void Services_without_HTTP_JSON_options_are_checked_against_their_defaults()
    {
        var chain = HttpChain.ChainFor(typeof(WolverineShapeEndpoints), nameof(WolverineShapeEndpoints.SingleValue));
        var policy = new ValueObjectRefusalPolicy();
        policy.ReadsOf(chain).Add(new ValueObjectReadFrame(
            Expected<Quantity, short>(ValueObjectHttpShape.Single, ValueObjectHttpSource.Query, "qty", required: true), typeof(Quantity), "qty"));
        using var provider = new ServiceCollection().BuildServiceProvider();

        policy.Apply([chain], new GenerationRules(), new ServiceContainer(new ServiceCollection(), provider));

        chain.Middleware[0].Should().BeOfType<ValueObjectRefusalFrame>();
    }

    [Theory]
    [InlineData(nameof(WolverineDeclinedParameters.Matrix))]
    [InlineData(nameof(WolverineDeclinedParameters.NullableElements))]
    [InlineData(nameof(WolverineDeclinedParameters.Collection))]
    [InlineData(nameof(WolverineDeclinedParameters.Dictionary))]
    [InlineData(nameof(WolverineDeclinedParameters.Primitive))]
    [InlineData(nameof(WolverineDeclinedParameters.Marker))]
    [InlineData(nameof(WolverineDeclinedParameters.Selfless))]
    [InlineData(nameof(WolverineDeclinedParameters.ClassBacked))]
    [InlineData(nameof(WolverineDeclinedParameters.Imposter))]
    [InlineData(nameof(WolverineDeclinedParameters.Open))]
    [InlineData(nameof(WolverineDeclinedParameters.AsParameters))]
    [InlineData(nameof(WolverineDeclinedParameters.Body))]
    [InlineData(nameof(WolverineDeclinedParameters.Service))]
    [InlineData(nameof(WolverineDeclinedParameters.KeyedService))]
    [InlineData(nameof(WolverineDeclinedParameters.NotBody))]
    [InlineData(nameof(WolverineDeclinedParameters.Loaded))]
    [InlineData(nameof(WolverineDeclinedParameters.RouteArray))]
    [InlineData(nameof(WolverineDeclinedParameters.HeaderArray))]
    [InlineData(nameof(WolverineDeclinedParameters.HeaderList))]
    [InlineData(nameof(WolverineDeclinedParameters.RouteSequence))]
    public async Task A_parameter_bound_otherwise_or_in_a_shape_Wolverine_reads_from_no_text_is_declined(string method)
    {
        await using var host = WolverineHttpHost.Map(typeof(WolverineSourceEndpoints));
        var chain = host.Chain("/route/{qty}");
        var parameter = typeof(WolverineDeclinedParameters).GetMethod(method)!.GetParameters().Single();

        ValueObjectHttpParameter.TryDescribe(chain, parameter, out var described).Should().BeFalse();
        described.Should().BeNull();
    }

    [Fact]
    public void Only_a_struct_implementing_the_contract_over_itself_has_a_value_type()
    {
        ValueObjectHttpParameter.TryGetValueType(typeof(Quantity), out var value).Should().BeTrue();
        value.Should().Be<short>();
        ValueObjectHttpParameter.TryGetValueType(typeof(WolverineImposter), out var imposter).Should().BeFalse("it implements the contract over HandWrittenCode");
        imposter.Should().BeNull();
        ValueObjectHttpParameter.TryGetValueType(typeof(Reference<>), out _).Should().BeFalse("an open generic type is nothing the code can name");
    }

    [Fact]
    public void The_options_are_required()
    {
        var bare = () => ValueObjectWolverineHttpOptionsExtensions.UseValueObjects(null!);
        var named = () => ValueObjectWolverineHttpOptionsExtensions.UseValueObjects(null!, static _ => { });
        var configure = () => new WolverineHttpOptions().UseValueObjects(null!);

        bare.Should().Throw<ArgumentNullException>().WithParameterName("options");
        named.Should().Throw<ArgumentNullException>().WithParameterName("options");
        configure.Should().Throw<ArgumentNullException>().WithParameterName("configure");
    }

    private static ValueObjectHttpParameter Read(WolverineHttpHost host, string route) => host.Reads(route).Single();

    private static ValueObjectHttpParameter Expected<TSelf, TValue>(ValueObjectHttpShape shape, ValueObjectHttpSource source, string key, bool required)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => new(typeof(TSelf), typeof(TValue), shape, source, key, required);
}
