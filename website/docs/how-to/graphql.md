---
title: Use with GraphQL (HotChocolate)
sidebar_label: GraphQL (HotChocolate)
slug: /how-to/graphql
description: Give every value object a GraphQL scalar of its own under HotChocolate 16, serialized as its underlying value and read through its rules, with a refused value answered with the rule's code under extensions.code, in the application's language if it asks, and filter on every value object in HotChocolate.Data through its scalar.
---

# Use with GraphQL (HotChocolate)

```bash
dotnet add package AdCodicem.ValueObjects.HotChocolate
```

```csharp skip
builder.Services.AddGraphQLServer()
    .AddQueryType<Query>()
    .AddValueObjects();
```

```graphql
"An International Bank Account Number, stored in its electronic form."
scalar Iban

type Query {
  account(iban: Iban!, qty: Quantity!, amount: Amount, country: CountryCode): Account!
  orderRef(r: ReferenceOfPurchaseOrder!): ReferenceOfPurchaseOrder!
}
```

The package is built on HotChocolate.Types 16, not on HotChocolate.AspNetCore: a schema built with
`services.AddGraphQL()`, outside ASP.NET Core, takes it as well.

## Without the package

HotChocolate infers a GraphQL type from every CLR type it meets, and knows nothing of a value object:

- **As an argument or an input field**, it infers an input object type, looks for a constructor it can fill from the
  field `value`, finds none, and the schema does not start: ``No compatible constructor found for input type type
  `Shop.Iban`. … There was no way to set the following properties: value.`` Every generated value object is built
  through a constructor that nothing outside the type can fill, by design.
- **As a field**, it infers an object type, `type Iban { value: String! }`, and a client has to select `iban { value }`.

Binding each value object to a built-in scalar by hand, `BindRuntimeType<Iban, StringType>()` with a type converter in
each direction, starts the schema, but publishes `String!` where the type was, loses its name and description, and
answers a refused value with HotChocolate's ``{"message": "The value provided for `iban` is not in a valid format.",
"extensions": {"code": "HC0001"}}``, the rule and its code left on `IError.Exception`.

Adopting the package changes what a schema publishes: a value object that was an output object type becomes a scalar,
so a client that selected `iban { value }` selects `iban`.

## What each underlying type becomes

`AddValueObjects()` registers a type discovery handler, asked before HotChocolate's own, which answers every value object
the schema references, as an argument, an input field, a variable, a field or the element of a list, with a
`ValueObjectScalarType<TSelf, TValue>` of its own. A `TSelf?` is the nullable scalar. Its literal is its underlying
value's:

| Underlying type | Serialized as | Read | Written |
| --- | --- | --- | --- |
| `bool` | Boolean | the literal, then `TryCreate` | `true` |
| `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong` | Int, as a JSON number | the digits read exactly as the underlying type, then `TryCreate`; a number it cannot hold is `value_object.not_parsable` | the number, `18446744073709551615` for `ulong.MaxValue` |
| `decimal`, `double`, `float` | Float, from an Int literal too | read with an exponent, `1e3` is `1000` for a `decimal`, then `TryCreate`; a number that is not finite, `1e400`, is `value_object.not_parsable` | the shortest text that reads back as the value; one that is not finite is refused, as no GraphQL Float holds it |
| `Int128`, `UInt128` | String, as the library's JSON writes them | `TSelf.TryParse` | the invariant text |
| `char`, `string`, `Guid`, `DateOnly`, `TimeOnly`, `DateTime`, `DateTimeOffset`, `TimeSpan` | String | `TSelf.TryParse(text, CultureInfo.InvariantCulture, …)`, the normalizer included: `"fr76 3000 6000 0112 3456 7890 189"` reads as `FR7630006000011234567890189` | the underlying value's invariant text, a date or a time in its round-trip form, a duration in its constant one: `07:30:00.0000000`, `2026-10-13T09:15:00.0000000`, `01:30:00` |
| any other, a value object written by hand over `Uri` | String | `TSelf.TryParse` | as the value object formats itself, `ToString(null, CultureInfo.InvariantCulture)` |

GraphQL's own Int holds 32 bits; a scalar over a wider integer takes every number its type holds, as the JSON its
converter writes does. A value is written as its underlying value, never through a formatting hook of the value object,
as every other integration writes it. A `TimeSpan` reads its constant form, as everywhere else in the library:
`PT1H30M` is `value_object.not_parsable`. A `DateTime` reads what its `TryParse` reads, `2026-10-13` included. Every
underlying type, option and hook of the test domain is run this way, from the edges its rules declare to the values they
rule out.

