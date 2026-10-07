using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// What LINQ and <c>Builders</c> send the server for a query over value objects: the bare values the serializers write,
/// and <c>.Value</c> translated as the field itself. Rendering a query opens no connection; the integration suite runs
/// the same shapes against a server.
/// </summary>
public sealed class MongoDbQueryTests : IDisposable
{
    private const string ValidIban = "FR7630006000011234567890189";

    private static readonly Iban Fr = Iban.Create(ValidIban);

    private static readonly Quantity Two = Quantity.Create(2);

    private static readonly CustomerId Customer = CustomerId.Create(Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff"));

    private readonly MongoClient _client;

    private readonly IMongoCollection<MongoOrder> _orders;

    public MongoDbQueryTests()
    {
        MongoDb.EnsureRegistered();

        // A client nothing connects through: translating a query needs none.
        _client = new MongoClient("mongodb://127.0.0.1:1");
        _orders = _client.GetDatabase("shop").GetCollection<MongoOrder>("orders");
    }

    public static TheoryData<string, string> Filters => new()
    {
        { "x.Iban == iban", """{ "Iban" : "FR7630006000011234567890189" }""" },
        { "x.Iban.Value == text", """{ "Iban" : "FR7630006000011234567890189" }""" },
        { "x.Iban.Value.StartsWith", """{ "Iban" : { "$regularExpression" : { "pattern" : "^FR", "options" : "s" } } }""" },
        { "x.Iban.Value.Length", """{ "Iban" : { "$regularExpression" : { "pattern" : "^.{21,}$", "options" : "s" } } }""" },
        { "x.Quantity > q", """{ "Quantity" : { "$gt" : 2 } }""" },
        { "x.Quantity.Value > 2", """{ "Quantity" : { "$gt" : 2 } }""" },
        { "x.Customer == id", """{ "Customer" : { "$binary" : { "base64" : "b5YZ/4uG0BG0LQDAT8lk/w==", "subType" : "04" } } }""" },
        { "x.Backup == iban", """{ "Backup" : "FR7630006000011234567890189" }""" },
        { "x.Backup == null", """{ "Backup" : null }""" },
        { "x.Backup.Value.Value.StartsWith", """{ "Backup" : { "$regularExpression" : { "pattern" : "^DE", "options" : "s" } } }""" },
        { "x.Countries.Contains", """{ "Countries" : "FR" }""" },
        { "list.Contains(x.Iban)", """{ "Iban" : { "$in" : ["FR7630006000011234567890189"] } }""" },
        { "x.Totals[key] > 10", """{ "Totals.FR" : { "$gt" : { "$numberDecimal" : "10" } } }""" },
        { "x.Totals.ContainsKey", """{ "Totals.FR" : { "$exists" : true } }""" },
        { "x.Status == known value", """{ "Status" : "final" }""" },
        { "x.FirstLine.Quantity.Value >= 2", """{ "FirstLine.Quantity" : { "$gte" : 2 } }""" },
        { "x.Lines.Any(l => l.Quantity == q)", """{ "Lines" : { "$elemMatch" : { "Quantity" : 2 } } }""" },
        { "x.Reference == construction", """{ "Reference" : "PO-1" }""" },
    };

    private static readonly Dictionary<string, Expression<Func<MongoOrder, bool>>> FilterExpressions = new()
    {
        ["x.Iban == iban"] = x => x.Iban == Fr,
        ["x.Iban.Value == text"] = x => x.Iban.Value == ValidIban,
        ["x.Iban.Value.StartsWith"] = x => x.Iban.Value.StartsWith("FR"),
        ["x.Iban.Value.Length"] = x => x.Iban.Value.Length > 20,
        ["x.Quantity > q"] = x => x.Quantity > Two,
        ["x.Quantity.Value > 2"] = x => x.Quantity.Value > 2,
        ["x.Customer == id"] = x => x.Customer == Customer,
        ["x.Backup == iban"] = x => x.Backup == Fr,
        ["x.Backup == null"] = x => x.Backup == null,
        ["x.Backup.Value.Value.StartsWith"] = x => x.Backup!.Value.Value.StartsWith("DE"),
        ["x.Countries.Contains"] = x => x.Countries.Contains(CountryCode.France),
        ["list.Contains(x.Iban)"] = x => new List<Iban> { Fr }.Contains(x.Iban),
        ["x.Totals[key] > 10"] = x => x.Totals[CountryCode.France] > 10,
        ["x.Totals.ContainsKey"] = x => x.Totals.ContainsKey(CountryCode.France),
        ["x.Status == known value"] = x => x.Status == DocumentStatus.Final,
        ["x.FirstLine.Quantity.Value >= 2"] = x => x.FirstLine!.Quantity.Value >= 2,
        ["x.Lines.Any(l => l.Quantity == q)"] = x => x.Lines.Any(l => l.Quantity == Two),
        ["x.Reference == construction"] = x => x.Reference == Reference<PurchaseOrder>.Create("PO-1"),
    };

    /// <summary>
    /// A filter over a value object, or over its <c>.Value</c>, renders as the same filter over the primitive would, on
    /// the field itself, in LINQ as in <c>Builders</c>' expression filters.
    /// </summary>
    /// <param name="shape">The shape of the filter.</param>
    /// <param name="expected">The filter the server gets.</param>
    [Theory]
    [MemberData(nameof(Filters))]
    public void A_filter_over_a_value_object_renders_as_over_its_underlying_value(string shape, string expected)
    {
        var expression = FilterExpressions[shape];

        Render(Builders<MongoOrder>.Filter.Where(expression)).Should().Be(expected);
        _orders.AsQueryable().Where(expression).ToString().Should().Be($$"""shop.orders.Aggregate([{ "$match" : {{expected}} }])""");
    }

    [Fact]
    public void Projections_sorts_and_groups_over_Value_render_on_the_field()
    {
        _orders.AsQueryable().Select(x => x.Iban).ToString()
            .Should().Be("""shop.orders.Aggregate([{ "$project" : { "_v" : "$Iban", "_id" : 0 } }])""");
        _orders.AsQueryable().Select(x => x.Iban.Value.ToLowerInvariant()).ToString()
            .Should().Be("""shop.orders.Aggregate([{ "$project" : { "_v" : { "$toLower" : "$Iban" }, "_id" : 0 } }])""");
        _orders.AsQueryable().Select(x => new { x.Iban, Next = x.Quantity.Value + 1 }).ToString()
            .Should().Be("""shop.orders.Aggregate([{ "$project" : { "Iban" : "$Iban", "Next" : { "$add" : ["$Quantity", 1] }, "_id" : 0 } }])""");
        _orders.AsQueryable().OrderBy(x => x.Quantity).ToString()
            .Should().Be("""shop.orders.Aggregate([{ "$sort" : { "Quantity" : 1 } }])""");
        _orders.AsQueryable().GroupBy(x => x.Customer).Select(g => new { g.Key, Total = g.Sum(x => x.Page.Value) }).ToString()
            .Should().Be("""shop.orders.Aggregate([{ "$group" : { "_id" : "$Customer", "__agg0" : { "$sum" : "$Page" } } }, { "$project" : { "Key" : "$_id", "Total" : "$__agg0", "_id" : 0 } }])""");
    }

    [Fact]
    public void Builders_write_a_value_object_as_its_underlying_value()
    {
        Render(Builders<MongoOrder>.Filter.Eq(x => x.Iban, Fr)).Should().Be("""{ "Iban" : "FR7630006000011234567890189" }""");
        Render(Builders<MongoOrder>.Filter.Gte(x => x.Quantity, Two)).Should().Be("""{ "Quantity" : { "$gte" : 2 } }""");
        Render(Builders<MongoOrder>.Filter.AnyIn(x => x.Countries, [CountryCode.France, CountryCode.Belgium]))
            .Should().Be("""{ "Countries" : { "$in" : ["FR", "BE"] } }""");
        Render(Builders<MongoOrder>.Update.Set(x => x.Iban, Fr)).Should().Be("""{ "$set" : { "Iban" : "FR7630006000011234567890189" } }""");
        Render(Builders<MongoOrder>.Update.Inc(x => x.Quantity.Value, (short)1)).Should().Be("""{ "$inc" : { "Quantity" : 1 } }""");
        Render(Builders<MongoOrder>.Sort.Ascending(x => x.Quantity)).Should().Be("""{ "Quantity" : 1 }""");
    }

#pragma warning disable VO0010 // The uninitialized constant is what the serializer refuses.
    [Fact]
    public void A_query_constant_the_type_refuses_is_refused_with_its_rule()
    {
        var filter = FluentActions.Invoking(() => Render(Builders<MongoOrder>.Filter.Where(x => x.Page == default(PageNumber))))
            .Should().Throw<BsonSerializationException>().Which;
        ValueObjectErrors.TryGetCode(filter, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);

        // LINQ translates through the same serializer; a query's text reports the refusal instead of throwing it.
        _orders.AsQueryable().Where(x => x.Page == default(PageNumber)).ToString()
            .Should().StartWith("MongoDB.Bson.BsonSerializationException: The value to write is not a valid PageNumber: ");

        FluentActions.Invoking(() => Render(Builders<MongoOrder>.Update.Set(x => x.Page, default(PageNumber))))
            .Should().Throw<BsonSerializationException>()
            .Which.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.OutOfRange);
    }
#pragma warning restore VO0010

