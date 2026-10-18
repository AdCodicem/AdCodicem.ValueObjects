using AdCodicem.ValueObjects.Wolverine.Http;
using AdCodicem.ValueObjects.Wolverine.Http.Binding;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AdCodicem.ValueObjects.UnitTests.WolverineHttp;

/// <summary>
/// The code Wolverine writes for an endpoint once the package claims its value objects, as <c>codegen write</c> writes it
/// for static mode: one statement per value object, the refusals answered before anything else runs, and code that
/// compiles for every family of value object.
/// </summary>
[Collection(nameof(WolverineHttpHosts))]
public sealed class WolverineHttpCodeTests
{
    private const string Binding = "global::AdCodicem.ValueObjects.Wolverine.Http.ValueObjectHttpBinding";

    [Fact]
    public async Task Each_value_object_is_one_statement_of_the_helpers_closed_over_it_and_the_refusals_are_answered_before_the_handler()
    {
        await using var host = WolverineHttpHost.Map([typeof(WolverineShapeEndpoints), typeof(WolverineSourceEndpoints), typeof(WolverineCompoundEndpoint)]);

        var code = await host.GenerateCodeAsync();

        const string Quantity = "<AdCodicem.ValueObjects.UnitTests.Domain.Quantity, System.Int16>";
        code.Should().Contain($"var qty = {Binding}.Bind{Quantity}(httpContext, \"qty\", {Binding}.Query(httpContext, \"qty\"), required: true);")
            .And.Contain($"var qty = {Binding}.BindNullable{Quantity}(httpContext, \"qty\", {Binding}.Query(httpContext, \"qty\"));")
            .And.Contain($"var qty = {Binding}.BindArray{Quantity}(httpContext, \"qty\", {Binding}.QueryValues(httpContext, \"qty\"));")
            .And.Contain($"var qty = new global::System.Collections.Generic.List<AdCodicem.ValueObjects.UnitTests.Domain.Quantity>({Binding}.BindArray{Quantity}(httpContext, \"qty\", {Binding}.QueryValues(httpContext, \"qty\")));")
            .And.Contain($"var qty = {Binding}.Bind{Quantity}(httpContext, \"qty\", {Binding}.Query(httpContext, \"qty\"), required: false);")
            .And.Contain($"var quantity = {Binding}.Bind{Quantity}(httpContext, \"q\", await {Binding}.FormAsync(httpContext, \"q\").ConfigureAwait(false), required: true);")
            .And.Contain($"var qty = {Binding}.BindArray{Quantity}(httpContext, \"qty\", await {Binding}.FormValuesAsync(httpContext, \"qty\").ConfigureAwait(false));")
            .And.Contain($"var n = {Binding}.Bind{Quantity}(httpContext, \"N\", {Binding}.Route(httpContext, \"N\"), required: true);")
            .And.Contain($"var quantity = {Binding}.Bind{Quantity}(httpContext, \"X-Quantity\", {Binding}.Header(httpContext, \"X-Quantity\"), required: true);")
            .And.Contain("// Value object header value \"X-Quantity\", read through the rules of AdCodicem.ValueObjects.UnitTests.Domain.Quantity");

        var compound = Handler(code, "GET_compound");
        var answer = compound.IndexOf($"if (await {Binding}.TryAnswerRefusalsAsync(httpContext).ConfigureAwait(false)) return;", StringComparison.Ordinal);
        answer.Should().BePositive();
        compound.IndexOf($"{Binding}.Bind", StringComparison.Ordinal).Should().BeLessThan(answer, "every read comes before the answer");
        compound.IndexOf("WolverineCompoundEndpoint.Before(qty)", StringComparison.Ordinal).Should().BeGreaterThan(answer, "a Before method never runs for a refused request");
        compound.Split($"{Binding}.Bind").Should().HaveCount(2, "the middleware and the handler share one read");
    }

    [Fact]
    public async Task The_code_written_for_every_family_of_value_object_compiles()
    {
        await using var host = WolverineHttpHost.Map(typeof(WolverineFamilyEndpoints));

        var code = await host.GenerateCodeAsync();

        code.Should().Contain("\"a\\\"b\\\\c\"", "a name holding a quote stays one literal");
        code.Should().Contain("Bind<AdCodicem.ValueObjects.UnitTests.Domain.Reference<AdCodicem.ValueObjects.UnitTests.WolverineHttp.WolverineOwner>, string>");
        Compile(code).Should().BeEmpty();
    }

