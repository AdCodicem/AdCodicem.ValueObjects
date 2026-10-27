using AdCodicem.ValueObjects.UnitTests.Forms;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The text family's part of Sample (GeneratedSurface/Sample.cs): the check that holds every sample to Blazor.

public abstract partial class Sample
{
    /// <summary>
    /// Renders the value object, alone and as its nullable type, in a statically rendered Blazor form, and checks that the
    /// text the input writes for the two instances and for every value at an edge of its rules reads back to the instance
    /// and binds it, and that the refused text and the text of every value its schema rules out leave the code and the
    /// message <c>TryParse</c> gives.
    /// </summary>
    public abstract void BindsThroughABlazorInput();
}

public partial class Sample<TSelf, TValue>
{
    public override void BindsThroughABlazorInput() => FormsParity.Check<TSelf, TValue>(Small, Large, Refused);
}