    [Fact]
    public void A_member_given_a_representation_is_written_and_queried_in_it()
    {
        var represented = new MongoRepresented
        {
            PageText = PageNumber.Create(7),
            MaybePageText = PageNumber.Create(8),
            PagesText = [PageNumber.Create(1), PageNumber.Create(2)],
            AsText = Customer,
            Legacy = Customer,
            Narrowed = FileSize.Create(5_000_000_000),
            Day = RecordedAt.Create(new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc)),
        };

        var document = represented.ToBsonDocument();

        document.ToJson().Should().Be(new BsonDocument
        {
            { "_id", ObjectId.Empty },
            { "PageText", "7" },
            { "MaybePageText", "8" },
            { "PagesText", new BsonArray { "1", "2" } },
            { "AsText", "6f9619ff-8b86-d011-b42d-00c04fc964ff" },
            { "Legacy", new BsonBinaryData(Customer.Value, GuidRepresentation.CSharpLegacy) },
            { "Narrowed", unchecked((int)5_000_000_000) },
            { "Day", new BsonDateTime(new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc)) },
        }.ToJson());
        var read = BsonSerializer.Deserialize<MongoRepresented>(document);
        (read.PageText, read.MaybePageText, read.AsText, read.Legacy, read.Day)
            .Should().Be((represented.PageText, represented.MaybePageText, represented.AsText, represented.Legacy, represented.Day));
        read.PagesText.Should().Equal(represented.PagesText);

        var represents = _client.GetDatabase("shop").GetCollection<MongoRepresented>("represented").AsQueryable();
        represents.Where(x => x.PageText > PageNumber.Create(5)).ToString()
            .Should().Be("""shop.represented.Aggregate([{ "$match" : { "PageText" : { "$gt" : "5" } } }])""");
        represents.Where(x => x.AsText == Customer).ToString()
            .Should().Be("""shop.represented.Aggregate([{ "$match" : { "AsText" : "6f9619ff-8b86-d011-b42d-00c04fc964ff" } }])""");
    }

    public void Dispose() => _client.Dispose();

    private static string Render(FilterDefinition<MongoOrder> filter)
        => filter.Render(Args<MongoOrder>()).ToJson();

    private static string Render(UpdateDefinition<MongoOrder> update)
        => update.Render(Args<MongoOrder>()).ToJson();

    private static string Render(SortDefinition<MongoOrder> sort)
        => sort.Render(Args<MongoOrder>()).ToJson();

    private static RenderArgs<T> Args<T>() => new(BsonSerializer.LookupSerializer<T>(), BsonSerializer.SerializerRegistry);
}

