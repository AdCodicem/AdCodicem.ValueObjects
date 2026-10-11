---
title: Use with Entity Framework Core
sidebar_label: Entity Framework Core
slug: /how-to/ef-core
description: Map every value object of an assembly to its underlying column type in one call, collections of them as primitive collections, size columns from declared rules, translate `.Value` in queries to the column, choose when reads are validated, never store a value a type rejects, write the rules as CHECK constraints, generate keys the provider's way and refuse a Guid key left unset, compile the model, and work with JSON columns, raw SQL, other providers, bulk extensions, Always Encrypted and data masking.
---

# Use with Entity Framework Core

```bash
dotnet add package AdCodicem.ValueObjects.EntityFrameworkCore
```

## Map every value object at once

```csharp skip
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    => builder.ConfigureValueObjects(typeof(Iban).Assembly);
```

One call maps every value object declared in the assembly to its underlying column type, with a value converter
and a value comparer. It runs once, while the model is built; nothing happens per query or per row. Pass several
assemblies if the value objects live in more than one.

## Columns sized by the type

A value object declaring `MaxLength` also sizes its column: an `Iban` with `MaxLength = 34` becomes
`character varying(34)` on PostgreSQL and `nvarchar(34)` on SQL Server, instead of unbounded text. A `Guid` value
object lands in the provider's native GUID column. Change the rule on the type, and the next migration follows.

LINQ queries compare value objects as they would compare the underlying values:
`Where(a => a.Iban == iban)` becomes an ordinary `WHERE` on the column, with `iban` sent as a parameter.

That holds for a value captured in a variable. A value created inside the predicate is evaluated while the query is
translated and written into the SQL as a literal: `Where(a => a.Iban == Iban.Create("DE89370400440532013000"))`
gives `WHERE [a].[Iban] = N'DE89370400440532013000'`, and `Quantity.Create(3)` gives `= 3`. Each value then makes a
statement of its own in the plan cache, and on an Always Encrypted column the query fails, since a literal cannot be
encrypted. Hoist the value into a variable, or wrap it in `EF.Parameter(...)`:

```csharp skip
var iban = Iban.Create("DE89370400440532013000");
var accounts = await db.Accounts.Where(a => a.Iban == iban).ToListAsync();
```

A static property, `Currency.Eur`, is sent as a parameter already.

## The value in a query

A comparison between value objects translates, but nothing a query reads of the value one holds:
`x.Iban.Value.StartsWith("FR")`, `x.Quantity.Value > 2` and `x.Total.Value * 2 > 10` fail, as "could not be
translated". The cast that does translate, `((string)(object)x.Iban).StartsWith("FR")`, becomes
`CAST([o].[Iban] AS nvarchar(max)) LIKE N'FR%'`, for which SQL Server can no longer seek the index on the column: it
scans it.

`UseValueObjectQueries()`, on the options of the context, translates `.Value` to the column itself:

```csharp skip
services.AddDbContext<ShopContext>(options => options
    .UseSqlServer(connectionString)
    .UseValueObjectQueries());
```

A context configuring itself calls it in `OnConfiguring`, after the provider:
`optionsBuilder.UseSqlServer(connectionString).UseValueObjectQueries()`.

