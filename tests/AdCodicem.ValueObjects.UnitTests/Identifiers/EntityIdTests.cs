using System.Globalization;
using System.Text.Json;
using AdCodicem.ValueObjects.Identifiers;

namespace AdCodicem.ValueObjects.UnitTests.Identifiers;

/// <summary>
/// The generated identifier types, exercised through the surface a consumer actually uses.
/// </summary>
public class EntityIdTests
{
    [Fact]
    public void New_mints_something_of_the_declared_shape()
    {
        var id = AccountId.New();

        id.Value.Should().StartWith("acc_").And.HaveLength(AccountId.Length);
        AccountId.Length.Should().Be(25);
        AccountId.Prefix.Should().Be("acc");
        AccountId.Granularity.Should().Be(IdGranularity.Hour);
    }

    [Fact]
    public void New_mints_a_different_identifier_every_time()
    {
        var minted = Enumerable.Range(0, 1_000).Select(_ => AccountId.New()).ToHashSet();

        minted.Should().HaveCount(1_000);
    }

    [Fact]
    public void New_reads_the_ambient_sources()
    {
        var clock = new StoppedClock(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));

        using (ValueObjectIds.Use(clock, new DeterministicEntropy(9)))
        {
            AccountId.New().Should().Be(AccountId.New(clock, new DeterministicEntropy(9)));
        }
    }

    [Fact]
    public void The_granularity_widens_or_narrows_the_identifier()
    {
        EventId.Length.Should().Be(27, "a minute bucket costs two more characters than an hour one");
        LedgerEntryId.Length.Should().Be(30, "'ldg_entry' is nine characters and a day bucket is three");
        LedgerEntryId.Granularity.Should().Be(IdGranularity.Day);
    }

    [Fact]
    public void Identifiers_sort_chronologically()
    {
        var entropy = new DeterministicEntropy();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var ordered = Enumerable
            .Range(0, 50)
            .Select(hours => AccountId.New(new StoppedClock(start.AddHours(hours * 7)), entropy))
            .ToList();

        ordered.Should().BeInAscendingOrder();
    }

    [Theory]
    [InlineData("  {0}  ")]
    [InlineData("{0}")]
    public void Create_accepts_the_canonical_spelling_and_the_sloppy_ones(string shape)
    {
        var canonical = AccountId.New().Value;

        AccountId.Create(string.Format(CultureInfo.InvariantCulture, shape, canonical)).Value.Should().Be(canonical);
        AccountId.Create(canonical.ToUpperInvariant()).Value.Should().Be(canonical);
        AccountId.Create(canonical.ToLowerInvariant()).Value.Should().Be(canonical);
    }

    [Fact]
    public void Create_drops_the_hyphens_Crockford_allows_for_readability()
    {
        var canonical = AccountId.New().Value;
        var grouped = string.Join('-', Chunk(canonical[4..], 5));

        AccountId.Create($"acc_{grouped}").Value.Should().Be(canonical);
    }

    [Fact]
    public void Parsing_and_formatting_round_trip()
    {
        var id = AccountId.New();

        AccountId.Parse(id.ToString()).Should().Be(id);
        AccountId.TryParse(id.Value, out var parsed).Should().BeTrue();
        parsed.Should().Be(id);
    }

    [Fact]
    public void Serializing_to_JSON_produces_the_bare_string()
    {
        var id = AccountId.New();

        var json = JsonSerializer.Serialize(id);

        json.Should().Be($"\"{id.Value}\"");
        JsonSerializer.Deserialize<AccountId>(json).Should().Be(id);
    }

    /// <summary>
    /// The reason the prefix is part of the value: swapping one identifier for another in a request parameter
    /// has to fail at the boundary, not in a repository.
    /// </summary>
    [Fact]
    public void An_identifier_of_another_type_is_refused_with_its_reason()
    {
        var subscription = SubscriptionId.New();

        AccountId.TryParse(subscription.Value, null, out _, out var validation).Should().BeFalse();
        validation.ErrorCode.Should().Be(IdentifierErrorCodes.InvalidPrefix);
    }

    [Fact]
    public void A_body_relabelled_with_this_prefix_is_refused_by_the_check_character()
    {
        var disguised = string.Concat("acc_", SubscriptionId.New().Value.AsSpan(4));

        AccountId.TryCreate(disguised, out _, out var validation).Should().BeFalse();
        validation.ErrorCode.Should().Be(IdentifierErrorCodes.InvalidChecksum);
    }

    [Theory]
    [InlineData("", ValueObjectErrorCodes.Required)]
    [InlineData("acc_", IdentifierErrorCodes.InvalidLength)]
    [InlineData("acc_2K7X9WQMZ4H3N8VYB6TC", IdentifierErrorCodes.InvalidLength)]
    public void A_rejection_carries_the_rule_that_fired(string candidate, string expected)
    {
        AccountId.TryCreate(candidate, out _, out var validation).Should().BeFalse();

        validation.ErrorCode.Should().Be(expected);
    }

    [Fact]
    public void A_character_outside_the_alphabet_is_named_as_such()
    {
        var corrupted = string.Concat("acc_U", AccountId.New().Value.AsSpan(5));

        AccountId.TryCreate(corrupted, out _, out var validation).Should().BeFalse();
        validation.ErrorCode.Should().Be(IdentifierErrorCodes.InvalidCharacter);
    }

    [Fact]
    public void Create_throws_carrying_the_same_reason()
    {
        var act = () => AccountId.Create("acc_2K7X9WQMZ4H3N8VYB6TC");

        act.Should().Throw<ValueObjectException>()
            .Which.ErrorCode.Should().Be(IdentifierErrorCodes.InvalidLength);
    }

    /// <summary>
    /// The declarative rules an identifier never wrote by hand still reach the schema and the column, which is
    /// what keeps the OpenAPI document and the database in step with the format.
    /// </summary>
    [Fact]
    public void The_schema_is_derived_from_the_profile()
    {
        AccountId.Schema.MinLength.Should().Be(AccountId.Length);
        AccountId.Schema.MaxLength.Should().Be(AccountId.Length);
        AccountId.Schema.Description.Should().Be("The public identifier of an account.");
        AccountId.Schema.Pattern.Should().Be("^acc_[0123456789ABCDEFGHJKMNPQRSTVWXYZ]{21}$");
    }

    [Fact]
    public void The_schema_example_is_valid_and_stable()
    {
        var example = AccountId.Schema.Example;

        example.Should().NotBeNull();
        AccountId.TryCreate(example!, out _).Should().BeTrue();
        AccountId.Schema.Example.Should().Be(example, "a churning example would make a committed document noisy");
    }

    [Fact]
    public void A_declared_description_wins_over_the_summary()
    {
        LedgerEntryId.Schema.Description.Should().Be("Identifies one line of the ledger.");
    }

    [Fact]
    public void Equality_and_hashing_follow_the_value()
    {
        var id = AccountId.New();
        var same = AccountId.Create(id.Value.ToLowerInvariant());

        same.Should().Be(id);
        same.GetHashCode().Should().Be(id.GetHashCode());
        (same == id).Should().BeTrue();
    }

    private static IEnumerable<string> Chunk(string text, int size)
    {
        for (var start = 0; start < text.Length; start += size)
        {
            yield return text.Substring(start, Math.Min(size, text.Length - start));
        }
    }

    private sealed class StoppedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public StoppedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
