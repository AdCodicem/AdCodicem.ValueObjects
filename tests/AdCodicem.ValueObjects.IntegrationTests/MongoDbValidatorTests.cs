using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using AdCodicem.ValueObjects.MongoDB;
using AdCodicem.ValueObjects.UnitTests.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Verifies, on a real MongoDB server, that the <c>$jsonSchema</c> validator built from the rules refuses a document
/// holding a value the type rejects, accepts every value the type accepts, and that the server reads each pattern the
/// validator publishes as .NET does; and that a document inserted without its entity identifier is given one.
/// </summary>
/// <remarks>
/// What each rule becomes is tested in the unit suite without a server. What only a server shows is how it applies
/// them: PCRE2 in place of .NET's engine, lengths in code points, numbers compared across BSON types.
/// </remarks>
public sealed class MongoDbValidatorTests(MongoDbFixture fixture) : IClassFixture<MongoDbFixture>
{
    private const string French = "FR7630006000011234567890189";

    private const string Emoji = "\uD83D\uDE00";

    private static readonly CustomerId Buyer = CustomerId.Create(Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff"));

    private IMongoDatabase Database => fixture.Database("validators");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_validator_built_from_the_rules_refuses_a_document_holding_a_value_the_type_refuses()
    {
        var orders = await CreateAsync<MongoOrder>("orders");
        var order = new MongoOrder
        {
            Iban = Iban.Create(French),
            Quantity = LineQuantity.Create(3),
            Customer = Buyer,
            Countries = [CountryCode.France, CountryCode.Germany],
            Totals = new() { [CountryCode.France] = Amount.Create(12.5m) },
            Status = DocumentStatus.Create("FINAL"),
            FirstLine = new MongoLine { Quantity = LineQuantity.Create(2), Price = Amount.Create(9.99m) },
            Lines = [new MongoLine { Quantity = LineQuantity.Create(1) }],
            Code = OwnedCode<MongoOrder>.Create("ORD-1"),
        };
        await orders.InsertOneAsync(order, cancellationToken: Cancellation);

        var valid = order.ToBsonDocument();
        valid.Remove("_id");

        await RefusedAsync(valid, document => document.Set("Iban", French.ToLowerInvariant()));
        await RefusedAsync(valid, document => document.Set("Iban", "FR76"));
        await RefusedAsync(valid, document => document.Remove("Iban"));
        await RefusedAsync(valid, document => document.Set("Quantity", 1000));
        await RefusedAsync(valid, document => document.Set("Quantity", 0));
        await RefusedAsync(valid, document => document.Set("Quantity", 5L));
        await RefusedAsync(valid, document => document.Set("Customer", Buyer.Value.ToString()));
        await RefusedAsync(valid, document => document.Set("Countries", new BsonArray { "FR", "GB" }));
        await RefusedAsync(valid, document => document.Set("FirstLine", new BsonDocument("Quantity", 0)));
        await RefusedAsync(valid, document => document.Set("FirstLine", new BsonDocument { { "Quantity", 2 }, { "Price", new BsonDecimal128(-1m) } }));
        await RefusedAsync(valid, document => document.Set("Lines", new BsonArray { new BsonDocument("Quantity", 0) }));
        await RefusedAsync(valid, document => document.Set("Code", "ORD-1-TOO-LONG"));

        await AcceptedAsync(valid, document => document.Set("Backup", BsonNull.Value));
        await AcceptedAsync(valid, document => document.Remove("Backup"));
        await AcceptedAsync(valid, document => document.Set("Backup", "DE89370400440532013000"));
        await AcceptedAsync(valid, document => document.Set("FirstLine", BsonNull.Value));
        await AcceptedAsync(valid, document => document.Set("Status", "Draft"));
        await AcceptedAsync(valid, document => document.Set("Totals", new BsonDocument("GB", "anything")));

        // The server checks an update as it checks an insert, which catches what no serializer sees.
        var update = await FluentActions.Awaiting(() => orders.UpdateOneAsync(
                x => x.Code == OwnedCode<MongoOrder>.Create("ORD-1"),
                Builders<MongoOrder>.Update.Inc(x => x.Quantity.Value, (short)1000),
                cancellationToken: Cancellation))
            .Should().ThrowAsync<MongoWriteException>();
        update.Which.WriteError.Code.Should().Be(121);

        async Task RefusedAsync(BsonDocument document, Action<BsonDocument> change)
        {
            var copy = document.DeepClone().AsBsonDocument;
            change(copy);
            var refusal = await FluentActions.Awaiting(() => Database.GetCollection<BsonDocument>("orders").InsertOneAsync(copy, cancellationToken: Cancellation))
                .Should().ThrowAsync<MongoWriteException>();
            refusal.Which.WriteError.Code.Should().Be(121, "the server refuses {0}", copy.ToJson());
        }

        async Task AcceptedAsync(BsonDocument document, Action<BsonDocument> change)
        {
            var copy = document.DeepClone().AsBsonDocument;
            change(copy);
            await Database.GetCollection<BsonDocument>("orders").InsertOneAsync(copy, cancellationToken: Cancellation);
        }
    }

    /// <summary>
    /// A validator stricter than the type would refuse the application's own writes: every value each type accepts,
    /// its bounds and its longest text included, is stored under the validator of a document holding it.
    /// </summary>
    [Fact]
    public async Task Every_value_a_type_accepts_is_stored_under_the_validator_of_a_document_holding_it()
    {
        await AcceptedAsync(Consent.Create(true), Consent.Create(false));
        await AcceptedAsync(Grade.Create('A'), Grade.Create('F'));
        await AcceptedAsync(Adjustment.Create(-10), Adjustment.Create(10));
        await AcceptedAsync(Score.Create(0), Score.Create(100));
        await AcceptedAsync(LineQuantity.Create(1), LineQuantity.Create(999));
        await AcceptedAsync(Port.Create(1), Port.Create(ushort.MaxValue));
        await AcceptedAsync(PageNumber.Create(1), PageNumber.Create(int.MaxValue));
        await AcceptedAsync(SequenceNumber.Create(0), SequenceNumber.Create(int.MaxValue));
        await AcceptedAsync(FileSize.Create(0), FileSize.Create(long.MaxValue));
        await AcceptedAsync(ByteCount.Create(0), ByteCount.Create(long.MaxValue));
        await AcceptedAsync(Amount.Create(0m), Amount.Create(12.5m), Amount.Create(1_000_000_000_000m));
        await AcceptedAsync(Latitude.Create(-90), Latitude.Create(90), Latitude.Create(48.85));
        await AcceptedAsync(Ratio.Create(0), Ratio.Create(1), Ratio.Create(0.1f));
        await AcceptedAsync(Tolerance.Create(-1e300), Tolerance.Create(1));
        await AcceptedAsync(OpeningTime.Create(new TimeOnly(6, 0)), OpeningTime.Create(new TimeOnly(12, 0)), OpeningTime.Create(new TimeOnly(9, 30, 15, 123)));
        await AcceptedAsync(RecordedAt.Create(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)), RecordedAt.Create(new DateTime(2099, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
        await AcceptedAsync(OccurredAt.Create(new DateTimeOffset(2024, 2, 29, 13, 45, 30, TimeSpan.FromHours(2))));
        await AcceptedAsync(Duration.Create(TimeSpan.Zero), Duration.Create(TimeSpan.FromDays(1)));
        await AcceptedAsync(EffectiveDate.Create(new DateOnly(2024, 2, 29)));
        await AcceptedAsync(Buyer);
        await AcceptedAsync(Iban.Create(French), Iban.Create("DE89370400440532013000"));
        await AcceptedAsync(CountryCode.France, CountryCode.Germany);
        await AcceptedAsync(DocumentStatus.Draft, DocumentStatus.Create("FINAL"), DocumentStatus.Create("Draft"));
        await AcceptedAsync(Label.Create(string.Empty), Label.Create("a"), Label.Create(string.Concat(Enumerable.Repeat(Emoji, 100))));
        await AcceptedAsync(PhoneNumber.Create("+33612345678"));
        await AcceptedAsync(EmailAddress.Create("ada@example.com"));
        await AcceptedAsync(PaymentId.New(), PaymentId.New());
        await AcceptedAsync(OwnedCode<MongoOrder>.Create("ORD-1"), OwnedCode<MongoOrder>.Create("ABCDEFGHIJKL"));

        // Two emoji are four UTF-16 code units, which the type counts, and two code points, which the server counts.
        await AcceptedAsync(Nickname.Create("abcd"), Nickname.Create("abcdefgh"), Nickname.Create(Emoji + Emoji), Nickname.Create(Emoji + Emoji + Emoji + Emoji));

        async Task AcceptedAsync<TSelf>(params TSelf[] values)
        {
            var name = typeof(TSelf).Name.Split('`')[0];
            var holders = await CreateAsync<Holder<TSelf>>($"holders.{name}");
            foreach (var value in values)
            {
                await holders.InsertOneAsync(new Holder<TSelf> { Value = value }, cancellationToken: Cancellation);
            }

            (await holders.CountDocumentsAsync(FilterDefinition<Holder<TSelf>>.Empty, cancellationToken: Cancellation)).Should().Be(values.Length);
        }
    }

    /// <summary>
    /// What the validator works around: the server counts a length in code points, so a minimum length counts a
    /// character outside the Basic Multilingual Plane once where .NET counts it twice, and a maximum length never more.
    /// </summary>
    [Fact]
    public async Task The_server_counts_a_length_in_code_points()
    {
        var shortest = await CreateAsync("shortest", new BsonDocument("minLength", 4));
        var longest = await CreateAsync("longest", new BsonDocument("maxLength", 2));

        var refusal = await FluentActions.Awaiting(() => shortest.InsertOneAsync(new BsonDocument("v", Emoji + Emoji), cancellationToken: Cancellation))
            .Should().ThrowAsync<MongoWriteException>();
        refusal.Which.WriteError.Code.Should().Be(121);
        await longest.InsertOneAsync(new BsonDocument("v", Emoji + Emoji), cancellationToken: Cancellation);

        Nickname.TryCreate(Emoji + Emoji, out _).Should().BeTrue("the type counts four code units");
        ValueObjectBsonSchema.For<Holder<Nickname>>()["$jsonSchema"]["properties"]["Value"]["minLength"].Should().Be(new BsonInt32(2));
    }

    /// <summary>
    /// Gets the .NET patterns the writer publishes, each with values to check, which .NET accepts or refuses.
    /// </summary>
    public static TheoryData<string, string[]> Patterns => new()
    {
        { "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", [French, French.ToLowerInvariant(), "FR76", Emoji] },
        { @"^\+[0-9]{6,15}$", ["+33612345678", "33612345678", "+\u0663\u0663\u0663\u0663\u0663\u0663"] },
        { @"^\d{3}$", ["123", "\u0663\u0664\u0665", "12a", "\uD835\uDFCE\uD835\uDFCF\uD835\uDFD0", "12"] },
        { @"^\w+$", ["word", "x\u00E9_1", "\u0663", "a-b", Emoji, "\u00E9\u0301", "\uA7CB", "\u0ECE", "\u1C89\u0897"] },
        { @"^\s$", [" ", "\t", "\u000B", "\u000C", "\u0085", "\u00A0", "\u2028", "\u3000", "x"] },
        { @"^[\w\s-]{2,4}$", ["a b", "a-\u00A0", "a", Emoji + Emoji, "abcde"] },
        { @"^\p{Lu}{2}$", ["AB", "\u00C0\u00C9", "ab", "\uD835\uDC00\uD835\uDC01", "\uA7CB\uA7CC", "A\u1C89", "\uA7CB\u0ECE"] },
        { @"^\u00E9+$", ["\u00E9", "\u00E9\u00E9", "e", "e\u0301"] },
        { @"^[\u0000-\uD7FF\uE000-\uFFFF]+$", ["a\u00E9\uFFFF", Emoji, "\u0000x"] },
        { @"^(?<year>\d{4})-(\d{2})$", ["2024-02", "2024-2", "\u0663\u0663\u0663\u0663-\u0663\u0663"] },
        { "^a+?b*?$", ["a", "ab", "aabbb", "b"] },
        { @"^\$\.\^\|\?\*\+\(\)\[\]\{\}\\$", [@"$.^|?*+()[]{}\", "x"] },
        { @"^[\]\[\\\-\^]+$", [@"][\-^", "a"] },
        { "^ !\"#%&',/:;<=>@_`~}$", [" !\"#%&',/:;<=>@_`~}", "x"] },
        { @"\Aab\z|^cd\Z", ["ab", "ab\n", "cd", "cd\n", "cd\n\n"] },
        { "^ab$", ["ab", "ab\n", "ab\r\n"] },
        { "ab", ["xaby", "a" + Emoji + "b", Emoji + "ab"] },
        { "^acc_[0123456789abcdefghjkmnpqrstvwxyz]{21}$", ["acc_0123456789abcdefghjkm", "acc_0123456789abcdefghjk", "acc_0123456789ABCDEFGHJKM"] },
        { "^(?:ab){1000}$", [string.Concat(Enumerable.Repeat("ab", 1000)), "ab"] },
    };

    /// <summary>
    /// The server accepts every value .NET accepts against a published pattern, and refuses every value .NET refuses:
    /// a class escape is listed as the characters .NET's own Unicode tables give it, characters Unicode 15 and 16 added
    /// included, which the server's PCRE2 may not know yet; and outside the Basic Multilingual Plane, the server reads a
    /// code point that no published construct matches, as .NET reads two halves of a surrogate pair none matches.
    /// </summary>
    /// <param name="pattern">The .NET pattern.</param>
    /// <param name="values">Values .NET accepts or refuses.</param>
    [Theory]
    [MemberData(nameof(Patterns))]
    public async Task A_pattern_written_is_read_by_the_server_as_dotnet_reads_it(string pattern, string[] values)
    {
        PcrePattern.TryWrite(pattern, out var pcre, out _).Should().BeTrue();
        var dotnet = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var collection = await CreateAsync($"pattern.{Guid.NewGuid():N}", new BsonDocument { { "bsonType", "string" }, { "pattern", pcre } });

        foreach (var value in values)
        {
            var insert = () => collection.InsertOneAsync(new BsonDocument("v", value), cancellationToken: Cancellation);
            if (dotnet.IsMatch(value))
            {
                await insert.Should().NotThrowAsync("the server reads the pattern written as .NET reads {0}, which accepts {1}", pattern, value);
            }
            else
            {
                (await insert.Should().ThrowAsync<MongoWriteException>("the server reads the pattern written as .NET reads {0}, which refuses {1}", pattern, value))
                    .Which.WriteError.Code.Should().Be(121);
            }
        }
    }

    /// <summary>
    /// What the writer rewrites or leaves out, because the server reads it apart from .NET: ASCII-only classes, a count
    /// in code points, escapes and constructs it refuses, and patterns too large for it.
    /// </summary>
    [Fact]
    public async Task The_server_reads_apart_what_the_writer_rewrites_or_leaves_out()
    {
        // Read as they are, \d, \w and \s are ASCII on the server: the writer lists the characters .NET matches.
        await RefusesWhatDotnetAcceptsAsync(@"^\d$", "\u0663", rewritten: true);
        await RefusesWhatDotnetAcceptsAsync(@"^\w$", "\u00E9", rewritten: true);
        await RefusesWhatDotnetAcceptsAsync(@"^\s$", "\u00A0", rewritten: true);

        // A count over what matches half of a surrogate pair in .NET counts a code point on the server: left out.
        await RefusesWhatDotnetAcceptsAsync("^.{2}$", Emoji, rewritten: false);
        await RefusesWhatDotnetAcceptsAsync("^[^a]{2}$", Emoji, rewritten: false);
        await RefusesWhatDotnetAcceptsAsync(@"^\P{L}{2}$", Emoji, rewritten: false);

        // Read as it is, a category matches a code point outside the Basic Multilingual Plane, which .NET reads as two
        // halves no category matches; it is read with the server's own Unicode tables too, which lag .NET's. Listed, it
        // refuses what .NET refuses.
        const string Capitals = "\uD835\uDC00\uD835\uDC01";
        Regex.IsMatch(Capitals, @"^\p{Lu}{2}$", RegexOptions.None, TimeSpan.FromSeconds(1)).Should().BeFalse();
        await (await CreateAsync($"named.{Guid.NewGuid():N}", new BsonDocument("pattern", @"^\p{Lu}{2}$")))
            .InsertOneAsync(new BsonDocument("v", Capitals), cancellationToken: Cancellation);
        PcrePattern.TryWrite(@"^\p{Lu}{2}$", out var capitals, out _).Should().BeTrue();
        var listed = await CreateAsync($"listed.{Guid.NewGuid():N}", new BsonDocument("pattern", capitals));
        (await FluentActions.Awaiting(() => listed.InsertOneAsync(new BsonDocument("v", Capitals), cancellationToken: Cancellation))
            .Should().ThrowAsync<MongoWriteException>()).Which.WriteError.Code.Should().Be(121);

        // Escapes and constructs the server refuses outright, failing the whole validator: rewritten, or left out.
        await FailsToCreateAsync(@"^\u00E9$", written: @"^\x{E9}$");
        await FailsToCreateAsync(@"^[\p{L}-[a]]$", written: null);
        await FailsToCreateAsync(@"^\p{IsBasicLatin}$", written: null);
        await FailsToCreateAsync("^a{65536}$", written: null);
        await FailsToCreateAsync("^(?:ab){10000}$", written: null);

        async Task RefusesWhatDotnetAcceptsAsync(string pattern, string value, bool rewritten)
        {
            Regex.IsMatch(value, pattern, RegexOptions.None, TimeSpan.FromSeconds(1)).Should().BeTrue();
            var collection = await CreateAsync($"apart.{Guid.NewGuid():N}", new BsonDocument("pattern", pattern));
            (await FluentActions.Awaiting(() => collection.InsertOneAsync(new BsonDocument("v", value), cancellationToken: Cancellation))
                .Should().ThrowAsync<MongoWriteException>()).Which.WriteError.Code.Should().Be(121);

            PcrePattern.TryWrite(pattern, out var pcre, out _).Should().Be(rewritten);
            if (pcre is not null)
            {
                await (await CreateAsync($"written.{Guid.NewGuid():N}", new BsonDocument("pattern", pcre)))
                    .InsertOneAsync(new BsonDocument("v", value), cancellationToken: Cancellation);
            }
        }

        async Task FailsToCreateAsync(string pattern, string? written)
        {
            PcrePattern.TryWrite(pattern, out var pcre, out _).Should().Be(written is not null);
            pcre.Should().Be(written);
            await FluentActions.Awaiting(() => CreateAsync($"invalid.{Guid.NewGuid():N}", new BsonDocument("pattern", pattern)))
                .Should().ThrowAsync<MongoCommandException>().WithMessage("*Regular expression is invalid*");
        }
    }

    /// <summary>
    /// A document whose value object members may all be left out lists none as <c>required</c>: the server refuses an
    /// empty array, which would fail the whole validator.
    /// </summary>
    [Fact]
    public async Task A_document_whose_value_objects_may_all_be_left_out_is_validated_without_required()
    {
        ValueObjectBsonSchema.For<MongoLeftOut>()["$jsonSchema"].AsBsonDocument.Contains("required").Should().BeFalse();
        var documents = await CreateAsync<MongoLeftOut>("left-out");

        await documents.InsertOneAsync(new MongoLeftOut(), cancellationToken: Cancellation);
        await documents.InsertOneAsync(new MongoLeftOut { Maybe = PageNumber.Create(2), Omitted = PageNumber.Create(3) }, cancellationToken: Cancellation);

        (await documents.CountDocumentsAsync(FilterDefinition<MongoLeftOut>.Empty, cancellationToken: Cancellation)).Should().Be(2);
        var refusal = await FluentActions.Awaiting(() => Database.GetCollection<BsonDocument>("left-out").InsertOneAsync(new BsonDocument("Omitted", 0), cancellationToken: Cancellation))
            .Should().ThrowAsync<MongoWriteException>();
        refusal.Which.WriteError.Code.Should().Be(121);
    }

    [Fact]
    public async Task A_document_inserted_without_its_entity_identifier_is_given_one_minted_by_the_type()
    {
        var payments = await CreateAsync<MongoPayment>("payments");
        var first = new MongoPayment { Note = "first" };
        var second = new MongoPayment { Note = "second" };
        var assigned = new MongoPayment { Id = PaymentId.New(), Note = "assigned" };
        var kept = assigned.Id;

        await payments.InsertManyAsync([first, second, assigned], cancellationToken: Cancellation);

        first.Id.Value.Should().StartWith("pay_");
        second.Id.Value.Should().StartWith("pay_").And.NotBe(first.Id.Value);
        assigned.Id.Should().Be(kept);
        var stored = await Database.GetCollection<BsonDocument>("payments").Find(new BsonDocument("Note", "first")).SingleAsync(Cancellation);
        stored["_id"].Should().Be(new BsonString(first.Id.Value));
        (await payments.Find(x => x.Id == second.Id).SingleAsync(Cancellation)).Note.Should().Be("second");
    }

    private async Task<IMongoCollection<T>> CreateAsync<T>(string name)
    {
        await Database.CreateCollectionAsync(
            name,
            new CreateCollectionOptions<BsonDocument> { Validator = new BsonDocumentFilterDefinition<BsonDocument>(ValueObjectBsonSchema.For<T>()) },
            Cancellation);

        return Database.GetCollection<T>(name);
    }

    private async Task<IMongoCollection<BsonDocument>> CreateAsync(string name, BsonDocument value)
    {
        var validator = new BsonDocument("$jsonSchema", new BsonDocument("properties", new BsonDocument("v", value)));
        await Database.CreateCollectionAsync(
            name,
            new CreateCollectionOptions<BsonDocument> { Validator = new BsonDocumentFilterDefinition<BsonDocument>(validator) },
            Cancellation);

        return Database.GetCollection<BsonDocument>(name);
    }
}

/// <summary>
/// A document whose value object members may all be left out: one nullable, one left out when it is the default.
/// </summary>
public sealed class MongoLeftOut
{
    public ObjectId Id { get; set; }

    public PageNumber? Maybe { get; set; }

    [BsonIgnoreIfDefault]
    public PageNumber Omitted { get; set; }
}

/// <summary>
/// A payment, whose entity identifier is its <c>_id</c>.
/// </summary>
public sealed class MongoPayment
{
    public PaymentId Id { get; set; }

    public string Note { get; set; } = string.Empty;
}