    /// <summary>
    /// Wolverine looks a value up again by its source and name for an <c>After</c> method, a middleware type's
    /// <c>Finally</c> method, a middleware method whose parameters nothing matched, and a parameter attribute loading by
    /// it, as <c>[Entity]</c> does: each finds the package's read, so the handler holds that read alone, and its code
    /// compiles where a second local of the name was CS0128.
    /// </summary>
    [Fact]
    public async Task A_value_Wolverine_looks_up_again_by_its_name_is_the_package_s_read_alone_and_the_code_compiles()
    {
        await using var host = WolverineHttpHost.Map(
            [
                typeof(WolverineAfterRouteEndpoint), typeof(WolverineAfterHeaderEndpoint), typeof(WolverineAfterQueryEndpoint),
                typeof(WolverineAfterRenamedEndpoint), typeof(WolverineFinallyRouteEndpoint), typeof(WolverineEntityEndpoints),
                typeof(WolverineUnmatchedRouteEndpoint),
            ],
            static options =>
            {
                options.UseValueObjects();
                options.AddMiddleware(typeof(WolverineFinallyMiddleware), static chain => chain.RoutePattern?.RawText == "/finally-route/{qty}");
                options.ConfigureEndpoints(static chain =>
                {
                    if (chain.RoutePattern?.RawText == "/unmatched-route/{qty}")
                    {
                        chain.Middleware.Add(new MethodCall(typeof(WolverineUnmatchedMiddleware), nameof(WolverineUnmatchedMiddleware.Before)));
                    }
                });
            });

        host.Graph.Chains.Should().OnlyContain(
            static chain => chain.Middleware[0] is ValueObjectRefusalFrame,
            "the answer comes before the try block of a middleware type's Finally method, which never runs for a refused request");

        var code = await host.GenerateCodeAsync();

        foreach (var (handler, call) in new[]
        {
            ("GET_after_route_qty", "WolverineAfterRouteEndpoint.After(qty)"),
            ("GET_after_header", "WolverineAfterHeaderEndpoint.After(quantity)"),
            ("GET_after_query", "WolverineAfterQueryEndpoint.After(quantity)"),
            ("GET_after_renamed_n", "WolverineAfterRenamedEndpoint.After(n)"),
            ("GET_finally_route_qty", "WolverineFinallyMiddleware.Finally(qty)"),
            ("GET_entity_route_id", "WolverineCustomerLookup.Find(id)"),
            ("GET_entity_query", "WolverineCustomerLookup.Find(id)"),
            ("GET_entity_first_id", "WolverineCustomerLookup.Find(id)"),
            ("GET_unmatched_route_qty", "WolverineUnmatchedMiddleware.Before(qty)"),
        })
        {
            var body = Handler(code, handler);
            body.Should().Contain(call);
            body.Split($"{Binding}.Bind").Should().HaveCount(2, "{0} reads its value once, through the package", handler);
            body.Should().NotContain("_rawValue", "Wolverine writes no read of its own in {0}", handler);
            body.IndexOf($"{Binding}.Bind", StringComparison.Ordinal).Should().BeLessThan(body.IndexOf(call, StringComparison.Ordinal));
        }

        Compile(code).Should().BeEmpty();
    }

    [Fact]
    public void A_frame_with_nothing_after_it_writes_its_own_statement_alone()
    {
        var read = new ValueObjectReadFrame(
            new(typeof(Quantity), typeof(short), ValueObjectHttpShape.Nullable, ValueObjectHttpSource.Header, "X-Quantity", Required: false),
            typeof(Quantity?),
            "quantity");
        var method = GeneratedMethod.ForNoArg<Task>("Handle");
        using var writer = new SourceWriter();

        read.GenerateCode(method, writer);
        new ValueObjectRefusalFrame([read]).GenerateCode(method, writer);

        writer.Code().Should()
            .Contain($"var quantity = {Binding}.BindNullable<AdCodicem.ValueObjects.UnitTests.Domain.Quantity, System.Int16>(httpContext, \"X-Quantity\", {Binding}.Header(httpContext, \"X-Quantity\"));")
            .And.Contain($"if (await {Binding}.TryAnswerRefusalsAsync(httpContext).ConfigureAwait(false)) return");
    }

    [Theory]
    [InlineData("qty", "\"qty\"")]
    [InlineData("a\"b\\c", "\"a\\\"b\\\\c\"")]
    [InlineData("tab\there", "\"tab\\u0009here\"")]
    [InlineData("line\nbreak", "\"line\\u000abreak\"")]
    [InlineData("😀", "\"\\ud83d\\ude00\"")]
    [InlineData("été", "\"été\"")]
    public void A_name_is_written_as_one_CSharp_string_literal(string name, string literal)
        => ValueObjectReadFrame.Literal(name).Should().Be(literal);

    /// <summary>Gets the code of one generated handler.</summary>
    /// <param name="code">The code Wolverine wrote.</param>
    /// <param name="name">The handler's name, <c>GET_compound</c> for <c>GET /compound</c>.</param>
    /// <returns>The handler's code.</returns>
    internal static string Handler(string code, string name)
    {
        var start = code.IndexOf($"// START: {name}", StringComparison.Ordinal);
        var end = code.IndexOf($"// END: {name}", start, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, "Wolverine writes the handler {0}", name);

        return code[start..end];
    }

    /// <summary>
    /// Compiles generated code against this process's assemblies, this one included, and gets its errors: each file as a
    /// file of its own, once, as <c>codegen write</c> writes them (the endpoint registry comes from two collections,
    /// whose second copy overwrites the first on disk).
    /// </summary>
    private static List<Diagnostic> Compile(string code)
    {
        var files = code.Split("// <auto-generated/>", StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .Select(static file => CSharpSyntaxTree.ParseText(file, cancellationToken: TestContext.Current.CancellationToken));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(WolverineHttpCodeTests).Assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "WolverineHandlers",
            files,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var assembly = new MemoryStream();

        var emitted = compilation.Emit(assembly, cancellationToken: TestContext.Current.CancellationToken);

        return [.. emitted.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
    }
}
