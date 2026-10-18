---
title: Use with Marten
sidebar_label: Marten
slug: /how-to/marten
description: Teach a Marten document store every value object, so that its LINQ, its duplicated fields and its compiled queries see the underlying value, its serializer writes it, and a stored value the rules refuse is refused with its code however it is read.
---

# Use with Marten

```bash
dotnet add package AdCodicem.ValueObjects.Marten
```

```csharp skip
builder.Services.AddMarten(options =>
{
    options.Connection(connectionString);
    options.UseSystemTextJsonForSerialization(AppJsonContext.Default.Options); // optional
    options.Schema.For<Order>().Duplicate(x => x.Iban);
}).UseValueObjects();
```

Marten stores a value object well without the package: its System.Text.Json serializer honours the generated
converter, so a document holds the bare values, `"Iban": "FR76…"`, `"Quantity": 20`, and a stored value the rules refuse
fails the read with the converter's `ValueObjectJsonException`. It cannot query one:

- **Every comparison throws**: `x.Iban == iban`, `x.Quantity > q`, a nullable member compared with a value, a member of
  a child collection: `BadLinqExpressionException: Marten cannot support custom value types in Linq expression.`
- **`.Value` answers no rows, silently**: `x.Iban.Value == "FR76…"` reads `d.data -> 'Iban' ->> 'Value'`, a path no
  document has, and `Select(x => x.Iban.Value)` returns empty strings.
- **The rest fails later**: `list.Contains(x.Country)` at execution, a compiled query taking a value object at planning,
  `Duplicate(x => x.Iban)` at the first write, `MaxAsync(x => x.Quantity)` on the server (`max(jsonb)`).
- **`RegisterValueType`, Marten's own answer, refuses every generated value object**: it wants a single public property,
  counting static ones, and a generated value object has its `Schema` beside its `Value`.

