using System.Text;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.UnitTests.Web;
using AdCodicem.ValueObjects.Wolverine.Http;
using AdCodicem.ValueObjects.Wolverine.Http.Binding;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Http;

namespace AdCodicem.ValueObjects.UnitTests.WolverineHttp;

/// <summary>
/// Requests the helpers the generated code calls are run on, outside Wolverine: a request whose services hold what
/// <c>AddWolverineHttp()</c> and <c>UseValueObjects()</c> leave there, its response written to memory.
/// </summary>
internal static class WolverineHttpRequests
{
    /// <summary>A request whose services hold Wolverine's options with the package's policy, naming the messages given.</summary>
    /// <param name="messages">The messages <c>UseValueObjects</c> names, or <see langword="null"/> for the English ones.</param>
    /// <param name="configure">Registers more services: localization, problem details, JSON options.</param>
    /// <returns>The request.</returns>
    public static DefaultHttpContext Request(ValueObjectMessageOptions? messages = null, Action<IServiceCollection>? configure = null)
        => Bare(services =>
        {
            var options = new WolverineHttpOptions();
            options.Policies.Add(new ValueObjectRefusalPolicy { Messages = messages });
            services.AddSingleton(options);
            configure?.Invoke(services);
        });

    /// <summary>A request whose services hold what is given alone, beside options and logging.</summary>
    /// <param name="configure">Registers the services.</param>
    /// <returns>The request.</returns>
    public static DefaultHttpContext Bare(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection().AddOptions().AddLogging();
        configure?.Invoke(services);

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };
    }

    /// <summary>Gives a request a form body.</summary>
    /// <param name="request">The request.</param>
    /// <param name="body">The form, URL-encoded.</param>
    /// <returns>The request.</returns>
    public static DefaultHttpContext WithForm(this DefaultHttpContext request, string body)
    {
        request.Request.ContentType = "application/x-www-form-urlencoded";
        request.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return request;
    }

    /// <summary>Answers the refusals of a request, and reads the problem written.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The problem, read as the messages and the codes of each member.</returns>
    public static async Task<ReadProblem> AnswerAsync(this DefaultHttpContext request)
    {
        (await ValueObjectHttpBinding.TryAnswerRefusalsAsync(request)).Should().BeTrue("the request was refused");

        return ProblemReading.ProblemOf(request.Response.StatusCode, await BodyAsync(request));
    }

    /// <summary>Reads what a request's response holds.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The body.</returns>
    public static async Task<string> BodyAsync(this HttpContext request)
    {
        request.Response.Body.Position = 0;
        using var reader = new StreamReader(request.Response.Body, leaveOpen: true);

        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
