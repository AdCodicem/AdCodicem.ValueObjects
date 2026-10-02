namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// <see cref="ValueObjectBound"/>: the bridge to the bound hooks, and the one form a bound of each type is written in,
/// which the message of a rejected value quotes and the schema publishes.
/// </summary>
public sealed class ValueObjectBoundTests
{
    public static TheoryData<object, string> Bounds => new()
    {
        { 42, "42" },
        { -1.5m, "-1.5" },
        { 1e-5d, "1E-05" },
        { 0.1f, "0.1" },
        { 'A', "A" },
        { TimeSpan.FromMinutes(90), "01:30:00" },
        { new DateOnly(2000, 1, 1), "2000-01-01" },
        { new TimeOnly(6, 0), "06:00:00.0000000" },
        { new DateTime(2000, 1, 1, 8, 30, 0), "2000-01-01T08:30:00.0000000" },
        { new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(2)), "2000-01-01T00:00:00.0000000+02:00" },
        { true, "True" },
    };

    [Theory]
    [MemberData(nameof(Bounds))]
    public void A_bound_is_written_in_the_one_invariant_form_of_its_type(object bound, string text)
        => ValueObjectBound.Text(bound).Should().Be(text);

    /// <summary>
    /// The check compares clock readings, whatever their kind: a <c>Z</c> or the offset of the machine would publish a
    /// bound the check does not apply.
    /// </summary>
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void A_date_and_time_bound_is_written_without_its_kind(DateTimeKind kind)
        => ValueObjectBound.Text(new DateTime(2020, 1, 1, 0, 0, 0, kind)).Should().Be("2020-01-01T00:00:00.0000000");

    [Fact]
    public void A_missing_bound_is_written_as_nothing()
        => ValueObjectBound.Text<string?>(null).Should().BeEmpty();

    /// <summary>
    /// A hook quotes its bound in that form, which is not always the form a deprecated text option was written in.
    /// </summary>
    [Fact]
    public void A_hook_quotes_its_bound_in_that_form()
    {
        OpeningTime.Validate(new TimeOnly(5, 59)).ErrorMessage.Should()
            .Be("The value must be greater than or equal to 06:00:00.0000000.");
        RecordedAt.Validate(new DateTime(2100, 1, 1)).ErrorMessage.Should()
            .Be("The value must be less than or equal to 2099-12-31T00:00:00.0000000.");
    }

    [Fact]
    public void The_bridge_reads_the_hook_of_the_value_object()
    {
        ValueObjectBound.Minimum<Latitude, double>().Should().Be(-90);
        ValueObjectBound.Maximum<Tolerance, double>().Should().Be(1);
    }
}
