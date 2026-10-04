# Integration reference

Install `AdCodicem.ValueObjects` plus the packages for the boundaries the application actually has. Each one is
closed over the concrete types at start-up, so per-request work is fully typed and allocates nothing extra.

| Package | Gives you |
| --- | --- |
| `AdCodicem.ValueObjects` | Contracts, source generator, analyzers. The one to install. |
| `AdCodicem.ValueObjects.Abstractions` | The contracts alone, no dependency. For a domain assembly that must stay bare. |
| `AdCodicem.ValueObjects.Json` | Source-generated `JsonSerializerContext` support, hand-written value objects, and the JSON Schema transform. |
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
//    generator cannot see the emitted [JsonConverter]. Name the hand-written factory it *can* see, from the
//    assembly declaring the context: the generator registers each converter with its value object's descriptor,
//    so a domain assembly declaring the value objects needs only AdCodicem.ValueObjects, not the JSON package.
[JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
[JsonSerializable(typeof(AccountResponse))]
public partial class ApiJsonContext : JsonSerializerContext;

// 2. A hand-written value object, or simply making the behaviour explicit at the composition root.
var options = new JsonSerializerOptions().AddValueObjects();
```

Writing refuses what reading would: an uninitialized instance (a member never set, a default array element) whose
default value its type rejects throws `ValueObjectJsonException` (a `JsonException`) from System.Text.Json and
`JsonSerializationException` from Newtonsoft.Json, as a value or a dictionary key. A type whose zero is valid (`Amount`
with `Minimum` 0, an unconstrained `Guid`) writes it.

**Every exception an integration throws for a refused value carries the rule's code.** System.Text.Json:
`ValueObjectJsonException.ErrorCode`, beside `ValueObjectType`. Newtonsoft.Json's `JsonSerializationException` and
Dapper's `DataException`: `exception.Data[ValueObjectErrors.ErrorCodeKey]`. EF Core: the `ValueObjectException` inside
`DbUpdateException`. Read any of them, wrapped or not, with `ValueObjectErrors.TryGetCode(exception, out var code)`.
A token or column not of the underlying type carries `value_object.not_parsable`, a `null` for a value object that
cannot be `null` `value_object.required`. An integration of your own sets
`exception.Data[ValueObjectErrors.ErrorCodeKey] = code`. For a gRPC `ErrorInfo.reason`, map the code with
`ValueObjectErrorCodes.ToUpperSnakeCase(code)` (`value_object.too_long` → `VALUE_OBJECT_TOO_LONG`); it throws
`ArgumentException` for a code that maps to no valid reason.

**JSON Schema** (`JsonSchemaExporter`, and every host built on it: Microsoft.Extensions.AI tools and structured
output, the MCP SDK, Semantic Kernel) describes a value object as `true` and a `List<Iban>` without `items`. Plug
`ValueObjectJsonSchema.TransformSchemaNode` into `JsonSchemaExporterOptions.TransformSchemaNode` to describe each one as
its underlying value with its rules (`type`, `null` for a nullable one, lengths, 1 for a `char`, `pattern`,
`minimum`/`maximum` or a sentence for a date, `enum`, `examples`, `description`, `format`), elements, dictionary values
and keys included; a key is described as the text it is written in, a number's text for a key over a number:

```csharp skip
var schema = JsonSchemaExporter.GetJsonSchemaAsNode(
    ApiJsonContext.Default.Options, typeof(Order),
    new JsonSchemaExporterOptions { TransformSchemaNode = ValueObjectJsonSchema.TransformSchemaNode });

// For a language model: the number alone, and formats JSON Schema does not define moved into the description.
// A host's own transform hands over the node's JsonTypeInfo:
var forTools = new AIJsonSchemaCreateOptions
{
    TransformSchemaNode = (context, node) => ValueObjectJsonSchema.Apply(
        context.TypeInfo, node, ValueObjectJsonSchemaProfile.LanguageModel),
};
```

`ValueObjectJsonSchema.CreateTransform(profile)` gives the exporter's delegate for a profile. The default profile,
`OpenApi`, follows `NumberHandling` as the OpenAPI document does. A host's description comes first and the value
object's follows; nothing is turned into a `$ref`. A `TimeOnly` or a `DateTime` gets no `time`/`date-time` format, which
RFC 3339 gives an offset it is written without, but the pattern of the form it is written in.

`Int128` and `UInt128` value objects travel as JSON **strings**, because JSON numbers cannot carry them. A numeric
value object follows `JsonSerializerOptions.NumberHandling` as its underlying type does: `AllowReadingFromString`,
`WriteAsString`, and `AllowNamedFloatingPointLiterals` for `NaN` and the infinities of a `double` or `float`. The
OpenAPI schema follows the same options: a number that may be read or written as text is `[integer|number, string]`
with a numeric `pattern`, as ASP.NET Core documents a bare number under its defaults.

Newtonsoft.Json: call `settings.AddValueObjects()` from `AdCodicem.ValueObjects.NewtonsoftJson`. It adds
`ValueObjectConverter` once, sets `DateParseHandling.None` and `FloatParseHandling.Decimal`, and returns the settings,
so it fits a host's own hook (`UseRecommendedSerializerSettings(s => s.AddValueObjects())` for Hangfire,
`AddNewtonsoftJson(o => o.SerializerSettings.AddValueObjects())` for MVC):

```csharp skip
var settings = new JsonSerializerSettings().AddValueObjects();
```

The converter applies the System.Text.Json rules and writes the same values, in the same text but for a whole
`decimal`, `double` or `float`, which Newtonsoft.Json writes with a fraction (`1250.0` against `1250`); either
serializer reads the other's text as the same value. It reads a number written as a string too (`"7"`), whole, as
System.Text.Json does under `AllowReadingFromString`, and otherwise only the token kind it writes. Without
`DateParseHandling.None`, Newtonsoft.Json turns date-like strings into `DateTime`, converted to local time when they
carry an offset, and the converter refuses a date that lost its text or its offset — so a `DateTimeOffset` value
object refuses any text with an offset, its own output (`+00:00` for UTC) included.

Pass `AddValueObjects(decimalReals: false)` when a payload carries a `double` or `float` value object beyond about
7.9e28, or one small enough to need more than 28 decimal places: `FloatParseHandling.Decimal` reads every number with
a fraction or an exponent as a `decimal`, so the first makes the reader throw and the second loses the digits past
them (all of them below about 1e-28, where it reads as zero).

Without the converter, Newtonsoft.Json goes through the generated `TypeConverter`: every value object is written as a
string, and a string or a number of any numeric type is read, checked (a number the underlying type cannot hold whole
is `value_object.not_parsable`, never truncated). The two modes read each other's numbers, but a rejection loses its
rule there, so keep the converter. Hosts that serialize through Newtonsoft.Json by default, the Azure Cosmos DB SDK v3
and Hangfire among them, take that path until their settings get the converter.

## ASP.NET Core

```csharp skip
builder.Services.AddControllers().AddValueObjects();                                   // model binding + JSON
builder.Services.Configure<ApiBehaviorOptions>(o => o.AddValueObjectProblemDetails()); // error codes in 400s
```

A value refused inside a JSON body is reported under its JSON path, `"errorCodes": { "$.email":
"value_object.invalid_format" }`, by the builder overload above, which replaces MVC's System.Text.Json input formatter
with one deriving from it, reading with the same `JsonSerializerOptions` instance, and leaves the model state messages
as MVC writes them; not once `AddNewtonsoftJson()` reads bodies, nor through a System.Text.Json input formatter the
application built with options of its own or derived, which is left as it is. To remove the System.Text.Json input
formatter, call `InputFormatters.RemoveType<SystemTextJsonInputFormatter>()` in `AddMvcOptions` or
`Configure<MvcOptions>`; a `PostConfigure<MvcOptions>` registered after `AddValueObjects()` must remove every
formatter that `is SystemTextJsonInputFormatter`, since `RemoveType` matches the exact type and misses the package's.
`AddValueObjects()` also exists on `MvcOptions` for an application that configures MVC directly; it adds the binder
alone, and records no code for a body. The MVC binder
treats white-space text as it treats empty text, as absent: `?country=%20` binds `CountryCode?` to `null`, while a
value object that cannot be `null` is a 400 with `value_object.required`, as MVC answers blank text for an `int`.

**Minimal APIs need no package to bind.** A generated value object implements `IParsable<T>` and `ISpanParsable<T>`,
which is exactly what minimal API parameter binding looks for:

```csharp skip
app.MapGet("/accounts/{iban}", (Iban iban) => ...);
```

A rejected value is a bare 400 there: no parameter name, no message, no code, `AddProblemDetails()` and
`AddValidation()` notwithstanding, and a 500 in Development behind `UseExceptionHandler`, where the
`BadHttpRequestException` of a body wraps the converter's exception: `ValueObjectErrors.TryGetCode` reads the code from
it. Empty text follows the
framework's rule, not MVC's: `?country=` for a `CountryCode?` is a 400 under the reflection-based binding.

**Under the Request Delegate Generator** (RDG), on in every build of a project setting `PublishAot`, `PublishTrimmed`
or `EnableRequestDelegateGenerator`, a value object declared in the project that maps the endpoints must list its
contract on its own declaration. The RDG is a source generator and does not see the generated `IParsable<T>`: without
the interface it binds the parameter from the body, so a route value gets a 400 and a query value is silently `null`.
`VO0033` reports it, and its code fix lists the interface. A value object from a referenced project needs nothing.
The contract is `IValueObject<TSelf, TValue>`, `INumericValueObject<TSelf, TValue>` with `Arithmetic = true`, or
`IEntityId<TSelf>` for an `[EntityId]`; listing it is harmless where no RDG runs:

```csharp
[ValueObject<string>(MaxLength = 10)]
public readonly partial struct Sku : IValueObject<Sku, string>;
```

Under the RDG, a handler returning a generated member needs an explicit return type, `string (Sku sku) => sku.Value`,
or the build fails with `CS0411` in `GeneratedRouteBuilderExtensions.g.cs`; and `?sku=` binds `null` to a nullable
parameter, where the reflection-based binding answers 400.

`AddValueObjectProblemDetails()` attaches the stable error code of the violated rule to the automatic 400
response of an **MVC controller**, under the extension named by `ValueObjectProblemDetails.ExtensionName`, so a client
can branch on `value_object.too_long` instead of parsing English. It extends `ApiBehaviorOptions`, which minimal APIs
never read. To carry the same codes out of a manually validated payload, fill that extension yourself:

```csharp skip
return Results.ValidationProblem(
    result.ToDictionary(),
    extensions: new Dictionary<string, object?>
    {
        [ValueObjectProblemDetails.ExtensionName] = result.Errors
            .GroupBy(failure => failure.PropertyName)   // a member can fail more than one rule
            .ToDictionary(member => member.Key, member => member.First().ErrorCode),
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
  validation per materialized value. EF Core builds the model once per context type and caches it, so `strict` is
  read once per context type: give strict reads a context type of their own, never a constructor argument.
- **The write path refuses a value the type rejects**, which only an uninitialized instance holds (an entity
  property never set): on an `Iban` property `SaveChanges` throws `DbUpdateException` around the
  `ValueObjectException`, and nothing is written; an optional `Iban?` property stores `NULL`. A type whose zero is
  valid writes it; one that must never hold `Guid.Empty` says so with a validator.
- A value object created inside a predicate, `Where(a => a.Iban == Iban.Create("DE89…"))`, becomes a SQL literal:
  hoist it into a variable, or wrap it in `EF.Parameter(...)`, so it goes as a parameter, as a captured value does.
- Departing from the convention for a single property: `builder.Property(e => e.Iban).HasValueObjectConversion<Iban, string>()`
  (with an optional `strict: true`). Prefer the convention.
- Never write `HasConversion` by hand for a value object: you would lose the generated comparer, and with it
  correct change tracking for a case-insensitive or otherwise custom comparison.
- A value object is a perfectly good key. Set the collation of a string key column to match the declared
  `Comparison`, or the database and the application will disagree about equality.
- EF Core maps no `Int128` or `UInt128`, so the convention skips a value object over either. Map it yourself:
  `builder.Properties<LedgerBalance>().HaveConversion<MyConverter, ValueObjectComparer<LedgerBalance>>()`, to a
  numeric column (sorts as numbers, but `System.Decimal` caps it near ±7.9 × 10²⁸) or a text one (full range,
  sorts as text), and its optional form with
  `builder.Properties<LedgerBalance?>().HaveConversion<MyConverter, NullableValueObjectComparer<LedgerBalance>>()`.
- **Compiled models** (`dotnet ef dbcontext optimize`) hold everything the conventions map. Regenerate after
  changing a rule that shapes a column. A `string` value object property with a database default is written, not
  defaulted, when left unset under a compiled model: declare it `Iban?` to let the default apply. A strict context
  (`ConfigureValueObjects(strict: true, …)`) cannot track an entity on a compiled model when one of its value objects
  refuses the default of its underlying type (`0`, `""`, `Guid.Empty`): keep it on the model built at run time, or
  read through it with `AsNoTracking()`. `--nativeaot`
  builds and publishes, but EF Core's precompiled queries fail on a converted key or parameter, value object or not:
  do not ship EF Core under native AOT yet. Never call the converters' `ToProvider`/`FromProvider`; they are public
  only for the generated model.

## Dapper

```csharp skip
ValueObjectDapper.AddValueObjectHandlers(typeof(Iban).Assembly);   // once, at start-up
```

Dapper keeps handlers in a process-wide table. Without this, every query touching a value object needs an
explicit projection. It never replaces a handler already in Dapper's table, and after
`SqlMapper.ResetTypeHandlers()` it has to be called again.

A construction of a generic value object gets a handler from it only if something resolved the construction first:
Dapper looks one up by the exact type, ahead of any query. Register each construction a query uses, once, at start-up:
`ValueObjectDapper.AddValueObjectHandler<Reference<PurchaseOrder>, string>();`.

- Read a nullable column into `Iban?`: `NULL` gives `null`. A single-column query into `Iban` throws
  `DataException`, but Dapper never calls the handler for a `NULL` mapped to a member or a constructor parameter:
  an `Iban` member is left uninitialized (`IsDefault`), silently. Declare `Iban?` for every column that can be
  `NULL`, outer joins included.
- A column the provider returns as the underlying type, or as its date and time counterpart (`DateTime` for a
  `date`, `TimeSpan` for a `time`, a UTC `DateTime` for a `timestamptz`), is trusted, like the EF Core read path.
  A value it cannot convert — a `DateTime` of no zone into a `DateTimeOffset`, a number out of range — throws
  `DataException` naming the type read and the value object.
- Text read into a non-string value object is parsed and validated, and a number or a `Guid` read into a string
  value object is turned into text (a `Guid` in its lowercase `D` form) and validated through `TryCreate`; a refusal
  throws `DataException` carrying the rule, and its code in `Data`.
- A parameter holding an uninitialized value object whose default its type rejects throws `DataException` before the
  command runs, from an `Iban` and an `Iban?` alike: the handler cannot see the column. A `null` `Iban?` goes out as
  `NULL`.
- A string parameter declares its column as the EF Core conventions map it: an `[EntityId]` as `char(n)`
  non-Unicode, a value object with `MaxLength` as Unicode text of that length. SQL Server keeps its index seek.
- A value object over `Int128` or `UInt128` gets no handler: no provider carries either type. Register a
  `SqlMapper.TypeHandler<T>` of your own that converts to the column you chose.

## FluentValidation

Defer to the rules the value object already owns instead of restating them:

```csharp skip
RuleFor(x => x.Iban).MustParseAs(typeof(Iban));        // the member holds raw text
RuleFor(x => x.Amount).MustSatisfy<Request, Amount, decimal>();  // raw underlying value, no instance built
RuleFor(x => x.Account).NotDefault<Request, Iban, string>();     // catches an uninitialized instance

// A required member: stop at the first failure, or empty text fails NotEmpty and the value object's rule both.
RuleFor(x => x.Iban).Cascade(CascadeMode.Stop).NotEmpty().MustParseAs(typeof(Iban));
```

Each failure carries the value object's own stable error code, so the API answers with the same vocabulary
everywhere. Options chained on `MustParseAs` or `MustSatisfy` (`WithErrorCode`, `WithMessage`, `WithSeverity`,
`WithState`, `WithName`) replace the value object's code or message; `{Reason}` quotes the value object's message. `MustParseAs` and `MustSatisfy` let `null` through: chain `NotEmpty()` when the member is required.
Empty text reaching `MustParseAs` is the value object's to judge: `value_object.required` for a string value object,
`value_object.not_parsable` for one over another type, and a pass for one declaring `AllowEmpty = true`.
Under native AOT, register a construction of a generic value object (`ValueObjectRegistry.Register<TSelf, TValue>`)
before a rule names it in `MustParseAs`, or building the rule throws `ArgumentException`.

## OpenAPI

```csharp skip
builder.Services.AddOpenApi(o => o.AddValueObjects());
```

A value object is documented as its underlying type carrying the rules declared on it: `maxLength`, `pattern`,
`minimum`, `format`, `enum` for a closed set, plus `Example` and `Description`. A closed set names its values after
its known values in `x-enum-varnames` (openapi-generator, Scalar), `x-enumNames` (NSwag) and `x-ms-enum` (Kiota,
AutoRest; named as the component, with each declared `Description`), never in the object form of
`x-enum-descriptions`, which NSwag refuses. A hand-written value object lists the names in
`Schema.KnownValueDetails`, one `KnownValueInfo` per value of `KnownValues`, in order, which the contract kit checks;
left out or out of step, none is published. Nothing to restate in an
annotation — and nothing to keep in sync, since the schema comes from the same declaration that validates.
`pattern` is the text of the `[GeneratedRegex]` behind `IValueObjectPatternValidator`, read when the type
compiles; its `RegexOptions` are not part of it (`VO0025`). A value written as a JSON string (`Int128`, `UInt128`,
`char`, dates, times) gets no inert `minimum`/`maximum`: its bounds go to `x-minimum`/`x-maximum` and a sentence of
the description. A `TimeSpan` gets no `format` (`duration` is ISO 8601, which it does not read) but the pattern of its
constant form, as the built-in stack documents a plain `TimeSpan`; a `TimeOnly` and a `DateTime` get no `time` or
`date-time` (RFC 3339 requires an offset they are written without) but the pattern of the form they are written in; a
`char` gets `minLength` and `maxLength` 1. Route, query and header parameters (minimal APIs, MVC, `[AsParameters]`)
carry the same schema in place, keeping the stricter bound or length of a route constraint; `items` of a collection
and `additionalProperties` of a dictionary refer to the component; a key goes to `propertyNames` as the text it is
written in, a string held to a number's pattern for a key over a number.

## Run-time lookup, when only a `Type` is known

```csharp skip
if (ValueObjectRegistry.TryGet(type, out var descriptor)
    && descriptor.TryParse(text, CultureInfo.InvariantCulture, out var boxed, out var validation))
{
    // descriptor.ValueObjectType, .ValueType, .Schema, .Create, .CreateUnchecked, .TryCreate, .GetValue, .Format,
    // .ValidateWrite (refuses the default of a type that rejects it, as the writers do), .Accept(visitor)
}
```

To close a generic adapter of your own over a value object known only by its descriptor, hand it a visitor rather than
calling `MakeGenericType`, which fails under native AOT for a struct:

```csharp skip
sealed class FormatterFor : IValueObjectVisitor<IFormatter>
{
    public IFormatter Visit<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
        => new ValueObjectFormatter<TSelf, TValue>(); // reads TSelf.Schema, calls TSelf.TryCreate
}

var formatter = descriptor.Accept(new FormatterFor());
```

Inside typed code, the rules are `TSelf.Schema` (`TSelf.Schema.MaxLength`), a static member of
`IValueObject<TSelf, TValue>`: no registry lookup.

Registration happens through a generated `[ModuleInitializer]`, so nothing needs registering by hand — but a
module initializer only runs once its assembly is loaded, which is what `EnsureAssemblyRegistered(assembly)`
forces (the EF Core and Dapper entry points already call it). A generic value object registers its definition
(`GetRegisteredGenericDefinitions()`), and `TryResolve` describes a construction, by reflection, once asked for it;
`TryGet` finds it from then on. Under native AOT, register each construction instead:
`ValueObjectRegistry.Register<Code<Order>, string>(static () => new Code<Order>.ValueJsonConverter())`. `TryGet` and `TryResolve` also unwrap `Nullable<T>`, and return the descriptor of the
value object itself: close a generic type over `descriptor.ValueObjectType`, never over the type asked for, and handle
`null` before calling `GetValue`, `Format` or `ValidateWrite`, which take a non-null instance (leave it to the host's
nullable wrapper where it has one). `IsValueObject` and `GetUnderlyingType` answer the cheap questions. A value
object is a struct implementing `IValueObject<TSelf, TValue>` over itself: `IsValueObject` is `true`, and
`GetUnderlyingType` other than `null`, exactly for what `TryResolve` describes, and every integration claims a type by
that rule.

This boxed path is for callers that only know a `Type` at run time. Domain code and the integrations above use
the typed path — the static abstract members of `IValueObject<TSelf, TValue>` — which neither boxes nor
allocates.