GraphQL input always comes from a client, so a scalar always validates: there is no trusted mode.

## Names and collisions

A scalar is named by the schema's naming conventions, as HotChocolate names the type it would otherwise infer: `Iban`,
`ReferenceOfPurchaseOrder` for `Reference<PurchaseOrder>`, and the name HotChocolate's `[GraphQLName]` gives the struct.
An application's own `INamingConventions` names the scalars too.

Two value objects of one name, `Billing.Code` and `Shipping.Code`, fail the schema at start, with HotChocolate's own
``The name `Code` was already registered by another type.``: the package renames nothing. Name one from the composition
root, which costs the domain no reference to HotChocolate:

```csharp skip
builder.Services.AddGraphQLServer()
    .AddQueryType<Query>()
    .AddValueObjects(options => options.Name<Shipping.Code>("ShippingCode"));
```

Every `AddValueObjects` call on one schema configures the same options, in the order of the calls, and each schema of
an application, `AddGraphQLServer("admin")`, keeps options of its own.

## What the schema publishes

- **The description** is the type's, from its schema: its XML summary.
- **The pattern** of a scalar serialized as a String is the one the type declares, or the one the wire form of a
  `TimeSpan`, a `TimeOnly` or a `DateTime` follows, as the OpenAPI packages publish them, in `ScalarType.Pattern`.
  HotChocolate writes it into the SDL only as `@serializeAs`, when the application turns on a composite-schema option
  that is off by default:

  ```csharp skip
  builder.Services.AddGraphQLServer()
      .ModifyOptions(options => options.ApplySerializeAsToScalars = true)
      .AddValueObjects();
  ```

  ```graphql
  scalar Iban @serializeAs(type: STRING, pattern: "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")
  scalar Quantity @serializeAs(type: INT)
  ```

  The pattern is .NET's text as it stands, as the OpenAPI packages publish it, where HotChocolate documents the
  argument as an ECMA-262 pattern: the two dialects read most patterns alike, but not all.
- **`@specifiedBy`** is published for a value object the options name, and for none by default: HotChocolate's own
  scalars point to specifications of their wire forms, and none describes a value object's.

  ```csharp skip
  .AddValueObjects(options => options.SpecifiedBy<Iban>("https://www.iso.org/standard/81090.html"))
  ```

GraphQL has no place for a length, a bound or a known value, and the package adds no directive of its own, which no
client tool would read. A closed set stays a scalar rather than a GraphQL `enum`: an enum value must be a GraphQL name,
which `fr-FR` or `42` is not, and its wire form would be that name rather than the value.

## A refused value

A refusal is a `LeafCoercionException` whose GraphQL error carries the message of the rule and, as its code, the rule's,
which a client reads under `extensions.code`. The package adds no extension of its own:

- **An argument or a field of an input object** is a field error. Its `path` is the field, HotChocolate adds the
  argument's `inputPath`, `["ibans", 1]` for the second element of a list, then `coordinate` and `fieldType`. Over HTTP
  the answer is 200, with the field `null`, or `data: null` when the field cannot be `null`:

  ```json
  {"errors": [{"message": "The value must be at least 15 characters long.", "path": ["account"],
    "extensions": {"code": "value_object.too_short", "inputPath": ["iban"], "coordinate": "Query.account(iban:)", "fieldType": "Iban"}}],
   "data": null}
  ```

- **A variable** is a request error. Its `path` is the variable, `["l", 1]` for an element of a list, and its
  extensions hold the code alone. Over HTTP the answer is 400 under `application/graphql-response+json`, and 200 under
  `application/json`.
- **A literal of another kind** than the scalar's, `qty: "3"` or a number for an `Int128`, is refused by HotChocolate's
  validation before the scalar reads it: "The specified argument value does not match the argument type.", with no
  code. A variable of another JSON kind reaches the scalar, which answers `value_object.not_parsable`: "Quantity cannot
  read a String."
- **`null`** never reaches the scalar: for an argument that cannot be `null` it is a validation error, "The argument
  `iban` is required.", or `HC0018` for a variable, and for a nullable argument the resolver receives `null`.
