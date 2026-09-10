using System.Globalization;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Identifiers;

namespace AdCodicem.ValueObjects.UnitTests.Identifiers;

public class EntityIdFormatTests
{
    private const string Prefix = "acc";

    [Theory]
    [InlineData(IdGranularity.Minute, 6, 23, 27)]
    [InlineData(IdGranularity.Hour, 4, 21, 25)]
    [InlineData(IdGranularity.Day, 3, 20, 24)]
    public void The_layout_is_fixed_width_per_granularity(
        IdGranularity granularity,
        int timestampLength,
        int bodyLength,
        int totalLength)
    {
        EntityIdFormat.TimestampLength(granularity).Should().Be(timestampLength);
        EntityIdFormat.BodyLength(granularity).Should().Be(bodyLength);
        EntityIdFormat.TotalLength(Prefix, granularity).Should().Be(totalLength);
    }

    [Fact]
    public void The_random_part_is_eighty_bits_at_every_granularity()
    {
        (EntityIdFormat.RandomLength * CrockfordBase32.BitsPerSymbol).Should().Be(80);
    }

    [Fact]
    public void Create_produces_something_it_accepts()
    {
        var id = EntityIdFormat.Create(Prefix, IdGranularity.Hour, TimeProvider.System, IdEntropySource.System);

        id.Should().StartWith("acc_").And.HaveLength(25);
        EntityIdFormat.Validate(id, Prefix, IdGranularity.Hour).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_is_reproducible_from_a_fixed_clock_and_a_fixed_entropy_source()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 9, 14, 30, 0, TimeSpan.Zero));

        var first = EntityIdFormat.Create(Prefix, IdGranularity.Hour, clock, new DeterministicEntropy());
        var second = EntityIdFormat.Create(Prefix, IdGranularity.Hour, clock, new DeterministicEntropy());

        second.Should().Be(first);
    }

    /// <summary>
    /// The reason the bucket is written big-endian into an ascending alphabet: the index orders itself.
    /// </summary>
    [Fact]
    public void Ordinal_order_reproduces_chronological_order()
    {
        var entropy = new DeterministicEntropy();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var identifiers = Enumerable
            .Range(0, 200)
            .Select(hours => EntityIdFormat.Create(
                Prefix,
                IdGranularity.Hour,
                new FakeTimeProvider(start.AddHours(hours * 13)),
                entropy))
            .ToList();

        identifiers.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public void Identifiers_within_one_bucket_carry_no_order_of_their_own()
    {
        // The bucket is the only monotonic part. Two identifiers minted in the same hour sort by their random
        // bodies, which is what stops a holder of a handful of them from counting anything.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 5, 5, 8, 0, 0, TimeSpan.Zero));

        var early = EntityIdFormat.Create(Prefix, IdGranularity.Hour, clock, new DeterministicEntropy(200));
        var late = EntityIdFormat.Create(Prefix, IdGranularity.Hour, clock, new DeterministicEntropy(1));

        string.CompareOrdinal(early, late).Should().BePositive("the later identifier drew the smaller body");
    }

    [Fact]
    public void A_clock_before_the_epoch_is_clamped_rather_than_wrapped()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(1999, 12, 31, 0, 0, 0, TimeSpan.Zero));

        var id = EntityIdFormat.Create(Prefix, IdGranularity.Hour, clock, new DeterministicEntropy());

        id.Substring(4, 4).Should().Be("0000");
        EntityIdFormat.Validate(id, Prefix, IdGranularity.Hour).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_clock_past_the_horizon_is_clamped_rather_than_wrapped()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(9999, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var id = EntityIdFormat.Create(Prefix, IdGranularity.Hour, clock, new DeterministicEntropy());

        id.Substring(4, 4).Should().Be("ZZZZ");
    }

    [Theory]
    [InlineData(IdGranularity.Minute, "2020-01-01T00:00:00Z", "000000")]
    [InlineData(IdGranularity.Minute, "2020-01-01T00:01:00Z", "000001")]
    [InlineData(IdGranularity.Hour, "2020-01-01T00:59:59Z", "0000")]
    [InlineData(IdGranularity.Hour, "2020-01-01T01:00:00Z", "0001")]
    [InlineData(IdGranularity.Day, "2020-01-02T23:59:59Z", "001")]
    public void The_bucket_counts_from_the_epoch(IdGranularity granularity, string instant, string expected)
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture));

        var id = EntityIdFormat.Create(Prefix, granularity, clock, new DeterministicEntropy());

        id.Substring(4, expected.Length).Should().Be(expected);
    }

    [Theory]
    [InlineData("  acc_2K7X9WQMZ4H3N8VYB6TCR  ")]
    [InlineData("ACC_2K7X9WQMZ4H3N8VYB6TCR")]
    [InlineData("acc_2k7x9wqmz4h3n8vyb6tcr")]
    [InlineData("acc_2K7X9-WQMZ4-H3N8V-YB6TC-R")]
    public void Normalize_folds_every_spelling_of_the_same_identifier(string input)
    {
        EntityIdFormat.Normalize(input, Prefix).Should().Be("acc_2K7X9WQMZ4H3N8VYB6TCR");
    }

    [Theory]
    [InlineData('i', '1')]
    [InlineData('I', '1')]
    [InlineData('l', '1')]
    [InlineData('L', '1')]
    [InlineData('o', '0')]
    [InlineData('O', '0')]
    public void Normalize_folds_the_Crockford_aliases(char alias, char canonical)
    {
        var body = EntityIdFormat.BodyLength(IdGranularity.Hour);

        var normalized = EntityIdFormat.Normalize($"acc_{new string(alias, body)}", Prefix);

        normalized.Should().Be($"acc_{new string(canonical, body)}");
    }

    [Fact]
    public void Normalize_settles_after_one_pass()
    {
        const string Input = "  ACC_2k7x9-wqmz4-h3n8v-yb6tc-r ";

        var once = EntityIdFormat.Normalize(Input, Prefix);

        EntityIdFormat.Normalize(once, Prefix).Should().Be(once);
    }

    [Fact]
    public void Normalize_hands_back_a_text_it_cannot_make_sense_of_rather_than_rejecting_it()
    {
        // Normalization never rejects: Validate is the member that says why.
        EntityIdFormat.Normalize("  cus_whatever  ", Prefix).Should().Be("cus_whatever");
    }

    [Fact]
    public void Validate_accepts_what_Create_produced()
    {
        var id = EntityIdFormat.Create(Prefix, IdGranularity.Day, TimeProvider.System, IdEntropySource.System);

        EntityIdFormat.Validate(id, Prefix, IdGranularity.Day).Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void Validate_reports_a_wrong_length()
    {
        var result = EntityIdFormat.Validate("acc_2K7X9", Prefix, IdGranularity.Hour);

        result.ErrorCode.Should().Be(IdentifierErrorCodes.InvalidLength);
    }

    [Fact]
    public void Validate_reports_a_foreign_prefix()
    {
        var foreign = EntityIdFormat.Create("cus", IdGranularity.Hour, TimeProvider.System, IdEntropySource.System);

        var result = EntityIdFormat.Validate(foreign, Prefix, IdGranularity.Hour);

        result.ErrorCode.Should().Be(IdentifierErrorCodes.InvalidPrefix);
    }

    [Fact]
    public void Validate_reports_a_character_outside_the_alphabet()
    {
        var id = EntityIdFormat.Create(Prefix, IdGranularity.Hour, TimeProvider.System, IdEntropySource.System);
        var corrupted = string.Concat(id.AsSpan(0, 4), "U", id.AsSpan(5));

        var result = EntityIdFormat.Validate(corrupted, Prefix, IdGranularity.Hour);

        result.ErrorCode.Should().Be(IdentifierErrorCodes.InvalidCharacter);
    }

    [Fact]
    public void Validate_reports_a_broken_check_character()
    {
        var id = EntityIdFormat.Create(Prefix, IdGranularity.Hour, TimeProvider.System, IdEntropySource.System);
        var replacement = id[^1] == 'Z' ? 'Y' : 'Z';
        var corrupted = string.Concat(id.AsSpan(0, id.Length - 1), replacement.ToString());

        var result = EntityIdFormat.Validate(corrupted, Prefix, IdGranularity.Hour);

        result.ErrorCode.Should().Be(IdentifierErrorCodes.InvalidChecksum);
    }

    /// <summary>
    /// The whole point of the prefix: a body lifted from one identifier type cannot be passed off as another,
    /// even though the length and the alphabet match exactly.
    /// </summary>
    [Fact]
    public void A_body_moved_between_prefixes_fails_its_check_character()
    {
        var customer = EntityIdFormat.Create("cus", IdGranularity.Hour, TimeProvider.System, IdEntropySource.System);
        var disguised = string.Concat("acc_", customer.AsSpan(4));

        var result = EntityIdFormat.Validate(disguised, Prefix, IdGranularity.Hour);

        result.ErrorCode.Should().Be(IdentifierErrorCodes.InvalidChecksum);
    }

    [Fact]
    public void SchemaPattern_describes_what_Validate_accepts()
    {
        var pattern = EntityIdFormat.SchemaPattern(Prefix, IdGranularity.Hour);
        var id = EntityIdFormat.Create(Prefix, IdGranularity.Hour, TimeProvider.System, IdEntropySource.System);

        Regex.IsMatch(id, pattern).Should().BeTrue();
        Regex.IsMatch("acc_toolong", pattern).Should().BeFalse();
    }

    [Fact]
    public void Create_refuses_a_malformed_prefix()
    {
        var act = () => EntityIdFormat.Create("Acc", IdGranularity.Hour, TimeProvider.System, IdEntropySource.System);

        act.Should().Throw<ArgumentException>();
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FakeTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
