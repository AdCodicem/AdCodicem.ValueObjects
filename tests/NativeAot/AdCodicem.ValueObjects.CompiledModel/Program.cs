// A round trip through SQL Server, under the model the project is built with: the compiled one that ci.yml has
// `dotnet ef dbcontext optimize` write, when it builds with CompiledModel=jit. The relaxed context writes a shipment
// holding every value object and another leaving every optional one out, both contexts read them back and filter on
// them, collections of value objects included, and the relaxed one tracks a change, an element added in place included.
//
// The queries are also the ones the native AOT variant precompiles, which is why they sit here, each on a context held
// in a local and filtering on locals: query precompilation takes a context passed as a parameter for a dynamic query,
// and fails on a parameter of the method inside the query.
//
// On the compiled model, the contexts read untracked (UNTRACKED_READS), since a strict one cannot track there: a compiled
// model rebuilds the sentinel of each property through its converter, which in a strict context validates it, so that
// the first entity it tracks fails on a value object that refuses the default of its underlying type (the EF Core guide,
// "Compiled models"). The model for native AOT is built from the other branch: Entity Framework Core 10 writes a
// precompiled untracked query that does not compile, for any entity type in a namespace, with or without this library.
using AdCodicem.ValueObjects.CompiledModel;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

await using var server = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
await server.StartAsync();

var connectionString = new SqlConnectionStringBuilder(server.GetConnectionString()) { InitialCatalog = "shop" }.ConnectionString;
var failures = 0;

await using (var context = new ShopContext(connectionString))
{
    await context.Database.EnsureCreatedAsync();
    context.Shipments.AddRange(Shipments.Full, Shipments.Sparse);
    Report("relaxed: saved", await context.SaveChangesAsync() == 2);
}

var full = Shipments.Full.Id;
var sparse = Shipments.Sparse.Id;
var quantity = Shipments.Full.Quantity;
var number = Shipments.Full.Number;
var previous = Shipments.Full.Previous;
var batch = Shipments.Full.Batches[1];
var contact = Shipments.Full.Contacts[1];

// Two shapes Entity Framework Core 10 fails to precompile, whatever the element type, are written around: Contains over an
// array, which C# 14 binds to MemoryExtensions.Contains over a span, is called on the array as a sequence, and
// List<T?>.Contains takes a local of the optional type, never a T.
Quantity? optionalBatch = Shipments.Full.OptionalBatches[0];
foreach (var strict in (bool[])[false, true])
{
    var name = strict ? "strict" : "relaxed";
    await using var context = strict ? new StrictShopContext(connectionString) : new ShopContext(connectionString);
    Report($"{name}: model {context.Model.GetType().Name}", Shipments.IsTheModelBuiltWith(context, strict));

#if UNTRACKED_READS
    var readFull = await context.Shipments.AsNoTracking().SingleAsync(shipment => shipment.Id == full);
    var readSparse = await context.Shipments.AsNoTracking().SingleAsync(shipment => shipment.Id == sparse);
#else
    var readFull = await context.Shipments.SingleAsync(shipment => shipment.Id == full);
    var readSparse = await context.Shipments.SingleAsync(shipment => shipment.Id == sparse);
#endif
    Compare($"{name}: full shipment read back", Shipments.Full, readFull);
    Compare($"{name}: sparse shipment read back", Shipments.Sparse, readSparse);

    Report($"{name}: filtered on a quantity", await context.Shipments.CountAsync(shipment => shipment.Quantity == quantity) == 1);
    Report($"{name}: filtered on an absent optional quantity", await context.Shipments.CountAsync(shipment => shipment.OptionalQuantity == null) == 1);
    Report($"{name}: filtered on a construction of a generic value object", await context.Shipments.CountAsync(shipment => shipment.Number == number) == 2);
    Report($"{name}: filtered on an optional identifier", await context.Shipments.CountAsync(shipment => shipment.Previous == previous) == 1);
    Report(
        $"{name}: filtered on an element of an array",
        await context.Shipments.CountAsync(shipment => ((IEnumerable<Quantity>)shipment.Batches).Contains(batch)) == 1);
    Report($"{name}: filtered on an element of a collection of text", await context.Shipments.CountAsync(shipment => shipment.Contacts.Contains(contact)) == 1);
    Report(
        $"{name}: filtered on an element of a collection of optional value objects",
        await context.Shipments.CountAsync(shipment => shipment.OptionalBatches.Contains(optionalBatch)) == 1);
}

await using (var context = new ShopContext(connectionString))
{
    var tracked = await context.Shipments.SingleAsync(shipment => shipment.Id == full);
    Report("relaxed: unchanged once read", !context.ChangeTracker.HasChanges());
    tracked.OptionalQuantity = null;
    Report("relaxed: an optional value object set to null saved", await context.SaveChangesAsync() == 1);
    Report("relaxed: then absent", await context.Shipments.CountAsync(shipment => shipment.OptionalQuantity == null) == 2);

    var added = EmailAddress.Create("linus@example.net");
    tracked.Contacts.Add(added);
    Report("relaxed: an element added to a collection in place saved", await context.SaveChangesAsync() == 1);
    Report("relaxed: then found", await context.Shipments.CountAsync(shipment => shipment.Contacts.Contains(added)) == 1);
}

return failures == 0 ? 0 : 1;

void Report(string scenario, bool passed)
{
    Console.WriteLine($"{(passed ? "ok  " : "FAIL")} {scenario}");
    failures += passed ? 0 : 1;
}

void Compare(string scenario, Shipment written, Shipment read)
{
    var differences = Shipments.Differences(written, read);
    Report(differences.Length == 0 ? scenario : $"{scenario}, with {differences}", differences.Length == 0);
}
