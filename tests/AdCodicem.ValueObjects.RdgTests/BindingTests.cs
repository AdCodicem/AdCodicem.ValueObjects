using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AdCodicem.ValueObjects.RdgTests;

/// <summary>
/// A value object declared in the project that maps the endpoints, bound by the code the Request Delegate Generator
/// writes: from a route value, the query string and a header, refused with a 400, and bound as <see langword="null"/>
/// when an optional one is absent.
/// </summary>
public sealed partial class BindingTests(RdgApplication application) : IClassFixture<RdgApplication>
{
    private static readonly string Customer = CustomerId.New().Value;

    [Theory]
    [InlineData("/skus/AB-12", "AB-12")]
    [InlineData("/quantities/7", "7")]
    [InlineData("/codes/X1", "X1")]
    [InlineData("/shelves/A3", "A3")]
    public async Task A_route_value_binds(string path, string expected)
    {
        (await GetAsync(path)).Should().Be((HttpStatusCode.OK, expected));
    }

    [Fact]
    public async Task A_query_value_binds()
    {
        (await GetAsync($"/customers?id={Customer}")).Should().Be((HttpStatusCode.OK, Customer));
    }

    [Fact]
    public async Task A_header_value_binds()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/current") { Headers = { { "X-Customer", Customer } } };

        (await SendAsync(request)).Should().Be((HttpStatusCode.OK, Customer));
    }

    [Theory]
    [InlineData("/skus/AB12")]
    [InlineData("/skus/AB-1234567890")]
    [InlineData("/quantities/0")]
    [InlineData("/quantities/101")]
    [InlineData("/quantities/seven")]
    [InlineData("/codes/X12345")]
    [InlineData("/shelves/A")]
    [InlineData("/customers?id=cus_0")]
    [InlineData("/customers")]
    [InlineData("/search?sku=AB12")]
    public async Task A_refused_value_answers_400(string path)
    {
        (await GetAsync(path)).Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("cus_0")]
    [InlineData(null)]
    public async Task A_refused_header_answers_400(string? header)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/current");
        if (header is not null)
        {
            request.Headers.Add("X-Customer", header);
        }

        (await SendAsync(request)).Status.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The RDG binds empty text as absent, where the reflection-based binding answers 400, as it does for an
    /// <see cref="int"/>? or a <see cref="Guid"/>?.
    /// </summary>
    [Theory]
    [InlineData("/search", "(none)")]
    [InlineData("/search?sku=", "(none)")]
    [InlineData("/search?sku=AB-12", "AB-12")]
    public async Task An_optional_value_binds_null_when_it_is_absent(string path, string expected)
    {
        (await GetAsync(path)).Should().Be((HttpStatusCode.OK, expected));
    }

    /// <summary>
    /// The suite tests the RDG only while the RDG writes the binding: were it off, the same requests would pass through
    /// the reflection-based binding, which the contracts listed on the declarations satisfy as well. And it binds each
    /// parameter through <c>TryParse</c>, never from the body, which is what it does for a value object it cannot see as
    /// parsable; the two bodies the problem details endpoints read (<see cref="ProblemEndpoints"/>) excepted.
    /// </summary>
    [Fact]
    public void The_Request_Delegate_Generator_binds_every_parameter_through_TryParse()
    {
        var generated = typeof(BindingTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "CompilerGeneratedFilesOutputPath").Value!;
        var file = Path.Combine(
            generated,
            "Microsoft.AspNetCore.Http.RequestDelegateGenerator",
            "Microsoft.AspNetCore.Http.RequestDelegateGenerator.RequestDelegateGenerator",
            "GeneratedRouteBuilderExtensions.g.cs");
        File.Exists(file).Should().BeTrue($"the RDG writes {file} when it runs");

        var bindings = ParameterBinding().Matches(File.ReadAllText(file))
            .Select(match => (match.Groups["name"].Value, Parsed: match.Groups["parsed"].Value == "true"));

        bindings.Should().BeEquivalentTo(
        [
            ("sku", true), ("quantity", true), ("code", true), ("shelf", true), ("id", true), ("sku", true), ("customer", true),
            ("item", true), ("amount", true), ("product", true), ("count", true), ("units", true), ("owner", true),
            ("counts", true), ("warehouse", true), ("place", true), ("id", true), ("size", true), ("order", false), ("parcel", false),
        ]);
    }

    private async Task<(HttpStatusCode Status, string Body)> GetAsync(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        return await SendAsync(request);
    }

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(HttpRequestMessage request)
    {
        using var response = await application.Client.SendAsync(request, TestContext.Current.CancellationToken);

        return (response.StatusCode, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Gets the metadata the RDG writes for each parameter it binds, with whether it binds it through <c>TryParse</c>.</summary>
    [GeneratedRegex(@"new ParameterBindingMetadata\(""(?<name>\w+)"", parameters\[\d+\], hasTryParse: (?<parsed>true|false)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ParameterBinding();
}