/// <summary>
/// An order as a MongoDB document, holding value objects in every shape a query reaches.
/// </summary>
public sealed class MongoOrder
{
    public ObjectId Id { get; set; }

    public Iban Iban { get; set; }

    public Quantity Quantity { get; set; }

    public PageNumber Page { get; set; }

    public CustomerId Customer { get; set; }

    public Iban? Backup { get; set; }

    public List<CountryCode> Countries { get; set; } = [];

    public Dictionary<CountryCode, decimal> Totals { get; set; } = [];

    public DocumentStatus Status { get; set; }

    public MongoLine? FirstLine { get; set; }

    public List<MongoLine> Lines { get; set; } = [];

    public Reference<PurchaseOrder> Reference { get; set; }
}

/// <summary>
/// A line of an order, as a sub-document.
/// </summary>
public sealed class MongoLine
{
    public Quantity Quantity { get; set; }

    public Amount? Price { get; set; }
}

/// <summary>
/// Value objects whose members say how the underlying value is stored, as they would on the primitive.
/// </summary>
public sealed class MongoRepresented
{
    public ObjectId Id { get; set; }

    [BsonRepresentation(BsonType.String)]
    public PageNumber PageText { get; set; }

    [BsonRepresentation(BsonType.String)]
    public PageNumber? MaybePageText { get; set; }

    [BsonRepresentation(BsonType.String)]
    public List<PageNumber> PagesText { get; set; } = [];

    [BsonRepresentation(BsonType.String)]
    public CustomerId AsText { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.CSharpLegacy)]
    public CustomerId Legacy { get; set; }

    [BsonRepresentation(BsonType.Int32, AllowOverflow = true)]
    public FileSize Narrowed { get; set; }

    [BsonDateTimeOptions(DateOnly = true)]
    public RecordedAt Day { get; set; }
}
