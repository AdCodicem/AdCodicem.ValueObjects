using AdCodicem.ValueObjects.UnitTests.Cli;
using AdCodicem.ValueObjects.UnitTests.Csv;
using AdCodicem.ValueObjects.UnitTests.Forms;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The text family's part of Sample (GeneratedSurface/Sample.cs): the checks that hold every sample to Blazor,
// System.CommandLine and CsvHelper.

public abstract partial class Sample
{
    /// <summary>
    /// Renders the value object, alone and as its nullable type, in a statically rendered Blazor form, and checks that the
    /// text the input writes for the two instances and for every value at an edge of its rules reads back to the instance
    /// and binds it, and that the refused text and the text of every value its schema rules out leave the code and the
    /// message <c>TryParse</c> gives.
    /// </summary>
    public abstract void BindsThroughABlazorInput();

    /// <summary>
    /// Reads the value object through System.CommandLine options of it, of its nullable type and of a list of it, and an
    /// argument, configured by <c>AcceptValueObjects()</c>, and checks that the text of the two instances and of every
    /// value at an edge of its rules reads as the instance <c>TryParse</c> gives, that the refused text, empty text and the
    /// text of every value its schema rules out leave the code and the message <c>TryParse</c> gives on the parse error,
    /// and that each known value is completed as text that reads back to it.
    /// </summary>
    public abstract void ReadsThroughACommandLineOption();

    /// <summary>
    /// Writes the value object, alone and as its nullable type, as a CsvHelper column through <c>AddValueObjects()</c>, and
    /// checks that the two instances and every value at an edge of its rules read back to themselves, in the invariant
    /// culture and in French, and alone through the converter in every culture whose calendar is not the Gregorian one;
    /// that the refused text, empty text and the text of every value its schema rules out leave a
    /// <c>TypeConverterException</c> with the code and the message <c>TryParse</c> gives, or
    /// <see cref="ValueObjectErrorCodes.Required"/> for empty text; and that its default is written only where its rules
    /// accept it.
    /// </summary>
    public abstract void RoundTripsThroughACsvColumn();
}

public partial class Sample<TSelf, TValue>
{
    public override void BindsThroughABlazorInput() => FormsParity.Check<TSelf, TValue>(Small, Large, Refused);

    public override void ReadsThroughACommandLineOption() => CliParity.Check<TSelf, TValue>(Small, Large, Refused);

    public override void RoundTripsThroughACsvColumn() => CsvParity.Check<TSelf, TValue>(Small, Large, Refused);
}
