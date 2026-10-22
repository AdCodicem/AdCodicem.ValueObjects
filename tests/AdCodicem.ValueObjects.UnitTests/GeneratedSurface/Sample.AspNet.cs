using AdCodicem.ValueObjects.UnitTests.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The aspnet family's part of Sample (GeneratedSurface/Sample.cs): the checks that hold every sample to minimal APIs.

public abstract partial class Sample
{
    /// <summary>Maps a minimal API endpoint binding the value object from the query string, under its name.</summary>
    /// <param name="routes">Where the endpoint is mapped.</param>
    public abstract void MapMinimalApiEndpoint(IEndpointRouteBuilder routes);

    /// <summary>
    /// Sends the refused text to that endpoint, under <c>/samples</c>, and checks that the problem details carry the rule
    /// that refused it.
    /// </summary>
    /// <param name="client">A client of the application that maps it.</param>
    /// <returns>The check.</returns>
    public abstract Task AnswersARefusedMinimalApiValueWithItsRuleAsync(HttpClient client);
}

public partial class Sample<TSelf, TValue>
{
    public override void MapMinimalApiEndpoint(IEndpointRouteBuilder routes)
        => routes.MapGet($"/{this}", static (TSelf value) => value.ToString());

    public override async Task AnswersARefusedMinimalApiValueWithItsRuleAsync(HttpClient client)
    {
        using var response = await client.GetAsync(
            $"/samples/{this}?value={Uri.EscapeDataString(Refused)}", TestContext.Current.CancellationToken);
        var problem = await ProblemReading.ReadProblemAsync(response);

        var (message, code) = ProblemReading.Refusal<TSelf, TValue>(Refused);
        problem.Messages.Should().ContainKey("value").WhoseValue.Should().Equal(message);
        problem.Codes.Should().Equal(new Dictionary<string, string> { ["value"] = code });
    }
}