`x.Iban.Value.StartsWith(prefix)` then becomes `WHERE [o].[Iban] LIKE @prefix_startswith`, which the index serves: on
SQL Server 2022, over 100,000 rows, an index seek reading 5 pages where the cast scanned 888. The column keeps the type
the rules of its value object gave it, so a parameter compared with it is declared as the column is,
`DECLARE @text nvarchar(34)` for `x.Iban.Value == text`, and `char(25)` for the fixed-width column of an
[entity identifier](#public-identifiers), whose `StartsWith` pattern is a `varchar` of that length. Comparisons,
arithmetic, `OrderBy`, `GroupBy`, string functions, `ExecuteUpdate`, `ExecuteDelete` and compiled queries take `.Value`
as they take a property of the underlying type. It translates in an optional value object, `x.Previous!.Value.Value`,
inside a [JSON column](#complex-types-and-json-columns), in an element of a
[collection](#collections-of-value-objects), `x.Ibans.Any(i => i.Value.StartsWith(prefix))`, and in a construction of
a [generic value object](#generic-value-objects).

Only a property this package maps is translated, through `ConfigureValueObjects`, `HasValueObjectConversion`,
`ConfigureEntityIds` or `HasEntityIdConversion`, including where Entity Framework Core composes its converter with its
own conversion of the underlying type, a `ushort` stored in an `int` column, say. A property mapped through a converter
of your own, as a [128-bit value object](#128-bit-value-objects) must be, keeps Entity Framework Core's refusal: what
its column holds may not be the value. So does this package's converter composed with a conversion of your own,
`ComposeWith`, except over a type the provider converts itself, a `ushort` or a `Uri`, where it cannot be told from
Entity Framework Core's composition: compose none there.

A column of a table is read as it stands. A column an optional navigation joins in, an element of a collection, and an
expression such as a `COALESCE`, are read through a `CAST` to their own store type, `CAST([i].[value] AS nvarchar(34))`,
which Entity Framework Core 11 leaves out of the SQL. On PostgreSQL, a `LIKE` prefix uses a B-tree index only under the
`C` collation or with `varchar_pattern_ops`, as it does on a column of text; an equality uses the index whatever the
collation.

`.Value` reads what the column holds, in a projection too: `Select(x => x.Iban.Value)` returns the text as stored, which
a [strict context](#validation-on-read) does not validate, since it validates the value objects it materializes. Project
the value object to have it validated.

Nothing else changes. `ToString()`, which follows the formatting hooks, and `EF.Property` still fail, and so does
`.Value` of an element of a list passed as a parameter, `ibans.Any(i => i.Value.StartsWith(prefix))`, which has no type
mapping yet when it is translated: compare the value objects there, or hoist the values,
`var texts = ibans.Select(i => i.Value).ToList();`. A conversion operator the value object declares, `(string)x.Iban`,
stays the cast to the unbounded type above.

The translation runs once per query shape, which Entity Framework Core caches: nothing more runs per execution. It
works on a relational provider, and does nothing on any other.

Entity Framework Core adds the translator to the service provider it builds for the context, never to one you hand it
with `UseInternalServiceProvider`. A context started on such a provider throws an `InvalidOperationException` naming
both calls, rather than leave `.Value` untranslated, unless that provider holds the services of the extensions of the
context's options, added through `IDbContextOptionsExtension.ApplyServices` as Entity Framework Core adds them to a
provider it builds.

## Validation on read

Materializing a row does **not** validate the value again: it uses `CreateUnchecked`. The read path is the
hottest one in most applications, and it reads values this same application validated when it wrote them.

For a table another system also writes to, turn validation back on:

```csharp skip
builder.ConfigureValueObjects(strict: true, typeof(Iban).Assembly);
```

It then costs one normalization and validation per materialized value. A value the domain would refuse fails
the query with the value object's `ValueObjectException`; one it would only normalize comes back normalized.
On a key, that has a consequence: a row stored as `fr76 3000 …` is tracked under `FR763000…`, which is not the key
the table holds. An update or a delete of that row through the strict context matches no row, and `SaveChanges`
throws a `DbUpdateConcurrencyException`, as it does for a row another writer deleted. Normalize such keys in the
table before relying on strict reads to write them back.

Such a table can also refuse a bad row when it is written, with [CHECK constraints from the
rules](#check-constraints-from-the-rules): a constraint stops a bad row when it is written, whoever writes it; a strict
read catches one that was already there, or that was written while the constraint was off. Use both.

`strict` is read once per context type, not per instance. Entity Framework Core builds the model the first time a
context type is used and caches it for every later instance, so a context whose constructor chooses `strict` from an
argument gets whichever model was built first, and the other value does nothing. Give strict reads a context type of
their own, which may derive from the other, or a custom `IModelCacheKeyFactory` that puts the choice in the key.

## Validation on write

An entity whose value object was never set holds an instance that never went through `Create`, and so the default
value: an empty `Iban`, a `Guid.Empty` customer identifier. Stored as it stands, it would come back from every later
read as an instance holding a value its own rules refuse, since the read trusts the column. The converters refuse it
instead:

- on a property of the value object's type, `SaveChanges` throws a `DbUpdateException` whose inner exception is the
  value object's `ValueObjectException`, naming the type and the rule, and nothing is written;
- on an optional property, `Iban?`, the column takes a `NULL`, and stores one.

Over a value type, nothing tells the default from a constructed zero, so the converter validates it: a type that
accepts its zero, an `Amount` with a minimum of 0, stores it, and an identifier that must never be `Guid.Empty` says
so with a validator, as the [`CustomerId` of the tutorial](../tutorials/request-to-database.md#the-domain) does.
Any other instance went through `Create`, and is written without being validated again. The strict converters write
the same way.

A primary key of one property over a `Guid` value object never gets that far under `ConfigureValueObjects`: left unset,
its entity is refused when it is added, whatever the type's rules, with an `InvalidOperationException` carrying
`value_object.required` ([a Guid key left unset](#a-guid-key-left-unset)).

`dotnet ef dbcontext optimize` converts the default of each property to write it into the compiled model. That writes
no column, and runs under `EF.IsDesignTime`, so the converters let it through.

## CHECK constraints from the rules

Only a column size reaches the database by default. Another writer, an ETL job, a migration script, a legacy
application, can still store `GBP` in a closed `CountryCode` or `0` in a `Quantity` bounded from 1. On request, the
rules become CHECK constraints, and the database refuses such a row whoever writes it, patterns included [on a second
request](#patterns):

```csharp skip
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    => builder
        .ConfigureValueObjects(typeof(Iban).Assembly)
        .AddValueObjectCheckConstraints();
```

It is off unless called, so no existing migration changes. Each column one of the package's converters maps, through
`ConfigureValueObjects`, `ConfigureEntityIds`, `HasValueObjectConversion` or `HasEntityIdConversion`, gets a constraint
per rule its value object declares, named `CK_{table}_{column}_{rule}`:

| Rule | SQL Server | PostgreSQL | SQLite |
| --- | --- | --- | --- |
| `MinLength` | `DATALENGTH([Iban]) / 2 >= 15`, or `DATALENGTH([Iban]) >= 15` on a column that is not Unicode | `octet_length("Iban") >= 15` | `length(CAST("Iban" AS BLOB)) >= 15` |
| `Minimum`, `Maximum` | `[Quantity] >= 1` | `"Quantity" >= 1` | `"Quantity" >= 1` |
| A closed set | `[Country] IN (N'FR', N'BE', N'LU')` | `"Country" IN ('FR', 'BE', 'LU')` | `"Country" IN ('FR', 'BE', 'LU')` |

A migration picks them up, as it picks up a column size: change a rule on the type, and the next migration drops the
constraint and adds the new one. A column that takes a `NULL` wraps each check, `"Quantity" IS NULL OR ("Quantity" >=
1)`, so that a `NULL` passes.

### Never stricter than the type

A constraint stricter than the type would fail a `SaveChanges` the application validated, which is worse than no
constraint. So each rule is written only where the database refuses no value the type accepts, and is left out
elsewhere:

- A length is counted in bytes where the database cannot count UTF-16 code units, as .NET does. A character never takes
  fewer bytes than code units, so the check is exact for ASCII text, an IBAN or a country code, and laxer for other
  text. `LEN` would drop trailing spaces, and `char_length` would count a character outside the Basic Multilingual
  Plane once, where .NET counts two.
- A bound is written as the provider writes a literal of the underlying type, and compared as .NET compares: a `char`
  under a binary collation, by code point; a `float` as the exact `double` the column holds; an instant with its offset,
  and on PostgreSQL an instant in a `timestamp with time zone`, as the UTC instant.
- A bound is left out where the column does not order values as .NET does: on SQLite, a `decimal`, a `TimeSpan` and a
  `DateTimeOffset`, which it stores as text, and a `ulong`, which it stores as a signed integer; on SQL Server, a
  `TimeSpan` outside `[0, 1 day)`, which `time` holds without its days, and any bound on a `datetime` or
  `smalldatetime` column; on PostgreSQL, a `char` minimum at or below a space, which `character(1)` drops; everywhere,
  a `decimal` bound finer than the column's scale, SQL Server's default `decimal(18,2)` included, an instant finer
  than the column keeps, 100 ns in SQL Server's `datetime2` unless its precision says less, a microsecond on
  PostgreSQL, and a `float` or `double` bound at an infinity.
- A closed set of text is written only when the type stores no other spelling of a known value, by the comparison its
  schema states, `Schema.Comparison`: one compared `Ordinal`, the default, gets its `IN`; one compared as a culture
  compares gets none, since the culture takes other spellings for a known value, some of the same length, `KʳW` for
  `KRW` or the Kelvin sign for `K`, which the type stores as written; one looked up with `OrdinalIgnoreCase` and
  normalized to upper case gets `IN ('KG', 'L')`, one without a normalizer none, since it stores `kg` as it was written,
  and neither does one normalized to lower case whose known value holds a Greek `μ`, since it also takes the micro sign
  `µ`, which shares the upper case of `μ`, and stores it as it was written. A value object written by hand states its
  comparison in its schema: one stating none is tried with the other cases of each known value and a trailing soft
  hyphen, fewer spellings than a culture's comparison takes. The check compares as the
  column's collation does: under SQL Server's default collation, which ignores case, `IN (N'FR', …)` also takes `fr`,
  laxer than the type.
- A closed set of another type gets its `IN` where the column compares its values as .NET does: numbers, a `char`, a
  `bool`, a `Guid`, dates, times, durations and instants. A closed set of floating-point numbers holding `NaN` or an
  infinity gets none, and neither does one of instants with their offset on SQLite, nor one over a reference type other
  than text, such as the `Uri` a value object written by hand may hold, whose equality ignores a fragment that its
  column's text keeps.
- A rule only code checks, the checksum of an `IValueObjectValidator<T>`, has no SQL form and stays in the
  application. A pattern is [a separate opt-in](#patterns).

Columns mapped to JSON, the elements of a [primitive collection](#collections-of-value-objects), a view, a property
split into another table with `SplitToTable`, and a column mapped by a converter of the application's own get none.
The columns an owned type or a complex type shares a table with get theirs, named after their column,
`CK_Orders_Shipping_City_Zip_MinLength`; an owned type in a table of its own gets them in that table. In a
table-per-concrete-type hierarchy, a type other types derive from gets none: Entity Framework Core would copy its
constraints into their tables under the same name, which SQL Server refuses; a type with no derived type constrains the
columns it inherits too, but for those of a complex property it inherits, which Entity Framework Core 10 maps to no
column of its table. A column two types of a table-per-hierarchy share is constrained only when both map the same value
object there, since the constraint of one would also judge the rows of the other. SQL Server, PostgreSQL and SQLite are
known; another provider gets no constraint.

A name longer than the provider takes, 63 bytes on PostgreSQL and 128 characters on SQL Server, keeps its start, cut as
short as it needs, then the rule, whole, and eight hexadecimal digits of the hash of its table and its column:
`CK_{table}_{column}_{rule}_{hash}`. So does a name another check constraint of the model already holds, one you
declared with `HasCheckConstraint`, in the same table, or anywhere in the schema on SQL Server; and a name another table
would take too, on SQL Server, which keeps a constraint name per schema and compares it ignoring case:
`CK_a_b_C_Maximum` for column `C` of table `a_b` and column `B_C` of table `a`, or a complex property's column
`Line_Score` in `orders` and a table `orders_Line`. Every name ends with its rule, or with its rule and a hash.

`Rules` leaves a family out:

```csharp skip
builder.AddValueObjectCheckConstraints(checks => checks.Rules = ValueObjectChecks.Lengths | ValueObjectChecks.KnownValues);
```

The options are read once per context type, as `strict` is: give a context that needs other options a type of its own,
which may derive from the other and call `AddValueObjectCheckConstraints` again, since a second call replaces the first.

### Patterns

A pattern becomes a constraint only when the options name the dialect of regular expressions the database runs, which
the model cannot tell, the compatibility level of a SQL Server database least of all:

```csharp skip
builder
    .ConfigureValueObjects(typeof(Iban).Assembly)
    .AddValueObjectCheckConstraints(checks => checks.Patterns = ValueObjectPatternDialect.PostgreSql);
```

| Dialect | The constraint of an IBAN |
| --- | --- |
| `ValueObjectPatternDialect.PostgreSql` | `"Iban" COLLATE "C" ~ '^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}\n?$'` |
| `ValueObjectPatternDialect.SqlServer2025` | `REGEXP_LIKE([Iban], N'^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}\n?$')` |

`REGEXP_LIKE` needs SQL Server 2025 and a database at compatibility level 170, which a new database of SQL Server 2025
is: at an earlier level, or on SQL Server 2022, the migration fails with "'REGEXP_LIKE' is not a recognized built-in
function name". On PostgreSQL the constraint matches under the `C` collation: a column under a nondeterministic
collation, the case-insensitive one recommended for a key compared ignoring case, would otherwise fail every write, since
PostgreSQL runs no regular expression under one. A dialect that does not match the provider at hand writes no pattern:
PostgreSQL's under SQLite, in tests, writes the other rules alone. SQLite has no regular expressions.

The pattern is translated, not copied, so that the database matches what .NET matches: groups become non-capturing,
lazy quantifiers greedy, which changes which match is found, never whether one is, a character outside printable ASCII
is escaped as each dialect reads it, and a `$` or a `\Z` ending the pattern, or one of its top-level alternatives, is
written `\n?$`, since .NET also matches it before a final line feed and both databases match `$` at the end alone. What
either database would read apart from .NET is refused:

| Construct | Why | Instead |
| --- | --- | --- |
| `\d`, `\w`, `\s`, `\p{…}` | .NET reads them over Unicode, the database over ASCII or its locale: `\d` matches `٣` in .NET | `[0-9]`, or the characters listed in a class |
| `.`, `[^…]`, `\D`, `\W`, `\S` | .NET matches one UTF-16 code unit, the database a whole character | a class listing what is accepted |
| `$` or `\Z` anywhere but at the end of the pattern or of a top-level alternative, inside a group included | .NET also matches before a final line feed | the anchor moved after the group, `^(?:a\|b)$` rather than `^(?:a$\|b$)`; `\z` means the end in .NET too, but OpenAPI clients, which read ECMA-262, do not know it |
| `\b`, `\B`, a class subtraction, a lookaround, a backreference, an atomic or conditional group, an inline option | no equivalent, or another meaning | a pattern without them |
| A count above 255 on PostgreSQL, 1000 on SQL Server, or repetitions matching more than 1000 characters once written out | the database refuses them as too complex when it first checks a value, failing every write | smaller counts |
| Any `RegexOptions` of the hook's regular expression but `CultureInvariant`, `Compiled`, `ExplicitCapture` and `NonBacktracking`: `IgnoreCase`, `Multiline`, `Singleline` and `IgnorePatternWhitespace`, which `VO0025` warns of, and `ECMAScript` and `RightToLeft`, which it does not | the database reads the text as .NET reads it with no option, and would refuse what the type accepts | a pattern that means the same without them, `[A-Za-z]` for `IgnoreCase` |

Naming a dialect checks the pattern of each column the convention constrains when the model is built, under SQLite as
under the database it names, and from `dotnet ef migrations add` and `dotnet ef dbcontext optimize` too: one refused
fails the model with an `InvalidOperationException` naming the value object, the construct or the option, and the
dialect. An email
address's `^[^@\s]+@[^@\s]+\.[^@\s]+$` is such a pattern. Leave that one value object without a pattern constraint,
its other rules written as before, and its pattern checked by the application alone:

```csharp skip
builder.AddValueObjectCheckConstraints(checks =>
{
    checks.Patterns = ValueObjectPatternDialect.PostgreSql;
    checks.ExcludePattern<EmailAddress>();
});
```

A fixed-length column, an entity identifier's `char(n)`, gets no pattern: SQL Server pads it with spaces, which the
pattern would see, and PostgreSQL drops them before it matches. A pattern a value object written by hand states in its
schema alone, with no hook, is translated and checked from its text alone: there is no regular expression whose options
could be checked.

### Rows already in the table

Adding a constraint to a table that already holds a row the rule refuses fails the migration: "The ALTER TABLE
statement conflicted with the CHECK constraint" on SQL Server, `23514` on PostgreSQL. That is the constraint doing its
job, but the row has to be corrected first. Rows an earlier version stored for a value object that was never set, `''`
or `0`, are among them. Count them, constraint by constraint, from the model the constraints are written in, before the
migration that adds them:

```csharp skip
var sql = db.GetService<ISqlGenerationHelper>();
foreach (var table in db.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables)
{
    foreach (var check in table.CheckConstraints)
    {
        var count = $"SELECT COUNT(*) AS {sql.DelimitIdentifier("Value")} FROM {sql.DelimitIdentifier(table.Name, table.Schema)} WHERE NOT ({check.Sql})";
        var refused = await db.Database.SqlQueryRaw<int>(count).SingleAsync();
        Console.WriteLine($"{check.Name}: {refused} row(s) refused");
    }
}
```

A check passes a `NULL`, and `NOT` of an unknown is unknown, so the count is what the constraint would refuse. The
relational model lists each constraint once, in the table it lies in: an entity type's `GetCheckConstraints()` also
lists those of its base type, which a table-per-type hierarchy keeps in another table, and a table-per-hierarchy in the
same one, so that a query built from it would fail on a column the table lacks, or count a constraint once per derived
type.

### When a constraint fires

The application's own writes pass: the value objects it writes went through their rules. A constraint fires for
another writer, and for an invalid instance a lax read let in and the application writes back. Through Entity
Framework Core, `SaveChanges` then throws a `DbUpdateException` around the provider's error, SQL Server's 547,
PostgreSQL's 23514 or SQLite's 19, which names the constraint, and so its rule, and its column unless the name was
shortened, but carries no rule code. It complements `strict: true` rather than replace it: a constraint stops a bad row when it is written, a strict
read catches one stored before the constraint existed.

The constraints are part of the model a migration, `EnsureCreated` or `GenerateCreateScript` is made from, never of a
[compiled model](#compiled-models), which holds none: `EnsureCreated` on a context using one builds the model that does.
Nothing runs per query or per row.

## One property, differently

To depart from the convention for a single property:

```csharp skip
modelBuilder.Entity<BankAccount>()
    .Property(account => account.Iban)
    .HasValueObjectConversion<Iban, string>(strict: true);
```

Prefer the convention everywhere else. And do not write `HasConversion` by hand for a value object: you would
lose the generated comparer, and with it correct change tracking for a type whose comparison is not ordinal.

## Generic value objects

A generic value object, `Reference<TOwner>` or `Catalog<TItem>.Stock`, has no single type the convention could map
up front. `ConfigureValueObjects` maps its generic definition instead, and each property holding a construction,
`Reference<PurchaseOrder>` say, gets the converter, the comparer and the column length closed over that construction,
as the model meets it. `strict: true` applies to them as to any other value object, and a property mapped explicitly
with `HasValueObjectConversion<Reference<PurchaseOrder>, string>()` keeps what it was mapped with.

## Collections of value objects

A property holding a collection of value objects, `List<Iban>`, `Quantity[]` or `IReadOnlyList<Iban?>`, is mapped as a
primitive collection, in an entity type, an owned type or a complex type, a complex collection included. Each element
gets the converter, the comparer and the length of its value object, a construction of a generic one included:

```csharp skip
public sealed class Account
{
    public int Id { get; set; }

    public List<Iban> Ibans { get; set; } = [];

    public Quantity[] Limits { get; set; } = [];
}
```

PostgreSQL stores such a collection as an array of the element's column type, `character varying(34)[]` for an IBAN,
`integer[]` for a quantity over `int`. SQL Server and SQLite store a JSON array, in `nvarchar(max)` and in `TEXT`, and
a query reads its elements as the value object's column type: `OPENJSON([a].[Ibans]) WITH ([value] nvarchar(34) '$')`
on SQL Server, `json_each` on SQLite. At compatibility level 170, which SQL Server 2025 offers, Entity Framework Core 10
makes the column `json` (from a script, not run).

A query over the elements translates on all three, `Contains`, `Any`, `Count` or an index: on PostgreSQL through the
array, `= ANY`, `unnest`, `cardinality` and a subscript; on SQL Server through `OPENJSON` and `JSON_VALUE`; on SQLite
through `json_each`, `json_array_length` and `->>`. Hoist the value into a variable, as for a
[column](#columns-sized-by-the-type):

```csharp skip
var iban = Iban.Create("BE68539007547034");
var accounts = await db.Accounts.Where(a => a.Ibans.Contains(iban)).ToListAsync();
```

Only a property Entity Framework Core would map itself becomes one: a property with a public getter and a setter of any
accessibility, its own or the one a base class declares for it. A write-only property, one whose getter is not public
and an indexer stay out of the model, and so does a read-only property over a backing field, as an aggregate exposes its
collection, as it always did. Declare it, and the convention configures its elements:

```csharp skip
modelBuilder.Entity<Account>().PrimitiveCollection(account => account.Ibans);
```

`strict: true` validates each element read, as it validates a column. On write, an element the value object rejects,
which only an instance that never went through `Create` can hold, is treated as a [column](#validation-on-write) is:

- in a `List<Iban>`, `SaveChanges` throws the `DbUpdateException` around the `ValueObjectException`, and nothing is
  written;
- in a `List<Iban?>`, a collection of an optional value object over text, the element is stored as a JSON `null`, a
  `NULL` in a PostgreSQL array, as an optional column stores a `NULL`;
- in a `List<Quantity?>`, a collection of an optional value object over a value type, the element is refused as in a
  `List<Quantity>`. Entity Framework Core's JSON writer throws a `NullReferenceException` on the `null` a converter hands
  it for such an element, so the element takes the converter of the value object, which refuses with the
  `ValueObjectException` instead.

A `null` element is stored as `null`, in either collection of an optional value object.

The convention leaves alone a property marked `[NotMapped]` or ignored, a whole collection you convert yourself, with
`HasConversion` or with `HaveConversion` in `ConfigureConventions`, an element whose converter you set, through
`ElementType(e => e.HasConversion<…>())`, and a collection of [128-bit value objects](#128-bit-value-objects), which it
maps nowhere. It reads the element off the `IEnumerable<T>` a property's type implements, but Entity Framework Core
tracks only an array or a list: a `HashSet<Iban>` builds the model, and is refused once tracked, `The type
'HashSet<Iban>' cannot be used as a primitive collection because it is not an array and does not implement
'IList<Iban?>'`, as a `HashSet<string>` is, and a property typed `IReadOnlyCollection<Iban>` or `IEnumerable<Iban>` must
hold a list or an array.

An [entity identifier](#public-identifiers) in a collection is mapped as the value object it is: it takes its length,
not the fixed width, the non-Unicode type or the collation `ConfigureEntityIds` gives a column. `MaxLength` is the one
rule that reaches the database for an element, where the provider gives the element a type of its own.

Collections were checked on PostgreSQL, SQL Server and SQLite, not on Azure Cosmos DB nor on
MongoDB.EntityFrameworkCore.

## 128-bit value objects

Entity Framework Core maps neither `Int128` nor `UInt128`, on any provider, so the convention leaves a value object
over either alone: the column it lands in is yours to choose, with a converter of your own. Give it the comparer the
convention would have given it, so that change tracking compares the way the value object does:

```csharp skip
builder.Properties<LedgerBalance>()
    .HaveConversion<LedgerBalanceToDecimal, ValueObjectComparer<LedgerBalance>>()
    .HavePrecision(38, 0);

internal sealed class LedgerBalanceToDecimal() : ValueConverter<LedgerBalance, decimal>(
    balance => (decimal)balance.Value,
    value => LedgerBalance.Create((Int128)value));
```

A numeric column sorts and compares as numbers do, but ADO.NET carries it through `System.Decimal`, which holds
about ±7.9 × 10²⁸: a value beyond that can be neither written nor read back. A text column holds the whole range,
and sorts and compares as text does, so `9` comes after `10`.

`Properties<LedgerBalance>()` configures a property, not the element of a collection: a `List<LedgerBalance>` fails model
building until you declare it as a primitive collection, with the converter and the comparer of its elements:

```csharp skip
modelBuilder.Entity<Ledger>()
    .PrimitiveCollection(ledger => ledger.Balances)
    .ElementType(element => element.HasConversion<LedgerBalanceToDecimal, ValueObjectComparer<LedgerBalance>>());
```

## Value objects as keys

A value object makes a perfectly good key, primary or foreign. For a string key declared with
`Comparison = StringComparison.OrdinalIgnoreCase`, set a case-insensitive collation on the column, or the
database and the application will disagree about which values are equal.

Entity Framework Core generates a key of a `Guid`, an `int` or a `long` when an entity is added, but it decides so from
the key's own type: a key typed as a value object is generated by nothing. Have it generated, or assign it in the
domain; a `Guid` key left unset is refused.

### Generating a Guid key

```csharp skip
modelBuilder.Entity<Shipment>()
    .Property(shipment => shipment.Id)
    .HasValueObjectKeyGeneration();
```

The key is generated when the entity is added, with GUIDs the database keeps in order. On SQL Server they are SQL
Server's sequential ones, as Entity Framework Core generates for a `Guid` key there, which a `uniqueidentifier` sorts in
the order they were generated, so that a clustered index takes each new row at its end. On every other provider they are
version 7 ones, as Npgsql generates for PostgreSQL, where Entity Framework Core would generate random ones on SQLite and
elsewhere. `ValueGeneratedOnAdd()` alone does not do that: Entity Framework Core then gives the key its random
generator, through the converter, on every provider.

A key the domain set is kept; one created from `Guid.Empty`, which equals the default, is generated over. The GUID goes
through the value object's `Create`, so a rule it breaks, a legacy identifier that must be a version 4 GUID say, fails
`Add` with the value object's `ValueObjectException` rather than reach the column. `Attach` and `Update` of an entity
whose key is unset add it, as for a `Guid` key. The key is generated in the application: a database default on the
column is not used. It needs no `ConfigureValueObjects`, and works beside `HasValueObjectConversion`. The migration that
follows is empty: only the snapshot records `.ValueGeneratedOnAdd()`.

A GUID the domain mints, the `New()` of [Domain behaviour](domain-members.md#a-factory-of-your-own), is a version 7
one. PostgreSQL sorts it by the time it was minted, but SQL Server sorts a `uniqueidentifier` by its last six bytes
first, so on SQL Server those keys land all over a clustered index: generate the key there, or cluster the table on
another column.

### A Guid key left unset

Under `ConfigureValueObjects`, an entity added without its `Guid` key is refused at once, before anything is written,
rather than stored with `Guid.Empty`, which a value object without a rule against it accepts, until the next one fails
on the duplicate key:

```text
InvalidOperationException: 'Shipment' was added without its key: 'Shipment.Id' holds default(ParcelId), Guid.Empty,
and nothing generates it. Assign the key in the domain, or have it generated with HasValueObjectKeyGeneration().
```

The exception carries `value_object.required` in its `Data`, which `ValueObjectErrors.TryGetCode` reads, as the
exception of every integration carries the code of what it refused ([the code in an
exception](../reference/errors.md#the-code-in-an-exception)).

That concerns a primary key of one property over a `Guid` value object, generated, generic or written by hand, which is
no foreign key and which nothing generates, an owned collection item's key of its own included. A composite key, an
alternate key, a foreign key (a dependent sharing its principal's key, an owned reference type), and a key with a
database default or a generator of your own are not refused. A key created from `Guid.Empty` equals the default, and is
refused too; an identifier whose validator refuses `Guid.Empty`, as the
[`CustomerId` of the tutorial](../tutorials/request-to-database.md#the-domain) does, is now refused at `Add`, with this
exception, rather than at `SaveChanges`, with a `DbUpdateException` around a `ValueObjectException`: both carry
`value_object.required`.

The refusal comes from a value generator the convention gives the key, which Entity Framework Core asks for a value
whenever an entity whose key is unset starts being tracked as new:

- `Add`, and `Attach`, `Update` and `Remove`, which take such an entity for a new one, where they used to track it with
  `Guid.Empty`;
- `DetectChanges`, and so `SaveChanges`, for a new entity found through a navigation;
- a dependent sharing its principal's key, added while the principal is not tracked, through the principal's key.

A refusal inside a graph leaves the entities attached before the refused one tracked, as any exception Entity Framework
Core meets while it attaches a graph does: call `ChangeTracker.Clear()` after catching it, rather than save the same
context, which would write the graph without the refused entity.

Since the key now has a generator, Entity Framework Core also takes a dependent whose key holds a foreign key to it for
a new one when that key is left at its default for the navigation to fill in, as it does for a `Guid` key: a dependent
sharing its principal's key, or one keyed by its principal's key and a number. `Attach` or `Update` of an aggregate
already stored then tracks such a dependent as added, without refusing it, and `SaveChanges` fails on the duplicate key,
where the dependent used to be tracked unchanged. Set the dependent's foreign key before attaching it, set its state by
hand afterwards, `context.Entry(dependent).State = EntityState.Unchanged`, or opt the principal's key out, below.

Setting an entry's state by hand to `Modified`, `Unchanged` or `Deleted` asks nothing. The key stays generated by
nothing, so neither the column nor a migration changes, and a row already stored with `Guid.Empty` is still read,
updated and deleted through a tracked query. `HasData` seeding a key with `Guid.Empty` fails model building with Entity
Framework Core's own message, as it does for a `Guid` key: "The seed entity for entity type 'Shipment' cannot be added
because a default value was provided for the required property 'Id'".

To store `Guid.Empty` on purpose, a legacy row attached as it is or a seed, or to attach aggregates whose dependents take
their keys from a navigation as before, opt the key out:

```csharp skip
modelBuilder.Entity<Shipment>().Property(shipment => shipment.Id).ValueGeneratedNever();
```

`[DatabaseGenerated(DatabaseGeneratedOption.None)]` on the property does the same. Neither generation nor the refusal
runs where the change tracker does not: a bulk insert, `ExecuteUpdate` or raw SQL.

### Generating an integer key

`HasValueObjectKeyGeneration()` is declared for a key over a `short`, an `int` or a `long` too, and a call picks the
one for its key's type; a value object over any other type is a compile error. The key becomes an identity column on
SQL Server and PostgreSQL, as a key of the integer itself is by convention there, and the row identifier on SQLite:

| Underlying type | SQL Server | PostgreSQL | SQLite |
| --- | --- | --- | --- |
| `short` | `smallint IDENTITY` | `smallint GENERATED BY DEFAULT AS IDENTITY` | `INTEGER PRIMARY KEY` |
| `int` | `int IDENTITY` | `integer GENERATED BY DEFAULT AS IDENTITY` | `INTEGER PRIMARY KEY` |
| `long` | `bigint IDENTITY` | `bigint GENERATED BY DEFAULT AS IDENTITY` | `INTEGER PRIMARY KEY` |

Until `SaveChanges`, the change tracker holds a temporary value, negative, and the entity's property its default; the
key the database generates then takes its place, read through the converter. The temporary value is never written, so it
is created unchecked: a type counted from one accepts it, in a strict context too, where `ValueGeneratedOnAdd()` alone
would fail `Add` on Entity Framework Core's own temporary value. A key the domain set, other than zero, is kept: SQL
Server refuses to insert it into an identity column while `IDENTITY_INSERT` is off, and PostgreSQL, whose identity is
`BY DEFAULT`, stores it. On SQLite the key is the row identifier, which reuses the highest key once that row is deleted,
and a 64-bit integer, which a `short` key outgrows past 32,767: a key of the integer itself is `AUTOINCREMENT` there by
convention, but a value-object key stays the plain row identifier unless the property also calls SQLite's
`UseAutoincrement()`.

Only a relational database generates such a key. On another provider, Entity Framework Core's in-memory database for
one, nothing would replace the temporary value, which the database would store as the key, a value the type may refuse:
adding an entity without its key throws an `InvalidOperationException` there instead, and the key has to be assigned in
the domain.

`byte`, `sbyte`, `ushort` and `uint` are left out, since SQL Server or PostgreSQL would number them in a wider column,
which overflows the type; `ulong`, `char` and `decimal` cannot be an identity. To number keys with HiLo or a sequence,
use the provider's own API, `UseHiLo()`, instead: the generator `HasValueObjectKeyGeneration()` sets would take
precedence over it (not run).

Turning generation on for a table that exists is a migration:

- SQL Server cannot alter a column into an identity column, and the migration fails with "To change the IDENTITY
  property of a column, the column needs to be dropped and recreated". Write it by hand: create the table again with
  the identity, copy the rows under `SET IDENTITY_INSERT … ON`, and swap the tables; or keep assigning keys on that
  table.
- PostgreSQL adds the identity, which starts at one, below the keys the table already holds: add
  `ALTER TABLE … ALTER COLUMN "Id" RESTART WITH …`, past the highest key, to the migration.
- SQLite has nothing to change: the column was already the row identifier.

An integer key nothing generates is not refused, unlike a `Guid` one: it is written as it stands, so a type counted from
one refuses its zero at `SaveChanges`, [as on write](#validation-on-write), and a type that accepts zero stores it, as a
key the domain chose. `HasData` seeding a key of zero still builds.

## Compiled models

`dotnet ef dbcontext optimize` turns the model into code that names each converter and comparer. Every value object
the conventions map compiles there, whatever its underlying type, generic or not, optional or not, identifiers
included. An optional property, `Quantity?`, is compared by `NullableValueObjectComparer<Quantity>`: given the comparer
of the value object, Entity Framework Core would wrap it in code that does not compile. A property you map with a
converter of your own, a [128-bit value object](#128-bit-value-objects) for one, needs the same for its optional form:

```csharp skip
builder.Properties<LedgerBalance?>()
    .HaveConversion<LedgerBalanceToDecimal, NullableValueObjectComparer<LedgerBalance>>();
```

The elements of a [collection](#collections-of-value-objects) compile there too, with their converters and comparers,
an optional element compared by `NullableValueObjectComparer<T>` as an optional property is. So do the generators of
[keys](#value-objects-as-keys): `new ValueObjectKeyGeneratorFactory<ParcelId, Guid>().Create` for a generated key,
`new UnassignedValueObjectKeyGeneratorFactory().Create` for a `Guid` key refused unset.

Regenerate the compiled model when a rule that shapes a column changes, `MaxLength` say, as after any other change to
the model. [CHECK constraints](#check-constraints-from-the-rules) are not in it: a compiled model holds none, and
`EnsureCreated` on a context using one builds the model that holds them.

One difference remains, on a value object over `string` whose property has a database default, through
`HasDefaultValue` or `HasDefaultValueSql`. Left unset, the property holds `default(T)`, which a model built at run
time takes for unset, so the database default applies. A compiled model rebuilds that sentinel from the empty text,
which gives an instance unequal to `default(T)`, so it writes the property: the empty text, for a type that accepts
it, or a [refusal](#validation-on-write), for one that does not. An optional property, `Currency?`, is not affected,
and neither is a value object over a value type.

A [strict context](#validation-on-read) does not run on a compiled model yet when one of its value objects refuses the
default of its underlying type: `0` for a quantity of at least one, the empty text for a required string, an empty
`Guid`, any identifier. A compiled model rebuilds the sentinel of every property from that default, through the
property's converter, which in a strict context validates it, so the first entity the context tracks, read by a query
or passed to `Add`, fails with a `ValueObjectException`. A query with `AsNoTracking()` reads and validates as on a
model built at run time. Keep a strict context on the model built at run time, or read through it untracked.

CI compiles both models, for the JIT and for native AOT, of a context mapping every value object the conventions map,
required and optional, collections of them, and keys it generates or refuses unset, with CHECK constraints, beside a
strict one, filters on `.Value` through [`UseValueObjectQueries()`](#the-value-in-a-query), and takes the first on a
round trip through SQL Server, whose tables `EnsureCreated` creates with the constraints.

### Native AOT

`dotnet ef dbcontext optimize --precompile-queries --nativeaot` writes a model whose code calls the conversions of
each converter itself, `ValueObjectConverter<OrderId, Guid>.ToProvider` and `FromProvider`. They are public for it,
and hidden from IntelliSense, since nothing else has a reason to call them. Such a model builds and publishes with
native AOT, and `SaveChanges` writes, but queries do not run yet. Two bugs of Entity Framework Core's precompiled
queries, which any converted type reproduces without this library, fail a query that materializes an entity whose key
has a converter, and a query that takes a parameter of a converted type. Until they are fixed, native AOT is out of
reach for an application reading value objects through Entity Framework Core, and the package does not claim to be
AOT-compatible.

The code it writes does not always compile either, with or without this library: Entity Framework Core 10 writes
precompiled code that does not compile for an untracked query and for a sealed entity type, and model code that
cannot tell a type of the model named as one of its own internal types, `Reference<T>` among them, from that type. It
cannot precompile a query naming a value object nested in a generic type, `Catalog<TItem>.Stock`, whose nested type it
does not find.

Two queries over a primitive collection fail to precompile, whatever its element, `int` included. `Contains` over an
array, which C# 14 binds to `MemoryExtensions.Contains` over a span, is refused: call it on the array as a sequence,
`((IEnumerable<Quantity>)shipment.Batches).Contains(batch)`. `Contains` over a `List<T?>` handed a `T` is refused:
hand it a variable of type `T?`.

A query precompiled by `--precompile-queries` carries the SQL Entity Framework Core wrote for it, so a query on `.Value`
carries the column [`UseValueObjectQueries()`](#the-value-in-a-query) reads: the context the design-time factory builds
calls it too, in `OnConfiguring` or in the options the factory passes, or the precompilation fails on that query.

The conventions never run in the native binary. Entity Framework Core builds no model under native AOT, and refuses to
start without a compiled one, so `ConfigureValueObjects` and `ConfigureEntityIds` run only where a model is built:
under the JIT, and in `dotnet ef dbcontext optimize`, which writes the model the binary reads.

## Complex types and JSON columns

Complex types, `ToJson`, complex collections and owned types mapped to JSON work with the convention, and the rules
still shape the query: a value object declaring `MaxLength = 34` is read as
`JSON_VALUE([i].[Shipping], '$.Account' RETURNING nvarchar(34)) = @iban` on SQL Server. A collection of value objects
inside them is a [primitive collection](#collections-of-value-objects), as anywhere in the model.

## Raw SQL

`FromSql` and `SqlQuery` know nothing of the convention. `FromSql($"… WHERE Iban = {iban}")` throws
`The current provider doesn't have a store type mapping for properties of type 'Iban'`, and so does
`SqlQuery<Iban>`. Pass `.Value` as the parameter, and create the value object from a scalar result:

```csharp skip
var orders = await db.Orders.FromSql($"SELECT * FROM Orders WHERE Iban = {iban.Value}").ToListAsync();
```

## Other providers

**Azure Cosmos DB**, through Microsoft.EntityFrameworkCore.Cosmos 10.0.12: the convention applies, and equality, range
and nullable queries translate. `ibans.Contains(x.Iban)` throws
`Couldn't find array type mapping when applying item/array mappings`, as it does for any value converter, one written
by hand included: write a chain of `||`, or query the underlying values. `MaxLength` has no effect there. This was
checked offline, on the text of the queries.

**MongoDB**, through MongoDB.EntityFrameworkCore 10.0.4: the convention is mandatory. Without it, the provider writes a
value object as `{}` and reads it back as a default instance, where a relational provider refuses to build the model.
With it, values are stored bare, queries translate, and strict reads validate. On a standalone `mongod`, which has no
transactions, `SaveChanges` needs `db.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never`; a replica set
does not. MongoDB.Driver used directly, without Entity Framework Core, goes through serializers of its own instead:
[MongoDB](mongodb.md).

## Bulk extensions and linq2db

**EFCore.BulkExtensions 10.0.1** reads the model, so the convention is all it needs: `BulkInsert`, `BulkUpdate`,
`BulkRead` keyed on a value object, `BulkInsertOrUpdate` and `BulkDelete` honour the converter, the nullability and
the column sizes, and a strict context validates on `BulkRead`. It writes through the same converters, so a default its
type rejects is refused as [on write](#validation-on-write) (inferred, not run).

**Z.EntityFramework.Extensions 10.105.8.1**, a commercial package: `BulkInsert`, `BulkMerge` on a value-object key and
`UpdateFromQuery` work. `WhereBulkContains` over a list of value objects fails,
`'Iban' is not a member of type 'Probe.Domain.Iban'`: pass anonymous objects instead, `new { Iban = iban }`.

**linq2db.EntityFrameworkCore 10.6.0**: after `LinqToDBForEFTools.Initialize()`, `ToLinqToDB()` queries, `BulkCopy`,
`Merge` and `Update().Set(…)` use the converters and the column sizes of the model, `DECLARE @iban NVarChar(34)`.
Compare value objects there too, not `.Value`, which linq2db cannot translate: `UseValueObjectQueries()` reaches the
translation of Entity Framework Core, not linq2db's.

## Always Encrypted

The converter runs before SqlClient encrypts, so deterministic equality, `Contains`, reads and `ExecuteUpdate` work on
an encrypted column. Three things trip it:

- A parameter wider than the column is refused. Declare a `MaxLength` no greater than the column's size: a value object
  without one is sent as `nvarchar(4000)`.
- A `varchar` column needs the value object mapped as non-Unicode:
  `configurationBuilder.Properties<Iban>().AreUnicode(false)`.
- A value object created inside a predicate, `Iban.Create("…")`, becomes a literal, which cannot be encrypted: hoist
  it into a variable, as [Columns sized by the type](#columns-sized-by-the-type) shows.

`BulkInsertOrUpdate` of EFCore.BulkExtensions does not work on randomized columns.

## Dynamic data masking

With EntityFrameworkCore.Extensions 10.2.0, a mask is one line per type, and covers its optional properties too:

```csharp skip
configurationBuilder.Properties<Iban>()
    .HaveAnnotation(AnnotationConstants.DynamicDataMasking, MaskingFunctions.Partial(2, "XXXXXXXX", 4));
```

A principal without `UNMASK` reads `DEXXXXXXXX3000`, which the default read, trusting the column, turns into an `Iban`
its rules refuse, and not a default one; a masked `int` reads as `0`, a default `Quantity`. Use
[strict reads](#validation-on-read) on a masked column.

## Public identifiers

`[EntityId]` identifiers have a convention of their own, from
`AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore`, which maps them to fixed-width, non-Unicode columns.
Call it in addition to `ConfigureValueObjects`; [Public identifiers](../tutorials/public-identifiers.md#store-it)
shows how. It sets the converter of the identifiers too, so a strict context passes `strict: true` to both calls.
