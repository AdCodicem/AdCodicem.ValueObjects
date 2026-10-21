// Runs a fixed script over a Marten document store on PostgreSQL and writes one line per scenario to the file it is
// given. heavy.yml's native AOT (Marten) job runs it once under the JIT (dotnet run) and once as the native binary,
// against the PostgreSQL service container NATIVE_AOT_POSTGRES names, then diffs the two files, as for
// AdCodicem.ValueObjects.NativeAot: a difference is a behaviour native AOT changes, and fails the job. The process also
// fails on its own when a scenario throws where neither run should.
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using AdCodicem.ValueObjects.Marten;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.NativeAot;
using AdCodicem.ValueObjects.NativeAot.Documents;
using JasperFx;
using Marten;
using Marten.Linq;
using Npgsql;

if (args.Length != 1 || Environment.GetEnvironmentVariable("NATIVE_AOT_POSTGRES") is not { Length: > 0 } connectionString)
{
    await Console.Error.WriteLineAsync("Usage: NATIVE_AOT_POSTGRES=<connection string> AdCodicem.ValueObjects.NativeAot.Marten <output file>");
    return 2;
}

const string Schema = "native_aot_marten";
var report = new Report();
report.Line("runtime", $"dynamic code supported: {RuntimeFeature.IsDynamicCodeSupported}");

// A construction of a generic value object and one written by hand: native AOT describes neither by reflection, so the
// application registers both, the construction with the converter named statically, the other with no converter at all.
ValueObjectRegistry.Register<DocumentNumber<PurchaseOrder>, string>(static () => new DocumentNumber<PurchaseOrder>.ValueJsonConverter());
ValueObjectRegistry.Register<HandWrittenCode, string>(HandWrittenCode.Schema);

using var store = DocumentStore.For(options =>
{
    options.Connection(connectionString);
    options.DatabaseSchemaName = Schema;
    options.AutoCreateSchemaObjects = AutoCreate.All;
    options.UseSystemTextJsonForSerialization(MartenJsonContext.Default.Options);
    options.UseValueObjects();
    options.UseValueObjectId<Invoice, CustomerId, Guid>();

    // Marten builds the mapping of a document type it was not told of by closing a generic type at run time, which a
    // native image cannot: every document type is named here.
    options.Schema.For<Shop>().Duplicate(x => x.Quantity).Duplicate(x => x.Email);
    options.Schema.For<Invoice>();
    options.Schema.For<Unregistered>();
});
await store.Advanced.Clean.CompletelyRemoveAllAsync();

var shops = Enumerable.Range(0, 4).Select(static index => new Shop
{
    Id = Guid.Parse($"00000000-0000-0000-0000-{index + 1:D12}"),
    Index = index,
    Email = EmailAddress.Create(index % 2 == 0 ? $"ada{index}@example.com" : $"grace{index}@example.org"),
    Backup = index % 2 == 0 ? null : EmailAddress.Create("backup@example.com"),
    Quantity = Quantity.Create((short)(10 * (index + 1))),
    Amount = Amount.Create(12.5m * (index + 1)),
    Page = PageNumber.Create(index + 1),
    Size = FileSize.Create(9_000_000_000L + index),
    Latitude = Latitude.Create(10.5 * index),
    Effective = EffectiveDate.Create(new DateOnly(2026, index + 1, 1)),
    At = OccurredAt.Create(new DateTimeOffset(2026, 1, 1, 12 + index, 0, 0, TimeSpan.FromHours(index - 1))),
    Consent = Consent.Create(index % 2 == 1),
    Grade = Grade.Create((char)('A' + index)),
    Status = index % 2 == 0 ? DocumentStatus.Draft : DocumentStatus.Final,
    Number = DocumentNumber<PurchaseOrder>.Create($"po-{index}"),
    Code = HandWrittenCode.Create(index % 2 == 0 ? "ABC" : "XYZ"),
}).ToArray();
await using (var write = store.LightweightSession())
{
    write.Store(shops);
    write.Store(new Invoice { Id = CustomerId.Create(Guid.Parse("11111111-2222-3333-4444-555555555555")), Total = Amount.Create(10m) });
    await write.SaveChangesAsync();
}

