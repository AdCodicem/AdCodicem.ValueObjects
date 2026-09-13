# Integration reference

Install `AdCodicem.ValueObjects` plus the packages for the boundaries the application actually has. Each one is
closed over the concrete types at start-up, so per-request work is fully typed and allocates nothing extra.

| Package | Gives you |
| --- | --- |
| `AdCodicem.ValueObjects` | Contracts, source generator, analyzers. The one to install. |
| `AdCodicem.ValueObjects.Abstractions` | The contracts alone, no dependency. For a domain assembly that must stay bare. |
| `AdCodicem.ValueObjects.Json` | Source-generated `JsonSerializerContext` support, and hand-written value objects. |
| `AdCodicem.ValueObjects.EntityFrameworkCore` | Converters, comparers, an assembly-wide convention. |
| `AdCodicem.ValueObjects.AspNetCore` | MVC model binding and RFC 9457 problem details carrying the violated rule. |
| `AdCodicem.ValueObjects.OpenApi` | Schema transformer for the built-in .NET OpenAPI stack. |
| `AdCodicem.ValueObjects.FluentValidation` | Rules that reuse what the value object already enforces. |
| `AdCodicem.ValueObjects.Dapper` | Type handlers for raw SQL. |
| `AdCodicem.ValueObjects.NewtonsoftJson` | Interop with code that has not moved to `System.Text.Json`. |
| `AdCodicem.ValueObjects.Identifiers[.EntityFrameworkCore]` | Stripe-style public identifiers. See `identifiers.md`. |
| `AdCodicem.ValueObjects.Testing` | The xUnit contract kit. |

## JSON

A generated value object carries its own `[JsonConverter]`, so **reflection-based `System.Text.Json` needs no
registration at all**: it serializes as the bare underlying value.

Two cases need `AdCodicem.ValueObjects.Json`:

```csharp skip
// 1. A source-generated serializer context. One source generator never sees another's output, so the STJ
//    generator cannot see the emitted [JsonConverter]. Name the hand-written factory it *can* see.
[JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
[JsonSerializable(typeof(AccountResponse))]
public partial class ApiJsonContext : JsonSerializerContext;

// 2. A hand-written value object, or simply making the behaviour explicit at the composition root.
var options = new JsonSerializerOptions().AddValueObjects();
```

`Int128` and `UInt128` value objects travel as JSON **strings**, because JSON numbers cannot carry them.

Newtonsoft.Json: add `ValueObjectConverter` from `AdCodicem.ValueObjects.NewtonsoftJson` to
`JsonSerializerSettings.Converters`.

## ASP.NET Core

```csharp skip
builder.Services.AddControllers().AddValueObjects();                                   // model binding + JSON
builder.Services.Configure<ApiBehaviorOptions>(o => o.AddValueObjectProblemDetails()); // error codes in 400s
```

`AddValueObjects()` also exists on `MvcOptions` for an application that configures MVC directly.

**Minimal APIs need nothing.** A generated value object implements `IParsable<T>` and `ISpanParsable<T>`, which
is exactly what minimal API parameter binding looks for:

```csharp skip
app.MapGet("/accounts/{iban}", (Iban iban) => ...);
```

`AddValueObjectProblemDetails()` attaches the stable error code of the violated rule to the automatic 400
response, under the extension named by `ValueObjectProblemDetails.ExtensionName`, so a client can branch on
`value_object.too_long` instead of parsing English. To carry the same codes out of a manually validated
payload, fill that extension yourself:

```csharp skip
return Results.ValidationProblem(
    result.ToDictionary(),
    extensions: new Dictionary<string, object?>
    {
        [ValueObjectProblemDetails.ExtensionName] =
            result.Errors.ToDictionary(failure => failure.PropertyName, failure => failure.ErrorCode),
    });
```

## Entity Framework Core

```csharp skip
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    => builder.ConfigureValueObjects(typeof(Iban).Assembly);
```

One call maps every value object of the assembly: an `Iban` declaring `MaxLength = 34` lands in `varchar(34)`,
a `Guid` value object in the provider's native `uuid`. It runs once while the model is built — nothing happens
per row.

- **The read path uses `CreateUnchecked`**, deliberately: it is the hottest path in most applications and it
  reads values this same application already validated. For a table another system also writes to, turn
  validation back on with `builder.ConfigureValueObjects(strict: true, typeof(Iban).Assembly)`; it costs one
  validation per materialized value.
- Departing from the convention for a single property: `builder.Property(e => e.Iban).HasValueObjectConversion<Iban, string>()`
  (with an optional `strict: true`). Prefer the convention.
- Never write `HasConversion` by hand for a value object: you would lose the generated comparer, and with it
  correct change tracking for a case-insensitive or otherwise custom comparison.
- A value object is a perfectly good key. Set the collation of a string key column to match the declared
  `Comparison`, or the database and the application will disagree about equality.

## Dapper

```csharp skip
ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);   // once, at start-up
```

Dapper keeps handlers in a process-wide table. Without this, every query touching a value object needs an
explicit projection.

## FluentValidation

Defer to the rules the value object already owns instead of restating them:

```csharp skip
RuleFor(x => x.Iban).MustParseAs(typeof(Iban));        // the member holds raw text
RuleFor(x => x.Amount).MustSatisfy<Request, Amount, decimal>();  // raw underlying value, no instance built
RuleFor(x => x.Account).NotDefault<Request, Iban, string>();     // catches an uninitialized instance
```

Each failure carries the value object's own stable error code, so the API answers with the same vocabulary
everywhere.

## OpenAPI

```csharp skip
builder.Services.AddOpenApi(o => o.AddValueObjects());
```

A value object is documented as its underlying type carrying the rules declared on it: `maxLength`, `pattern`,
`minimum`, `format`, `enum` for a closed set, plus `Example` and `Description`. Nothing to restate in an
annotation — and nothing to keep in sync, since the schema comes from the same declaration that validates.

## Run-time lookup, when only a `Type` is known

```csharp skip
if (ValueObjectRegistry.TryGet(type, out var descriptor)
    && descriptor.TryParse(text, CultureInfo.InvariantCulture, out var boxed, out var validation))
{
    // descriptor.ValueObjectType, .ValueType, .Schema, .Create, .CreateUnchecked, .TryCreate, .GetValue, .Format
}
```

Registration happens through a generated `[ModuleInitializer]`, so nothing needs registering by hand — but a
module initializer only runs once its assembly is loaded, which is what `EnsureAssemblyRegistered(assembly)`
forces (the EF Core and Dapper entry points already call it). `TryResolve` also unwraps `Nullable<T>`;
`IsValueObject` and `GetUnderlyingType` answer the cheap questions.

This boxed path is for callers that only know a `Type` at run time. Domain code and the integrations above use
the typed path — the static abstract members of `IValueObject<TSelf, TValue>` — which neither boxes nor
allocates.
