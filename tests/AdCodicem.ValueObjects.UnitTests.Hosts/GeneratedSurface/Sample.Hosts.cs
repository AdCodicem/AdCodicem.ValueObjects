using AdCodicem.ValueObjects.UnitTests.Functions;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The hosts family's part of Sample (GeneratedSurface/Sample.cs): the check that holds every sample to Azure
// Functions.

public abstract partial class Sample
{
    /// <summary>
    /// Converts the value object's text through the Azure Functions converter, as the isolated worker hands it a route
    /// value, a query value or other binding data, and checks that it binds the value <c>TryParse</c> reads, nullable or
    /// not, and from the JSON string literal the host sends binding data of another type as, and that the refused text
    /// fails with the code and the message <c>TryParse</c> gives; then that the problem details middleware answers the
    /// refused text with the message and the code a minimal API answers it with.
    /// </summary>
    /// <returns>The check.</returns>
    public abstract Task BindsThroughTheFunctionsWorkerAsync();
}

public partial class Sample<TSelf, TValue>
{
    public override Task BindsThroughTheFunctionsWorkerAsync() => FunctionsParity.CheckAsync<TSelf, TValue>(Small, Text, Refused);
}