foreach (var row in await RowsAsync($"select data::text || ' | ' || quantity || ' | ' || email from {Schema}.mt_doc_shop order by data ->> 'Index'"))
{
    report.Line("stored", row);
}

await using var session = store.QuerySession();
var email = EmailAddress.Create("ada0@example.com");
var pages = new List<PageNumber> { PageNumber.Create(2), PageNumber.Create(4) };
var statuses = new[] { DocumentStatus.Final };

await AnswersAsync("x.Email == email", x => x.Email == email);
await AnswersAsync("x.Email.Value.StartsWith", static x => x.Email.Value.StartsWith("grace"));
await AnswersAsync("x.Quantity > quantity", static x => x.Quantity > Quantity.Create(20));
await AnswersAsync("x.Quantity.Value <= 20", static x => x.Quantity.Value <= 20);
await AnswersAsync("x.Amount >= amount", static x => x.Amount >= Amount.Create(25m));
await AnswersAsync("x.Page != page", static x => x.Page != PageNumber.Create(1));
await AnswersAsync("x.Size > size", static x => x.Size > FileSize.Create(9_000_000_001L));
await AnswersAsync("x.Latitude < latitude", static x => x.Latitude < Latitude.Create(20));
await AnswersAsync("x.Effective < date", static x => x.Effective < EffectiveDate.Create(new DateOnly(2026, 3, 1)));
await AnswersAsync("x.At > moment at an offset", static x => x.At > OccurredAt.Create(new DateTimeOffset(2026, 1, 1, 13, 0, 0, TimeSpan.FromHours(2))));
await AnswersAsync("x.Consent == consent", static x => x.Consent == Consent.Create(true));
await AnswersAsync("x.Grade == grade", static x => x.Grade == Grade.Create('B'));
await AnswersAsync("x.Status == known value", static x => x.Status == DocumentStatus.Final);
await AnswersAsync("list.Contains(x.Page)", x => pages.Contains(x.Page));
await AnswersAsync("x.Status.IsOneOf(array)", x => x.Status.IsOneOf(statuses));
await AnswersAsync("x.Backup == null", static x => x.Backup == null);
await AnswersAsync("x.Number == construction", static x => x.Number == DocumentNumber<PurchaseOrder>.Create("PO-2"));
await AnswersAsync("x.Code == written by hand", static x => x.Code == HandWrittenCode.Create("XYZ"));

report.Line("OrderByDescending(x => x.Amount)", string.Join(",", await session.Query<Shop>().OrderByDescending(static x => x.Amount).Select(static x => x.Index).ToListAsync()));
await LineAsync("Select(x => x.Page)", async () => string.Join(",", await session.Query<Shop>().OrderBy(static x => x.Index).Select(static x => x.Page).ToListAsync()));
await LineAsync("MaxAsync(x => x.Amount)", async () => (await session.Query<Shop>().MaxAsync(static x => x.Amount)).ToString(null, CultureInfo.InvariantCulture));

// Marten's duplicated field of a value type is not one of its value-type members: it builds the selector of a projection
// from the field's type, Quantity?, the type the package's members report, which the JIT refuses as a result type it
// does not know and a native image as a select clause it never compiled. Both refuse before a row is read.
await RefusedAsync("Select(x => x.Quantity), duplicated", async () => string.Join(",", await session.Query<Shop>().OrderBy(static x => x.Index).Select(static x => x.Quantity).ToListAsync()));
await LineAsync("LoadAsync(identity)", async () => (await session.LoadAsync<Invoice>(CustomerId.Create(Guid.Parse("11111111-2222-3333-4444-555555555555"))))?.Total.ToString(null, CultureInfo.InvariantCulture) ?? "none");
await LineAsync("x.Id == identity", async () => (await session.Query<Invoice>().Where(static x => x.Id == CustomerId.Create(Guid.Parse("11111111-2222-3333-4444-555555555555"))).CountAsync()).ToString(CultureInfo.InvariantCulture));

