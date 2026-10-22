namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The text family's part of GeneratedSurfaceTests: every sample held to Blazor, System.CommandLine and CsvHelper.

public partial class GeneratedSurfaceTests
{
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_binds_through_a_Blazor_input(string type)
        => Samples.All[type].BindsThroughABlazorInput();

    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_reads_through_a_command_line_option(string type)
        => Samples.All[type].ReadsThroughACommandLineOption();

    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_round_trips_through_a_CSV_column(string type)
        => Samples.All[type].RoundTripsThroughACsvColumn();
}
