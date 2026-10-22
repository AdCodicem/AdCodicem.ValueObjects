using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// What an exception handler of a minimal API reads of a refusal, with <c>RouteHandlerOptions.ThrowOnBadRequest</c> on,
/// as it is in Development: the exception a body fails with is wrapped, and the code is read through the wrapper.
/// </summary>
public sealed class MinimalApiErrorCodeTests : IAsyncLifetime
{
    private WebApplication _application = null!;
    private HttpClient _client = null!;
    private Exception? _thrown;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.Configure<RouteHandlerOptions>(static options => options.ThrowOnBadRequest = true);

        _application = builder.Build();
        _application.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (BadHttpRequestException exception)
            {
                _thrown = exception;
                context.Response.StatusCode = exception.StatusCode;
            }
        });
        _application.MapPost("/orders", static (ProbeOrder order) => order.Reference.Value);
        _application.MapGet("/countries/{country}", static (CountryCode country) => country.Value);
        await _application.StartAsync(TestContext.Current.CancellationToken);
        _client = _application.GetTestClient();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
    }

    [Fact]
    public async Task The_code_of_a_value_a_body_refuses_is_read_through_the_exception_the_minimal_API_wraps_it_in()
    {
        using var response = await _client.PostAsJsonAsync("/orders", new { reference = "no" }, TestContext.Current.CancellationToken);

        ((int)response.StatusCode).Should().Be(StatusCodes.Status400BadRequest);
        _thrown.Should().BeOfType<BadHttpRequestException>().Which.InnerException.Should().BeAssignableTo<ValueObjectJsonException>();
        ValueObjectErrors.TryGetCode(_thrown!, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.TooShort);
    }

    /// <summary>
    /// A route or query value is bound through <c>TryParse</c>, which throws nothing: the exception the minimal API
    /// throws for it wraps none, and carries no code.
    /// </summary>
    [Fact]
    public async Task A_value_a_route_refuses_carries_no_code_to_read()
    {
        using var response = await _client.GetAsync("/countries/ZZ", TestContext.Current.CancellationToken);

        ((int)response.StatusCode).Should().Be(StatusCodes.Status400BadRequest);
        _thrown.Should().BeOfType<BadHttpRequestException>();
        ValueObjectErrors.TryGetCode(_thrown!, out _).Should().BeFalse();
    }
}