// A compiled query: a single value object is refused, its .Value form and a collection are bound on each call.
await LineAsync("compiled x.Quantity > Minimum", async () => (await session.QueryAsync(new ShopsAboveQuantity { Minimum = Quantity.Create(20) })).Count().ToString(CultureInfo.InvariantCulture));
foreach (var minimum in (int[])[10, 30])
{
    await LineAsync($"compiled x.Quantity.Value > {minimum}", async () => string.Join(",", (await session.QueryAsync(new ShopsAboveQuantityValue { Minimum = minimum })).Select(static x => x.Index)));
}

foreach (var asked in (int[][])[[1, 3], [2]])
{
    await LineAsync($"compiled Pages.Contains(x.Page) [{string.Join(",", asked)}]", async () => string.Join(",", (await session.QueryAsync(new ShopsOnPages { Pages = [.. asked.Select(PageNumber.Create)] })).Select(static x => x.Index)));
}

// A value object nothing registered: the member source refuses it, naming the registrations.
await LineAsync("x.Code == unregistered", async () => (await session.Query<Unregistered>().Where(static x => x.Code == UnregisteredCode.CreateUnchecked("abc")).CountAsync()).ToString(CultureInfo.InvariantCulture));

// A stored value the rules refuse, written past them: refused with its code however it is read, a document by the
// generated converter, a projected value by the package.
await ExecuteAsync($"update {Schema}.mt_doc_shop set data = jsonb_set(data, '{{Page}}', '0') where data ->> 'Index' = '0'");
await using (var refused = store.QuerySession())
{
    await LineAsync("refused LoadAsync", async () => (await refused.LoadAsync<Shop>(shops[0].Id))?.Page.ToString() ?? "none");
    await LineAsync("refused Select(x => x.Page)", async () => string.Join(",", await refused.Query<Shop>().Select(static x => x.Page).ToListAsync()));
    await LineAsync("refused MinAsync(x => x.Page)", async () => (await refused.Query<Shop>().MinAsync(static x => x.Page)).ToString());
}

await File.WriteAllLinesAsync(args[0], report.Lines);
if (report.Unexpected.Count == 0)
{
    Console.WriteLine($"{report.Lines.Count} lines written to {args[0]}.");
    return 0;
}

foreach (var line in report.Unexpected)
{
    await Console.Error.WriteLineAsync($"Unexpected: {line}");
}

return 1;

// Runs a filter on the server and in memory: the line holds what the server answers, and the process fails where the
// documents in memory answer otherwise.
async Task AnswersAsync(string subject, Expression<Func<Shop, bool>> filter)
{
    try
    {
        var rows = (await session.Query<Shop>().Where(filter).ToListAsync()).Select(static x => x.Index).Order().ToList();
        var expected = shops.Where(filter.Compile()).Select(static x => x.Index).ToList();
        if (rows.SequenceEqual(expected))
        {
            report.Line(subject, string.Join(",", rows));
        }
        else
        {
            report.Fail(subject, $"[{string.Join(",", rows)}] where the documents in memory answer [{string.Join(",", expected)}]");
        }
    }
    catch (Exception exception)
    {
        report.Fail(subject, Report.Describe(exception));
    }
}

// Runs a scenario whose outcome, a value or a refusal, is the same in both runs.
async Task LineAsync(string subject, Func<Task<string>> scenario)
{
    try
    {
        report.Line(subject, await scenario());
    }
    catch (Exception exception)
    {
        report.Line(subject, $"{Report.Describe(exception)}: {exception.Message.Split('\n')[0]}");
    }
}

// Runs a scenario Marten refuses in both runs, with an exception that differs between them, Marten's own: the line says
// that it was refused, so that any other outcome, a value or another exception, still differs.
async Task RefusedAsync(string subject, Func<Task<string>> scenario)
{
    try
    {
        report.Line(subject, await scenario());
    }
    catch (Exception exception) when (exception is NotSupportedException or MissingMethodException)
    {
        report.Line(subject, "refused before a row is read");
    }
}

async Task<List<string>> RowsAsync(string sql)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();
    List<string> rows = [];
    while (await reader.ReadAsync())
    {
        rows.Add(reader.GetString(0));
    }

    return rows;
}

async Task ExecuteAsync(string sql)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}