The package teaches Marten each value object as Marten's own value types are taught, through its public extension
points: a member source hands Marten its own value-type member, or collection member, for each value object a query
meets, closed through the descriptor's visitor ([Run-time lookup](runtime-lookup.md#back-to-the-typed-path)), a parser
reads `Contains` and `IsOneOf`, and each value object's nullable type joins the value types Marten's duplicated fields
read, the value object itself kept out so that Marten refuses a compiled query taking one
([Compiled queries](#compiled-queries)). That last list is internal to Marten; the package reaches it through
`[UnsafeAccessor]`, and a test pins it, so that a Marten release that moves it fails the package's CI before it reaches
you.

## Registering the value objects

`AddMarten(…).UseValueObjects()` registers through Marten's `ConfigureMarten`, which Marten applies after `AddMarten`'s
own configuration: the order of the calls inside `AddMarten` does not matter, and a serializer replaced there gets the
converter factory too. Marten applies each `ConfigureMarten` in the order it was registered, so one registered after
`UseValueObjects()` runs later: if it replaces the serializer, register it before, or end it with
`options.UseValueObjects()`, or the factory is lost with the serializer it was in.

On bare `StoreOptions`, call `UseValueObjects()` after `UseSystemTextJsonForSerialization`, which replaces the
serializer, and the factory with it:

```csharp skip
var store = DocumentStore.For(options =>
{
    options.Connection(connectionString);
    options.UseSystemTextJsonForSerialization(AppJsonContext.Default.Options);
    options.UseValueObjects();
});
```

`UseValueObjects()` teaches Marten every value object the registry describes: those registered when it runs, and any
other the first time a query, a duplicated field or a compiled query meets it. `UseValueObject<TSelf, TValue>()`
teaches one value object without the registry, as native AOT needs for a construction of a generic one or a value
object written by hand ([Native AOT](#native-aot)); a store taught only that way leaves every other value object to
Marten.

## The serializer

`UseValueObjects()` adds `ValueObjectJsonConverterFactory` to the store's System.Text.Json options. Marten copies the
options it is given, so a source-generated context taken as a resolver loses the factory its own options name:
`UseTypeInfoResolver(AppJsonContext.Default)` alone writes every value object as `{}`, or as `{"CountryCode":"FR"}` for
an `Iban` with a member of its own, and reads each back as its default, with no error. Without the factory in the
options, a value object written by hand is stored as `{"Value": "ABC", "IsDefault": false}` and read back as its
default. With `UseValueObjects()`, both write the bare values ([JSON](json.md#systemtextjson-source-generated)). A
custom `ISerializer` of the application's own is left alone.

## What translates

Every shape below answers what the same query over a member of the underlying type answers, and what the documents in
memory answer:

| Shape | SQL |
| --- | --- |
| `x.Iban == iban`, `!=`, `x.Iban.Value == "…"` | `d.data ->> 'Iban' = :p0` |
| `x.Iban.Value.StartsWith("FR")`, `EndsWith`, `Contains`, `Equals(…, OrdinalIgnoreCase)`, `string.IsNullOrEmpty` | `LIKE :p0`, `ILIKE :p0`, `IS NULL OR = ''` |
| `x.Quantity > q`, `x.Quantity.Value > 30` | `CAST(d.data ->> 'Quantity' as integer) > :p0` |
| `long`, `decimal`, `double`, `Guid`, `bool`, `char`, `sbyte`, `byte`, `short`, `uint`, `float`, `TimeSpan` | `CAST(… as bigint / decimal / double precision / uuid / boolean / text / smallint / oid / real / interval)` |
| `DateOnly`, `TimeOnly`, `DateTime`, `DateTimeOffset` | `public.mt_immutable_date(d.data ->> 'Due') < :p0`, and Marten's other immutable functions |
| `list.Contains(x.Country)`, `array.Contains(…)`, `x.Country.IsOneOf(…)`, `x.Country.In(…)` | `d.data ->> 'Country' = ANY(:p0)` |
| A `null` among the values of a `Contains` or an `IsOneOf` | `(… = ANY(:p0) or … is null)` |
| `x.Countries.Contains(code)`, `x.Countries.Any(c => c == code)`, over a list, an array or a set | `d.data @> :p0` |
| `x.Lines.Any(l => l.Tag == tag)`, `l.Score.Value == 30`, `l.Quantity > q` | `d.data -> 'Lines' @> :p0`, `jsonb_path_exists(…)` |
| `x.Backup == null`, `x.Backup == iban`, `x.MaybeQuantity > q`, `.HasValue` | `is null`, `= :p0`, `CAST(…) > :p0`, `is not null` |
| A construction of a generic value object, an `[EntityId]`, a value object written by hand | `d.data ->> 'Po' = :p0` |
| `OrderBy(x => x.Quantity)`, `Select(x => x.Quantity)`, `Select(x => x.Backup)`, `MaxAsync(x => x.Quantity)` | `order by CAST(…)`; the values, `null` included |
| `x.Id == id`, `x.Id.IsOneOf(ids)` over an identity | `d.id = :p0`, `d.id = ANY(:p0)` |
| `Patch<Order>(x => x.Quantity > q)` | runs |

A date or a time is read through the immutable function Marten's own member of that type reads it through, so that a
computed index serves the query. A `DateTimeOffset` is handed to the server in UTC, at whatever offset it was given, as
Marten's own member hands one; Marten's own `Contains` over `DateTimeOffset` values does not convert them, and Npgsql
refuses any offset but UTC there. `.Value` compares the stored text as it stands, with no rule applied:
`x.Iban.Value == "not an iban"` simply matches nothing.

What does not translate is refused rather than answered with no rows:

- `x.Iban.ToString()` and a member of the author's (`x.Iban.CountryCode`): "Marten does not (yet) support member".
- `x.Iban.Equals(iban)`: Marten reads no `Equals` over a value type; use `==`.
- `x.Quantity.CompareTo(q) > 0` translates, but hands the value object itself to the parameter, which fails when the
  query runs; use `>`.
- `Select(x => x.Iban.Value)` fails inside Marten's query handler; select `x.Iban` and read `.Value` in memory.
- `x.Grade.Value == 'B'` over a value object over `char`: C# compares it as an `int`, and PostgreSQL refuses an
  integer against text, as for a `char` property; compare `x.Grade == Grade.Create('B')`. In a child collection,
  `l.Grade.Value == 'B'` matches the lines holding `B`, where a `char` property's `l.Letter == 'B'` matches none, Marten
  placing the number; `l.Grade.Value > 'B'` matches none, as for the property.
- A value object over `ushort`, `ulong`, `Int128` or `UInt128`, which Marten maps to no column: refused as Marten
  refuses those types (`BadLinqExpressionException`).

## Duplicated fields

`Duplicate(x => x.Iban)` creates a column of the underlying type: `varchar(34)` for a value object over text with a
`MaxLength`, which PostgreSQL counts in characters, never more than the UTF-16 code units .NET counts, so the column
refuses no value the type accepts; `varchar` without one; `integer`, `numeric`, `date`, `uuid`, `boolean`, `oid` and
`text` for a `char`, as for the primitive. A `TSelf?` member duplicates too, a `NULL` for a value it does not hold. A
bulk insert writes them.

Marten compares a duplicated value-type field through the document, as it does without the package: only `StartsWith`,
the other string methods and full-text search read the column, and a `Contains` over one, which reads `d.iban =
ANY(:p0)`. A computed index (`Index(x => x.Amount)`, `Index(x => x.DueDate)`) serves `==` and `>`. Two limits are
Marten's own:

- `Select(x => x.Quantity)` over a duplicated field is refused before a row is read (`NotSupportedException: Marten does
  not know how to use result type …`, a `MissingMethodException` in a native image): Marten's duplicated field is not
  one of its value-type members, and builds its selector from the field's type, the value object's nullable type, which
  would read the column as JSON anyway. Select a member that is not duplicated, or the document.
- A duplicated `DateTimeOffset` at an offset other than UTC is refused by Npgsql on the write, as for the primitive.

## Compiled queries

Marten binds no parameter of a value type in a compiled query, and accepts a query type holding one only when the type
is in its list of value types: a type registered with Marten's own `RegisterValueType` makes every call answer with the
value of the first call, silently. The package keeps each value object out of that list, binds what it can read, and
lets Marten refuse the rest:

- **A collection of value objects is bound on every call**: `Contains` or `IsOneOf` over an array or a list of value
  objects held by a member of the query type, a field or a property, alone, twice, or beside members of other types.

  ```csharp skip
  public sealed class OrdersInCountries : ICompiledListQuery<Order>
  {
      public List<CountryCode> Countries { get; set; } = [];

      public Expression<Func<IMartenQueryable<Order>, IEnumerable<Order>>> QueryIs()
          => orders => orders.Where(x => x.Country.IsOneOf(Countries));
  }
  ```

- **A single value object is refused by Marten**, whatever the query does with it: compared with a member, with a
  member of a child collection, sought in a collection the document holds, its `Value` compared with another member or
  handed to a string method. Marten throws its own `InvalidCompiledQueryException` ("Members Iban Iban cannot be used
  as parameters to a compiled query") on every call, as it does without the package: it tells a compiled query's
  parameters apart by their values in the call it plans from, which no extension of Marten sees, and only a collection
  can be read off its member instead. Declare the member as the underlying type and compare the value object's `Value`
  with it, which Marten binds as any primitive:

  ```csharp skip
  public sealed class OrdersByIban : ICompiledListQuery<Order>
  {
      public string Iban { get; set; } = "";

      public Expression<Func<IMartenQueryable<Order>, IEnumerable<Order>>> QueryIs()
          => orders => orders.Where(x => x.Iban.Value == Iban);
  }
  ```

  Over a `short` or a `byte`, declare the member as an `int`: C# compares the `.Value` as one, and Marten binds no
  `short` member. A `TSelf?` member is refused by Marten as an `int?` is.
- A constant value object stays a constant of the plan, and so does a constant collection, unless the query type also
  holds a collection of that value object: the package cannot tell the constant collection from values read through a
  local, and refuses it.
- **Refused by the package**, with a `NotSupportedException` naming the member, since Marten would freeze them: values
  of a member read through a local of `QueryIs` (read them off the member, `Countries.Contains(x.Country)`), and a
  collection of nullable value objects, whose null test Marten would plan from the first call (declare a collection of
  the value object).

**Warning: a value object Marten takes for a document's identity answers every call of a compiled query with the first
call's value.** Marten lists such a type among its value types itself, once it has built that document's mapping
(before, it refuses the query type as any other), and then accepts a compiled query holding one: the identity compared
with the member, and every use no code of the package sees, answer with the first call's value; the package refuses
only its own comparisons of such a member, with a `NotSupportedException` naming the `.Value` form. Load the document by
its identity, or use a LINQ query, and keep the identity's type out of a compiled query's members. The package's tests
pin this behaviour, so that the warning goes the day Marten binds it.

Compiled queries generated by Marten.SourceGenerator are not covered.

## Identities

A value object over `Guid`, `string`, `int` or `long` works as a document's identity: load, `Where(x => x.Id == id)`,
`IsOneOf`, and a sequential `Guid` assigned when unset. Marten takes a struct for an identity when exactly one of its
public properties, static ones counted, has an identity type, so a value object with another one is none: an
`[EntityId]` (its `Prefix` and `Length`), an integer identifier with a public bound hook, and a value object with a
member of the author's of that type, as an IBAN's `CountryCode`. `LoadManyAsync` has no overload for a strong-typed
identity.

Under native AOT, declare each identity with `UseValueObjectId<TDocument, TSelf, TValue>()`, which keeps what Marten
reflects over in the native image:

```csharp skip
options.UseValueObjectId<Invoice, InvoiceId, Guid>();
```

Marten builds an identity it projects (`Select(x => x.Id)`) through the type's own `Create`, so a refused identity read
that way throws `ValueObjectException`, where a document read throws `ValueObjectJsonException`; both carry the code.

## Events and projections

Events are written and read by the same serializer: `QueryRawEventDataOnly<T>()` translates its LINQ, an inline snapshot
projection holding value objects is queried as any document, and `AggregateStreamAsync` reads through the rules. A
stored event the rules refuse fails `FetchStreamAsync` inside Marten's `EventDeserializationFailureException`, whose
code `ValueObjectErrors.TryGetCode` reads, and a raw event query with the converter's `ValueObjectJsonException`.

## Strict reads and patching

A document is always read through the generated converter, which validates: Marten's reads are strict by construction,
for a store more than one program may write. A value a query projects (`Select(x => x.Quantity)`,
`MaxAsync(x => x.Quantity)`) is read through the package, which refuses a stored value the rules refuse with the same
`ValueObjectJsonException` and code as a document read, so one `catch` covers it whichever way it is read; its message
names the type as C# does, `Reference<PurchaseOrder>` where the converter writes `Reference`.
Where the runtime supports no dynamic code, as under native AOT, Marten calls the package's read through reflection,
and the exception comes inside a `TargetInvocationException`, whose code `ValueObjectErrors.TryGetCode` reads all the
same. There is no trusted mode.

`Patch<Order>(id).Set(x => x.Iban, iban)` writes the bare, normalized value; a default instance its type refuses is
refused on `SaveChangesAsync`, with the rule's code. `Patch.Set("Quantity", 500)` and raw SQL write past the rules, and
the next read refuses with the code.

## Dates and times

PostgreSQL keeps microseconds: a value with 100-nanosecond ticks compares unequal to what was stored and projects
truncated, as the primitive does. A `DateTime` of UTC kind compared with a `timestamp without time zone` is refused by
Npgsql, as for the primitive.

## Native AOT

The package is AOT-compatible: it closes each value object through the descriptor's visitor, and keeps the select clause
Marten's member closes with `Activator.CreateInstance`. Marten's own publish warns for Marten, JasperFx, Weasel and
FastExpressionCompiler; none of the warnings names this library. In a native image:

- register a construction of a generic value object with its converter, and a value object written by hand with its
  schema, before the store is built, or teach either with `UseValueObject<TSelf, TValue>()`; one nothing registered is
  refused with a `NotSupportedException` naming those registrations
  ([Run-time lookup](runtime-lookup.md#generic-value-objects));
- declare each identity with `UseValueObjectId<TDocument, TSelf, TValue>()`;
- name every document type, `options.Schema.For<Invoice>()`, which Marten itself needs: it otherwise builds the mapping
  of a document type by closing a generic type at run time, and a native image throws `MissingMethodException`;
- serialize through a source-generated context's options,
  `UseSystemTextJsonForSerialization(AppJsonContext.Default.Options)`.

CI publishes a Marten application natively and runs it against PostgreSQL, beside the JIT run of the same script.

## Not covered

Marten.Newtonsoft, multi-tenancy, partitioning, NGram indexes, patch operations other than `Set`, async projections and
the daemon, HotChocolate.Data's Marten provider, Wolverine's Marten attributes over a value-object identifier, CHECK
constraints on a document table (Marten exposes none), and the first use of a value object nothing registered from two
threads at once.
