---
title: Use with GraphQL (HotChocolate)
sidebar_label: GraphQL (HotChocolate)
slug: /how-to/graphql
description: Give every value object a GraphQL scalar of its own under HotChocolate 16, serialized as its underlying value and read through its rules, with a refused value answered with the rule's code under extensions.code, in the application's language if it asks.
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

## Native AOT

The package is AOT-compatible: each scalar is closed over its value object through the descriptor's visitor, never with
`MakeGenericType`, and its own code publishes with no warning. HotChocolate 16.6.8 itself does not yet start natively
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

## Not covered

- **HotChocolate 15 and earlier.** The package is built against HotChocolate.Types 16.6.8, its floor.
- **`AnyEntityId`**, which is not a value object: HotChocolate still infers an input object type from it.
- **Relay node identifiers**, `[ID]` and global object identification, mutation conventions, subscriptions, Fusion,
  and HotChocolate's own OpenAPI and Model Context Protocol adapters: not run.
- **Filtering and sorting with HotChocolate.Data.** `[UseFiltering]` infers an input from the struct's public
  properties, `IbanFilterInput { countryCode value }`, so a filter names the wrapper, `iban: { value: { eq: "…" } }`,
  and compares raw text the normalizer never saw.
- **The built-in scalars.** A value object is always a scalar of its own; publishing `String!` or `UUID!` instead would
  change the wire form of three underlying types, HotChocolate's `DateTime` requiring an offset, its `Duration` being
  ISO 8601, and a `char`, an `Int128` or a `UInt128` having no built-in scalar at all.