- **A field** whose resolver returns an uninitialized instance its type refuses is not written, as the JSON converter
  refuses it, nor is a real that is not finite. HotChocolate replaces the code with its own `EXEC_INVALID_LEAF_VALUE`, and
  keeps the rule's message: such a value is a bug of the server, not bad input.
- **A value object written by hand** whose `TryCreate` refuses without saying why is answered with
  `value_object.not_parsable` and "The value is not a valid HandWrittenRating.".

The exception carries the code in its `Data`, under `ValueObjectErrors.ErrorCodeKey`, and the error's `Exception` is a
`ValueObjectException` with the type and the code, never the refused value. HotChocolate never serializes that
exception; an error filter of the application's own reads it, an output refusal's included:

```csharp skip
builder.Services.AddGraphQLServer()
    .AddValueObjects()
    .AddErrorFilter(error => error.Exception is { } exception && ValueObjectErrors.TryGetCode(exception, out var code)
        ? error.SetExtension("rule", code)
        : error);
```

Under `IncludeExceptionDetails`, HotChocolate adds `"exception": {"message": …}`, which is the rule's message and never
quotes the value.

## Messages in the application's language

A scalar has no request to read a culture from: HotChocolate hands it the literal or the JSON value alone. An error
filter reads the messages of the application's resource instead, by the rule's code, as the ASP.NET Core packages do
([Error codes](../reference/errors.md#a-message-in-the-applications-language)):

```csharp skip
builder.Services.AddLocalization();
builder.Services.AddGraphQLServer()
    .AddQueryType<Query>()
    .AddValueObjects(options => options.UseResource<ValidationMessages>());

app.UseRequestLocalization(options => options.AddSupportedCultures("en-US", "fr-FR").AddSupportedUICultures("en-US", "fr-FR"));
app.MapGraphQL();
```

The filter looks each refusal up under `ValueObject.{code}`, in the current UI culture, and formats it in the current
culture, which HotChocolate's execution keeps from the request that `UseRequestLocalization()` set it on. `{0}` is the
argument, the input field or the variable the refusal concerns, the last name of the error's `inputPath`, or else of its
`path`: `« iban » : au moins 15 caractères.`, `« q » doit être compris entre 1 et 100.` for a variable `$q`. A variable's
default value, `query($q: Quantity = 5000)`, is the exception: HotChocolate reads it under a path of its own,
`["root"]`, which names no variable, so the filter quotes the scalar's name, `« Quantity »`. A code the resource holds
no message for keeps the English one, and the code stays the rule's.

A value refused in an argument or an input field a predicate of `QuoteScalarNameIn` selects, by the member the error's
`coordinate` names, is quoted by the scalar's name too. Use it for an input named after what it does with the value
rather than after the value:

```csharp skip
.AddValueObjects(options => options
    .UseResource<ValidationMessages>()
    .QuoteScalarNameIn(inputValue => inputValue.Coordinate.Name == "RangeInput"))
```

The operations of a HotChocolate.Data filter are such inputs, and `AddValueObjectFiltering()` selects them itself
([Filtering](#filtering-and-sorting-with-hotchocolatedata)): `« Iban »`, never `« eq »`. Only the message changes: the
code, the `inputPath` and the `coordinate` stay HotChocolate's. A variable HotChocolate reads before any field, `eq: $v`,
has no coordinate and keeps its name, `« v »`.

The localizer comes from the application's services: without `services.AddLocalization()`, the request executor fails
to start, naming the call, rather than the first refusal. The filter runs where the first `AddValueObjects` call
registers it among the schema's error filters: one of the application's registered after it sees the localized
message, one registered before it the English one.

## Generic value objects and value objects written by hand

A construction of a generic value object, `Reference<PurchaseOrder>`, and a value object written by hand that nothing
registered are described by the registry the first time the schema meets them, which takes dynamic code. Where the
runtime has none, as under native AOT, the handler refuses such a type with a `NotSupportedException` naming the two
ways to map it at compile time: registering it at start-up, or adding its scalar typed:

```csharp skip
builder.Services.AddGraphQLServer()
    .AddValueObjects()
    .AddValueObjectScalar<Reference<PurchaseOrder>, string>()
    .AddValueObjectScalar<Rating, int>("Stars");
```

`AddValueObjectScalar<TSelf, TValue>()` reads the rules through `TSelf`, never through the registry, and needs no
`AddValueObjects()` beside it. Its scalar wins for its type, its `name` over the options' `Name<TSelf>`, which names one
added without a name.

## Filtering and sorting with HotChocolate.Data

```bash
dotnet add package AdCodicem.ValueObjects.HotChocolate.Data
```

Without it, HotChocolate.Data has the same gap one level down. `[UseFiltering]` infers an input from the struct's public
properties, `IbanFilterInput { countryCode: StringOperationFilterInput value: StringOperationFilterInput }`, so a filter
names the wrapper, `where: { iban: { value: { eq: "…" } } }`, and compares raw text, which normalization never saw:
`fr76 3000 …` finds nothing. Entity Framework Core translates that `value` only under `UseValueObjectQueries()`
([Entity Framework Core](ef-core.md)), and fails the query otherwise.

`AddValueObjectFiltering()`, after `AddFiltering()`, filters every value object a filter meets through the operations of
its scalar, with no binding per type:

```csharp skip
builder.Services.AddGraphQLServer()
    .AddQueryType<Query>()
    .AddValueObjects()
    .AddSorting()
    .AddProjections()
    .AddFiltering()
    .AddValueObjectFiltering();   // after AddFiltering()
```

```graphql
input AccountFilterInput {
  iban: IbanOperationFilterInput
  backup: EmailAddressOperationFilterInput            # EmailAddress?
  ref: ReferenceOfPurchaseOrderOperationFilterInput   # Reference<PurchaseOrder>
  previous: ListIbanOperationFilterInput              # List<Iban>: all, none, some, any
  …
}

input IbanOperationFilterInput {
  eq: Iban neq: Iban in: [Iban!] nin: [Iban!] gt: Iban ngt: Iban gte: Iban ngte: Iban lt: Iban nlt: Iban lte: Iban nlte: Iban
}
```

### What it covers

A type interceptor replaces the input HotChocolate.Data infers for a value-object member with the package's, closed over
the value object through its descriptor's visitor:

- a member of the value object's type or of its nullable type, and the elements of a list or an array of either, whose
  list operations are HotChocolate's;
- every filter input: those `[UseFiltering]` infers, a part of the row, a `FilterInputType<T>` of the application's own
  for a field it does not type itself, a named convention, `AddFiltering("s")`, whose inputs keep HotChocolate's prefix,
  `s_IbanOperationFilterInput`, and a `FilterConvention` subclass registered with `AddFiltering<TConvention>()`;
- a construction of a generic value object and a value object written by hand, which the registry describes when the
  filter meets them ([below](#generic-value-objects-and-value-objects-written-by-hand-in-a-filter)).

It leaves alone what the application decides itself: a binding of its own, `BindRuntimeType<Quantity, QuantityEqOnly>()`,
a field its filter types, `descriptor.Field(x => x.Qty).Type<QuantityEqOnly>()`, and HotChocolate's `[ID]`, whose
`IdOperationFilterInput` replaces any other input.

### The operations

- **Every value object** gets `eq`, `neq`, `in`, `nin`, `gt`, `gte`, `lt`, `lte` and their negations, as HotChocolate's
  comparable types do; a closed set keeps them, as its scalar is a String.
- **A value object over `bool`** gets `eq` and `neq` alone, as HotChocolate gives a `bool`.
- **An operand is nullable**, and `eq: null` finds the rows whose nullable member holds no value. An element of `in` or
  `nin` is not, `in: [Iban!]`, unlike HotChocolate's `in: [Int]`: `or: [{ backup: { eq: null } }, { backup: { in: […] }
  }]` finds both.

HotChocolate.Data's own handlers read an operand through the scalar, so it is normalized and validated, and compare it
with the member through the value object's operators, which Entity Framework Core translates to the column through its
converter, with no `UseValueObjectQueries()`:

| Filter | SQLite | PostgreSQL | SQL Server |
| --- | --- | --- | --- |
| `iban: { eq: "fr76 3000 6000 0112 3456 7890 189" }` | `"a"."Iban" = @p`, `@p='FR7630006000011234567890189'` | `a."Iban" = @p` | `[a].[Iban] = @p` |
| `iban: { in: [ … ] }` | `"a"."Iban" IN (@p1, @p2)` | `a."Iban" = ANY (@p)` | `[a].[Iban] IN (@p1, @p2)` |
| `qty: { gt: 5 }` | `"a"."Qty" > @p` | `a."Qty" > @p` | `[a].[Qty] > @p` |
| `backup: { eq: null }` | `"a"."Backup" IS NULL` | `a."Backup" IS NULL` | `[a].[Backup] IS NULL` |
| `previous: { some: { eq: "…" } }` | `@p IN (SELECT "p"."value" FROM json_each("a"."Previous") AS "p")` | `@p = ANY (a."Previous")` | `@p IN (SELECT [p].[value] FROM OPENJSON([a].[Previous]) WITH ([value] nvarchar(34) '$') AS [p])` |

- **A comparison of text**, `gt: "DE89370400440532013000"`, compares the column under its collation, as for a `string`
  column: SQL Server's default collation is case-insensitive, where the value object's own order, in memory, is ordinal.
- **The provider's limits are the underlying type's.** SQLite translates no comparison of a `ulong`, a `TimeSpan` or a
  `DateTimeOffset`, and the query fails with "Unexpected Execution Error", as the same comparison of a column of the
  underlying type does. Npgsql refuses a `DateTime` operand without an offset against a `timestamp with time zone`
  column: send it with `Z`.
- **A value object written by hand needs its operators**, `==`, `!=`, `<`, `<=`, `>` and `>=`, which every generated one
  has: without them, its `eq` and `gt` fail when they run, "The binary operator Equal is not defined for the types …",
  while its `in`, which goes through its equality, answers.
- **Sorting and projections need nothing.** `order: { iban: ASC }` is `ORDER BY` the column, a nullable member and a
  generic construction included, in the database's collation, and `[UseProjection]` selects the columns of value objects,
  with or without the package.

A refused operand is the scalar's own refusal ([A refused value](#a-refused-value)): the rule's message and, under
`extensions.code`, its code, at the operation HotChocolate names:

```json
{"message": "The value must be at least 15 characters long.", "path": ["accounts"],
 "extensions": {"code": "value_object.too_short", "inputPath": ["eq"], "coordinate": "IbanOperationFilterInput.eq", "fieldType": "Iban"}}
```

`inputPath` is `["in", 1]` for the second element of `in`, and `["w", "iban", "eq"]` for a variable `$w` holding the
filter, which is then a request error with no `path`. A literal of another kind, `qty: { eq: "3" }`, is refused by
HotChocolate's validation, with no rule's code. Under `UseResource<T>()`, the message quotes the scalar, never the
operation: `« Iban » : au moins 15 caractères.` ([Messages](#messages-in-the-applications-language)).

### The text operations, on request

```csharp skip
builder.Services.AddGraphQLServer()
    .AddQueryType<Query>()
    .AddValueObjects()
    .AddFiltering()
    .AddValueObjectFiltering(options => options.IncludeTextOperations());
```

Every value object over `string` then also gets `contains`, `startsWith` and `endsWith`, and their negations, whose
operand is a `String`, never the value object: `FR` is no IBAN. They compare its underlying text, `IValueObject<string>.Value`:

- **The fragment is compared as typed, never normalized**: a normalizer is written for a whole value, and one that pads,
  prefixes or reformats would change a fragment's meaning. `contains: "fr76"` finds nothing on SQLite and PostgreSQL,
  and finds the row on SQL Server, whose default collation is case-insensitive.
- **Entity Framework Core translates them only under `UseValueObjectQueries()`**, as it does `.Value` of a value object
  ([Entity Framework Core](ef-core.md)): `"a"."Iban" LIKE @p_startswith ESCAPE '\'` and `instr("a"."Iban", @p) > 0` on
  SQLite, `a."Iban" LIKE @p_startswith` on PostgreSQL, `[a].[Iban] LIKE @p_startswith ESCAPE N'\'` on SQL Server, the
  element of a list included. Without it, each fails at its first query, with "Unexpected Execution Error" around Entity
  Framework Core's "could not be translated", never at start: the package does not reference Entity Framework Core, and
  cannot tell.
- **A nullable member is guarded**, and a negation finds the rows that hold no value, as `ncontains` does on a
  `string?`: `"a"."Backup" IS NULL OR "a"."Backup" IS NULL OR instr("a"."Backup", @p) <= 0`. HotChocolate's own guard
  repeats the test on the column, which costs nothing.
- **A `null` fragment** is refused as HotChocolate refuses one for a `String` member, word for word, `HC0026`: "The
  provided value for filter `contains` of type IbanOperationFilterInput is invalid. Null values are not supported.",
  with no rule's code.
- **They extend the default filter convention alone**, which `AddFiltering()` registers: a named convention keeps the
  comparable operations, and the call has to follow `AddFiltering()`, or the schema fails at start, ``No default filter
  convention found. Call `AddFiltering()` on the schema builder.`` The comparable operations work wherever the call
  stands.
- **Cost.** HotChocolate's cost analysis weighs a `String` member's text operations as expensive, 20, through a flag that
  is internal to it: the package's take the default cost of a filter operation, 10.

### Names

The inputs are named after the scalar, `IbanOperationFilterInput` and `ListIbanOperationFilterInput`, so a scalar the
options name, `Name<Iban>("BankAccount")`, names them `BankAccountOperationFilterInput` and
`ListBankAccountOperationFilterInput`, and two value objects of one CLR name, whose scalars the application sets apart
([Names and collisions](#names-and-collisions)), get two inputs, `CodeOperationFilterInput` and
`ShippingCodeOperationFilterInput`. HotChocolate's own `ComparableOperationFilterInputType<TSelf>` is named after the CLR
type, and fails such a schema: ``The name `ComparableCodeOperationFilterInput` was already registered by another type.``

`AddValueObjectFiltering()` calls `AddValueObjects()`, whose scalars the operands are, and every call on one schema
configures the same options, read when the schema is built.

### Generic value objects and value objects written by hand in a filter

A construction of a generic value object, `Reference<PurchaseOrder>`, and a value object written by hand that nothing
registered are described by the registry the first time a filter meets them, as their scalars are, which takes dynamic
code. Where the runtime has none, the interceptor refuses such a type at start with a `NotSupportedException` naming
`ValueObjectRegistry.Register`, and the typed scalar and binding, which ask no registry:

```csharp skip
builder.Services.AddGraphQLServer()
    .AddValueObjectScalar<Reference<PurchaseOrder>, string>()
    .AddFiltering(convention => convention.AddDefaults().BindValueObject<Reference<PurchaseOrder>, string>())
    .AddValueObjectFiltering();
```

`BindValueObject<TSelf, TValue>()` binds the value object and its nullable type to its comparable operations, named after
its scalar, a list of them too; it also serves a convention the application configures itself. Its operands are the
value object's scalar, so where the runtime has no dynamic code it needs `AddValueObjectScalar<TSelf, TValue>()` beside
it, or the scalar handler refuses the type in its turn; `ValueObjectRegistry.Register<TSelf, TValue>(TSelf.Schema)` at
start-up covers the scalar and the filter alike.

### Binding by hand

The schema types are public, `ValueObjectOperationFilterInputType<TSelf>`, and `ValueObjectTextOperationFilterInputType<TSelf>`
for a value object over `string`: subclass one to narrow the operations of one value object, and bind it with
`BindRuntimeType` for both the value object and its nullable type. HotChocolate looks a binding up by a member's exact
type: bound for one form alone, the other form's member gets the package's input of the same name, and the schema does
not start. The text type's `contains`, `startsWith` and `endsWith` run through the handlers
`AddValueObjectFiltering(options => options.IncludeTextOperations())` adds to the default convention alone: bound
without the option, or in a named convention, it fails the schema at start, ``For the field contains of type
IbanOperationFilterInput was no handler found.``, and bound to a value object over another type, ``'Shop.Quantity' is
not a value object over string``.

```csharp skip
builder.Services.AddGraphQLServer()
    .AddFiltering(convention => convention.AddDefaults()
        .BindRuntimeType<Iban, NarrowIban>()
        .BindRuntimeType<Iban?, NarrowIban>())
    .AddValueObjectFiltering();

public class NarrowIban : ValueObjectOperationFilterInputType<Iban>
{
    protected override void Configure(IFilterInputTypeDescriptor descriptor)
    {
        base.Configure(descriptor);
        descriptor.Ignore(DefaultFilterOperations.GreaterThan).Ignore(DefaultFilterOperations.NotGreaterThan);
    }
}
```

A subclass that names itself, `descriptor.Name("QuantityRange")`, keeps its name, and a list of it is named after it,
`ListQuantityRange`.

### Moving from the recipe

Before the package, this guide bound each value object to HotChocolate's own comparable operations,
`BindRuntimeType<Iban, ComparableOperationFilterInputType<Iban>>()`. A binding of the application's own wins, so those
keep their inputs, `ComparableIbanOperationFilterInput`, beside the package's for every other member: remove the lines to
let the package cover them too, a nullable member, which the recipe missed, included. The inputs are then renamed,
`IbanOperationFilterInput`, which a client sees only where it declares a variable of that type itself; a variable typed
as the whole filter, `AccountFilterInput`, keeps working.

## Native AOT

The package is AOT-compatible: each scalar is closed over its value object through the descriptor's visitor, never with
`MakeGenericType`, and its own code publishes with no warning. So is AdCodicem.ValueObjects.HotChocolate.Data's: its
operation types are closed through the same visitor, and its type interceptor is created by a factory, whose
constructor the AOT compiler keeps. HotChocolate 16.6.8 itself does not yet start natively
without help, so CI publishes no HotChocolate application:

- Its publish warns (`IL2104` for HotChocolate.Types and GreenDonut, `IL3053`, `IL3000`, `IL3002`).
- Its binary aborts at start, `A suitable constructor for type 'HotChocolate.Fetching.DataLoaderRootFieldTypeInterceptor'
  could not be located.`, unless HotChocolate.Types and the application's assembly are rooted:

  ```xml
  <ItemGroup>
    <TrimmerRootAssembly Include="HotChocolate.Types" />
    <TrimmerRootAssembly Include="$(AssemblyName)" />
  </ItemGroup>
  ```

With both rooted, a native binary answers literals, variables, a generic construction, a value object written by hand
and registered at start, and lists of value objects or of their nullable type exactly as the JIT does. HotChocolate builds
the `List<TSelf>` behind a list argument with `MakeGenericType`, which fails natively for a struct the AOT compiler never
saw: each scalar names `List<TSelf>`, `List<TSelf?>`, `TSelf[]` and `TSelf?[]`, so take a list argument as one of
those, or as an `IReadOnlyList<TSelf>`, which HotChocolate fills with a `List<TSelf>`, never a `HashSet<TSelf>` or an
`ImmutableArray<TSelf>`. A generated value object needs no typed registration; a
construction of a generic one, or a value object written by hand, needs `AddValueObjectScalar` or
`ValueObjectRegistry.Register`.

HotChocolate.Data's filtering of a value object does not run natively, whatever the package does. With HotChocolate.Types,
HotChocolate.Data and the package's assembly rooted, the schema builds and the text operations answer, but
HotChocolate.Data closes generic methods over the member's type at run time, which have no native code for a struct:
`'HotChocolate.Data.Filters.Expressions.FilterExpressionBuilder.CreateAndConvertParameter[Shop.Iban](System.Object)' is
missing native code. MethodInfo.MakeGenericMethod() is not compatible with AOT compilation.`, the same for
`Enumerable.Contains[Shop.Iban]`, and sorting fails on the `Expression<Func<T, Iban>>` it builds.

## Not covered

- **HotChocolate 15 and earlier.** The packages are built against HotChocolate.Types 16.6.8 and HotChocolate.Data
  16.6.8, their floors.
- **`AnyEntityId`**, which is not a value object: HotChocolate still infers an input object type from it.
- **Relay node identifiers**, `[ID]` and global object identification, mutation conventions, subscriptions, Fusion,
  and HotChocolate's own OpenAPI and Model Context Protocol adapters: not run. `[ID]` on a value-object member of a
  filtered type replaces the package's input with HotChocolate's `IdOperationFilterInput`, which HotChocolate then fails
  to run: `Unable to convert type from Object to OrderId`.
- **HotChocolate.Data's other providers**, HotChocolate.Data.MongoDb, Marten's and Raven's, which have handlers of their
  own: the package covers the queryable provider, which Entity Framework Core and rows in memory go through.
- **The text operations in a named convention**, which their handlers do not extend, and a `@cost` weight of their own.
- **A `[UseFiltering]` field returning value objects themselves**, `IQueryable<Iban>`, which keeps HotChocolate's
  inferred `IbanFilterInput { countryCode value }`: the argument's own type is no member a filter input holds. Nor a
  list of lists.
- **A `[UseProjection]` field beside a field filtered through a named convention**, over a row holding an object of its
  own: HotChocolate 16.6.8 fails such a schema at start, a type registered twice, with or without the package.
- **The built-in scalars.** A value object is always a scalar of its own; publishing `String!` or `UUID!` instead would
  change the wire form of three underlying types, HotChocolate's `DateTime` requiring an offset, its `Duration` being
  ISO 8601, and a `char`, an `Int128` or a `UInt128` having no built-in scalar at all.
