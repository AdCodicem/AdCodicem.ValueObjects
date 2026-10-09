# Integration reference

Install `AdCodicem.ValueObjects` plus the packages for the boundaries the application actually has. Each one is
closed over the concrete types at start-up, so per-request work is fully typed and allocates nothing extra.

| Package | Gives you |
| --- | --- |
| `AdCodicem.ValueObjects` | Contracts, source generator, analyzers, and XML serialization on request (below). The one to install. |
| `AdCodicem.ValueObjects.Abstractions` | The contracts alone, no dependency. For a domain assembly that must stay bare. |
| `AdCodicem.ValueObjects.Json` | Source-generated `JsonSerializerContext` support, hand-written value objects, and the JSON Schema transform. |
| `AdCodicem.ValueObjects.EntityFrameworkCore` | Converters, comparers, an assembly-wide convention. |
| `AdCodicem.ValueObjects.AspNetCore` | MVC model binding and RFC 9457 problem details carrying the violated rule. |
| `AdCodicem.ValueObjects.AspNetCore.Http` | The same problem details for minimal APIs, AOT-compatible. |
| `AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson` | MVC on Newtonsoft.Json: the converter in MVC's settings, and the codes of a refused body. |
| `AdCodicem.ValueObjects.OpenApi` | Schema transformer for the built-in .NET OpenAPI stack. |
| `AdCodicem.ValueObjects.Swashbuckle` | Schema and parameter filters for Swashbuckle 10 and later. |
| `AdCodicem.ValueObjects.FluentValidation` | Rules that reuse what the value object already enforces. |
| `AdCodicem.ValueObjects.Dapper` | Type handlers for raw SQL. |
| `AdCodicem.ValueObjects.MongoDB` | MongoDB.Driver serializers: the bare value in BSON, `.Value` in LINQ, strict reads, a `$jsonSchema` validator from the rules. |
| `AdCodicem.ValueObjects.MessagePack` | MessagePack formatters and SignalR's MessagePack hub protocol: the bare value on the wire, strict reads. |
| `AdCodicem.ValueObjects.NewtonsoftJson` | Interop with code that has not moved to `System.Text.Json`. |
| `AdCodicem.ValueObjects.Serilog` | Serilog logs a value object as its underlying value, with `@` and, on request, without. AOT-compatible. |
| `AdCodicem.ValueObjects.AI` | Microsoft.Extensions.AI: the rules in tool and structured-output schemas; a refused tool argument answered with its code. AOT-compatible. |
| `AdCodicem.ValueObjects.ModelContextProtocol` | MCP C# SDK: the rules in tool input and output schemas; a refused tool argument answered with its code, as a tool execution error. AOT-compatible. |
| `AdCodicem.ValueObjects.Identifiers[.EntityFrameworkCore\|.MongoDB]` | Stripe-style public identifiers, their columns, their minting on insert. See `identifiers.md`. |
| `AdCodicem.ValueObjects.Testing` | The xUnit contract kit. |
| `AdCodicem.ValueObjects.Testing.Data` | Values each type accepts, drawn from its rules; the values at its edges and the values its schema rules out. |
| `AdCodicem.ValueObjects.AutoFixture` | An AutoFixture customization creating every value object from its rules. |
| `AdCodicem.ValueObjects.Bogus` | `RuleForValueObjects()` on a `Faker<T>`, and `faker.ValueObject<T>()`, deterministic under `UseSeed`. |
| `AdCodicem.ValueObjects.FsCheck` | Arbitraries merged into an `ArbMap`, biased towards the edges, shrinking to accepted values. |

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

// Under native AOT, a hand-written value object is registered, with no converter of its own: the factory serves it
// a general-purpose one, closed through its descriptor. List it in the context, which brings its underlying type along.
// Left unregistered, it fails its first serialization with a NotSupportedException.
ValueObjectRegistry.Register<Link, Uri>(Link.Schema);
```

Writing refuses what reading would: an uninitialized instance (a member never set, a default array element) whose
default value its type rejects throws `ValueObjectJsonException` (a `JsonException`) from System.Text.Json and
`JsonSerializationException` from Newtonsoft.Json, as a value or a dictionary key. A type whose zero is valid (`Amount`
with `Minimum` 0, an unconstrained `Guid`) writes it.

**Every exception an integration throws for a refused value carries the rule's code.** System.Text.Json:
`ValueObjectJsonException.ErrorCode`, beside `ValueObjectType`. Newtonsoft.Json's `JsonSerializationException`, Dapper's
`DataException`, MongoDB.Driver's `FormatException` (read) and `BsonSerializationException` (write), MessagePack's
`MessagePackSerializationException`, and the `XmlException` of `XmlSerializer` and `DataContractSerializer` in an
assembly marked `[assembly: ValueObjectXmlSerialization]`, inside the serializer's own exception:
`exception.Data[ValueObjectErrors.ErrorCodeKey]`. EF Core: the `ValueObjectException` inside `DbUpdateException`. Read
any of them, wrapped or not, with `ValueObjectErrors.TryGetCode(exception, out var code)`. A token or column not of the
underlying type carries `value_object.not_parsable`, a `null` for a value object that cannot be `null`
`value_object.required`. An integration of your own sets `exception.Data[ValueObjectErrors.ErrorCodeKey] = code`. For a
gRPC `ErrorInfo.reason`, map the code with `ValueObjectErrorCodes.ToUpperSnakeCase(code)` (`value_object.too_long` →
`VALUE_OBJECT_TOO_LONG`); it throws `ArgumentException` for a code that maps to no valid reason.

**JSON Schema** (`JsonSchemaExporter`, and every host built on it: Microsoft.Extensions.AI tools and structured
output, the MCP SDK, Agent Framework) describes a value object as `true` and a `List<Iban>` without `items`. For
Microsoft.Extensions.AI, use `AdCodicem.ValueObjects.AI`'s `WithValueObjects()` (below, Language models); for an MCP
server, `AdCodicem.ValueObjects.ModelContextProtocol`'s `WithValueObjectTools<T>()` (below). Plug
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
so it fits a host's own hook (`UseRecommendedSerializerSettings(s => s.AddValueObjects())` for Hangfire). For MVC,
call `AddNewtonsoftJson().AddValueObjectsNewtonsoftJson()` instead, from
`AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson` (ASP.NET Core below):

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
as MVC writes them; not through a System.Text.Json input formatter the application built with options of its own or
derived, which is left as it is. To remove the System.Text.Json input formatter, call
`InputFormatters.RemoveType<SystemTextJsonInputFormatter>()` in `AddMvcOptions` or `Configure<MvcOptions>`; a
`PostConfigure<MvcOptions>` registered after `AddValueObjects()` must remove every formatter that
`is SystemTextJsonInputFormatter`, since `RemoveType` matches the exact type and misses the package's.
`AddValueObjects()` also exists on `MvcOptions` for an application that configures MVC directly; it adds the binder
alone, and records no code for a body. The MVC binder
treats white-space text as it treats empty text, as absent: `?country=%20` binds `CountryCode?` to `null`, while a
value object that cannot be `null` is a 400 with `value_object.required`, as MVC answers blank text for an `int`.

**MVC on Newtonsoft.Json takes `AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson`** instead, as `AddNewtonsoftJson()`
replaces the System.Text.Json formatter and a body then records no code:

```csharp skip
builder.Services.AddControllers().AddNewtonsoftJson().AddValueObjectsNewtonsoftJson();   // in either order
builder.Services.Configure<ApiBehaviorOptions>(o => o.AddValueObjectProblemDetails());
```

- It calls `AddValueObjects()` itself (calling it too behaves the same), and adds `ValueObjectConverter` with
  `DateParseHandling.None` and `FloatParseHandling.Decimal` to MVC's `SerializerSettings` unless they already hold the
  converter once configured, so `AddNewtonsoftJson(o => o.SerializerSettings.AddValueObjects(decimalReals: false))`
  keeps its choice, before or after. MVC writes every response with those settings too, and they reach every member:
  a numeric or boolean value object goes out as `7`/`true` where the type converter wrote `"7"`/`"True"`, `"True"`
  for a boolean value object is refused, and an `object` or `JToken` member keeps a date-like string as a string and
  reads a real as a `decimal` (`1e30` is refused).
- It replaces MVC's `NewtonsoftJsonInputFormatter` in place with one deriving from it, over the same settings and
  options; a formatter the application built over its own settings or derived, the JSON Patch one included, is left
  alone and records no code. The model state, its messages and the exception policy are the framework's.
- Codes go under the key MVC gives the error, Newtonsoft.Json's path: `"errorCodes": { "email":
  "value_object.invalid_format", "lines[2].sku": "value_object.too_short" }`. Every refused property of a class body
  keeps its own code (Newtonsoft.Json reads on); a positional record stops at its first. None past
  `MaxModelValidationErrors`.
- A value object as a dictionary key is read through its type converter: no code, and the message quotes the key.
- Not AOT-compatible. In a `PostConfigure<MvcOptions>` registered after it, `RemoveType<NewtonsoftJsonInputFormatter>()`
  misses its formatter: remove every formatter that `is NewtonsoftJsonInputFormatter and not
  NewtonsoftJsonPatchInputFormatter` instead, leaving out any you derived yourself.

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

**`AdCodicem.ValueObjects.AspNetCore.Http` answers those refusals with MVC's problem details**, `errors` and
`errorCodes`, on the endpoints a convention covers. It is AOT-compatible and runs under the RDG:

```csharp skip
builder.Services.AddProblemDetails();
builder.Services.AddValueObjectHttpProblemDetails();   // the exception handler, for ThrowOnBadRequest

var app = builder.Build();
app.UseExceptionHandler();                             // wherever ThrowOnBadRequest is on

var api = app.MapGroup("/api").WithValueObjectProblemDetails();   // a group, one endpoint, or MapGroup("")
api.MapGet("/lines", (Quantity qty, CountryCode? country) => ...);
```

- Covered: route, query and header values under the name they bind from (`[FromRoute(Name)]`, `[FromQuery(Name)]`,
  `[FromHeader(Name)]`), `[AsParameters]` members, arrays of value objects from the query string (every refused
  element's message, the first one's code), and an absent required value object (`value_object.required`).
- A JSON body only with `RouteHandlerOptions.ThrowOnBadRequest` on (the Development default): the framework otherwise
  answers it before any filter. The registered `IExceptionHandler` lists it under its JSON path, `$.from`, never
  copying the framework's message; it needs `AddProblemDetails()` and `UseExceptionHandler()`, and runs in
  registration order among handlers. The package never turns `ThrowOnBadRequest` on.
- Left to the framework: a 400 no value object caused (an `int`, malformed JSON, even beside a refused query value), a
  form field, a header array. `AddValidation()`'s filter runs first: a request failing a DataAnnotations rule too gets
  that rule alone. A key a dictionary keyed by a value object refuses is listed under its JSON path, which holds it.
- Empty text follows the binder: reported under the reflection-based binding. The RDG refuses no empty query text, so
  nothing is reported there: it binds `null` to a `T?`, and the unchecked default instance to a `T` or an array
  element; declare the parameter `T?`. It takes an empty header for an absent one (`value_object.required`).
- Native AOT: register `AddProblemDetails()`, whose serializer context writes the response without reflection;
  without it the covered endpoints fail to build with an `InvalidOperationException` naming it. Register a value
  object written by hand or a generic construction (`ValueObjectRegistry.Register`), or its refusal is left
  unexplained. `ValueObjectProblemDetails` lives in this package, namespace `AdCodicem.ValueObjects.AspNetCore`,
  forwarded by the MVC package.

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
parameter, where the reflection-based binding answers 400, and the default instance, unchecked, to one that cannot be
`null`.

`AddValueObjectProblemDetails()` attaches the stable error code of the violated rule to the automatic 400
response of an **MVC controller**, under the extension named by `ValueObjectProblemDetails.ExtensionName`, so a client
can branch on `value_object.too_long` instead of parsing English. A member refused more than once (the elements of a
query array, a key repeated in a body Newtonsoft.Json reads) keeps the code of its first refusal, as a minimal API
does. It extends `ApiBehaviorOptions`, which minimal APIs never read; they use `AdCodicem.ValueObjects.AspNetCore.Http`
above. To carry the same codes out of a manually validated payload, fill that extension yourself:

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

Under native AOT, list `Dictionary<string, string>` in the `JsonSerializerContext` chained into
`ConfigureHttpJsonOptions`, or build the codes as a `JsonElement`: the framework's context for problem details knows
no dictionary, and the response otherwise fails to serialize.

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
- Under native AOT, Dapper's own handler cache fails inside Dapper, however the handler was built. Use Dapper.AOT,
  which ignores `SqlMapper`'s table: declare each handler at module level,
  `[module: TypeHandler(typeof(Iban), typeof(ValueObjectTypeHandler<Iban, string>))]` beside `[module: DapperAot]`,
  and keep `AddValueObjectHandlers` for the calls Dapper.AOT does not intercept. `QuerySingle<Iban>` is `DAP037`
  there: query the underlying type and `Create` the value object from it.

## MongoDB

```csharp skip
BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)); // first
ValueObjectBson.Register(typeof(Iban).Assembly);   // once, at start-up, before anything is serialized
```

Without it, MongoDB.Driver writes a value object as `{}`, reads back a default instance, and a filter over one matches
every document or none, silently. `Register` adds a provider that gives every value object a
`ValueObjectBsonSerializer<TSelf, TValue>` over the driver's serializer of the underlying type, so it stores exactly
what the primitive stores, the application's `Guid` and `DateTime` conventions included.

- Order matters: the driver caches every serializer for the process. Register serializers of your own first, the
  `GuidSerializer` before all, then `Register`, before any document is serialized or any class map built. `Register`
  throws `InvalidOperationException` when a value object over `Guid` meets the driver's default `GuidSerializer`
  (`GuidRepresentation.Unspecified`), and when the driver already mapped a value object through a class map (a generic
  construction or one written by hand included). A second call with the other trust throws too.
- Reads are strict: `TryCreate` normalizes and validates; a refusal is a `FormatException` (inside the driver's, which
  names the member, when it reads a document) carrying the rule's code in `Data`: `ValueObjectErrors.TryGetCode`.
  Whatever the trust, a BSON `null` read into an `Iban` is `value_object.required` (declare `Iban?`), and a BSON value
  the underlying serializer cannot read (another type, out of range) `value_object.not_parsable`. A field missing from
  the document leaves the default instance: `[BsonRequired]`.
- A collection only the application writes: `ValueObjectBson.Register(trusted: true, assembly)`, per type
  `ValueObjectBson.Register<Iban, string>(trusted: true)` before the driver asks for it, or per member on a class map,
  `map.MapMember(x => x.Iban).SetSerializer(new ValueObjectBsonSerializer<Iban, string>(stringSerializer, trusted: true))`.
- A write of an uninitialized value object whose default its type rejects throws `BsonSerializationException`, from
  `InsertOne`, `ReplaceOne`, an update and a query constant alike (inside the driver's, naming the member, when it
  writes a document); an `Iban?` holding nothing is written `null`. An update through `.Value`
  (`Update.Set(x => x.Page.Value, 0)`, `Inc`, `Mul`, `Min`, `Max`) writes the raw value unchecked: set the value object,
  `Update.Set(x => x.Page, page)`.
- Query value objects as their values: `x.Iban == iban`, `x.Iban.Value.StartsWith("FR")`, `x.Quantity.Value > 2`,
  `ids.Contains(x.Id)`, `Update.Inc(x => x.Quantity.Value, 1)` all translate on the field itself. Equality is the
  server's, exact: a case-insensitive value object matches only the spelling stored. To find stored values the type
  refuses, compare `.Value`; a constant the type refuses is refused.
- `[BsonRepresentation]`, `[BsonGuidRepresentation]` and `[BsonDateTimeOptions]` on a value-object member reach the
  underlying serializer as on the primitive.
- A value object over `Int128` or `UInt128` is refused both ways (MongoDB.Bson has no serializer for either) unless
  you register a serializer of your own for the underlying type first.
- A generic construction is described by reflection on first use;
  `ValueObjectBson.Register<Reference<PurchaseOrder>, string>()` registers it without. Not AOT-compatible: the driver
  itself is not. Azure Cosmos DB for MongoDB and Azure DocumentDB use the same driver. MongoDB.EntityFrameworkCore goes
  through `ConfigureValueObjects` instead.

### A collection validator from the rules

```csharp skip
await database.CreateCollectionAsync("orders", new CreateCollectionOptions<BsonDocument>
{
    // at start-up, after the registrations: see below
    Validator = new BsonDocumentFilterDefinition<BsonDocument>(ValueObjectBsonSchema.For<Order>()),
});
// an existing collection: RunCommand { collMod: "orders", validator: ValueObjectBsonSchema.For<Order>() }
```

- `For<TDocument>()` builds the class maps of the document and of its nested classes, which the driver keeps for good:
  call it after `ValueObjectBson.Register`, after `EntityIdBson.Register` and after any `BsonClassMap.RegisterClassMap`
  of those types.
- It walks the class map (element names and serialization options included) and returns `{ "$jsonSchema": … }`:
  `bsonType` from what the underlying serializer writes, `maxLength`, `minLength`, the pattern in PCRE2, each class
  escape listed as the characters .NET matches it with (the server's own Unicode tables lag .NET's), `minimum`/`maximum`
  as the serializer writes a number, `enum` for a closed set, `description`; a value object member is `required` unless
  the class map may leave it out; a `TSelf?` adds `null`; nested documents and arrays are described where they hold a
  value object; dictionaries and members with a serializer of your own are left free.
- Each rule is carried only where the server refuses no value the type accepts: `minLength` is halved unless the
  published pattern is anchored at both ends (the server counts an emoji once); a pattern with `.`, a negated class,
  `\D`/`\W`/`\S`/`\P{…}`, `\p{C}`, a surrogate, `\b`, a class subtraction or a named block is left out, and so is
  one whose regex sets `IgnoreCase`, `Multiline` or `IgnorePatternWhitespace`; a value object written by hand without
  the pattern hook has its `Schema.Pattern` published as declared, which its validation must run with no option; no
  `enum` for a set compared ignoring case or by culture, nor for reals or decimals stored as text, nor for dates; no
  bound for text, dates, under `AllowOverflow`, for a time or a duration written as an Int32, or through a serializer
  of the underlying type that is not MongoDB.Bson's.
- The server refuses with a `MongoWriteException` (`MongoBulkWriteException` from `InsertMany`), write error code 121,
  "Document failed validation", naming the field and keyword; no rule code. It checks updates too, so it catches an
  `Update.Inc(x => x.Quantity.Value, …)` past the maximum. `bsonType` refuses a number of another BSON type the reader
  would accept (`5L` for an `int` value object), and `required` a document missing a member the reader would leave as
  it was. `validationLevel`/`validationAction` are yours to set.
- `For<TDocument>()` throws `InvalidOperationException` before `ValueObjectBson.Register`, and for a document the driver
  serializes without a class map.

## MessagePack and SignalR

```csharp skip
var options = MessagePackSerializerOptions.Standard.WithValueObjects(); // or WithValueObjects(trusted: true)
MessagePackSerializer.DefaultOptions = options;                          // for code that passes no options

builder.Services.AddSignalR().AddMessagePackProtocol(o => o.UseValueObjects());        // the server
new HubConnectionBuilder().WithUrl(url).AddMessagePackProtocol(o => o.UseValueObjects()); // and the .NET client
```

Without it, MessagePack's `StandardResolver` throws `FormatterNotRegisteredException` on the first value object, and a
contractless resolver, SignalR's own included, writes `{}` and reads back a default instance, silently.
`WithValueObjects` returns options whose resolver is `ValueObjectResolver` in front of the options' own (security and
compression kept); `UseValueObjects` does the same to `MessagePackHubProtocolOptions.SerializerOptions` and returns the
options. The last `WithValueObjects` call answers first.

- A value object is written as the bare value the options' formatter of its underlying type writes, byte for byte the
  primitive's: a `Guid` as text under `Standard`, 16 bytes under `NativeGuidResolver`; `decimal` and `DateTime` follow
  their `Native*` resolvers too. A `TSelf?` is MessagePack's `NullableFormatter` (`nil` for none).
- Reads are strict: `TryCreate` normalizes and validates; a refusal is a `MessagePackSerializationException` (inside
  MessagePack's own, "Failed to deserialize … value.") carrying the rule's code in `Data`:
  `ValueObjectErrors.TryGetCode`. Whatever the trust, a `nil` read into an `Iban` is `value_object.required` (declare
  `Iban?`), and a value the underlying formatter cannot read (another type, out of range, truncated)
  `value_object.not_parsable`. `trusted: true` reads through `CreateUnchecked`, for bytes only the application wrote.
- A write of an uninitialized value object whose default its type rejects throws `MessagePackSerializationException`,
  as a value, a dictionary key or inside an `Iban?`.
- SignalR: both ends call `UseValueObjects()`. A refused argument fails the invocation before the hub method runs: the
  client gets `HubException` ("Failed to invoke 'Add' due to an error on the server.", plus the binding failure with
  `EnableDetailedErrors`), never the code; the server logs `InvalidHubParameters` at `Debug` with the code in the
  exception's chain. A .NET client refuses to send a default its type rejects; a hub returning one closes the
  connection. A client without `UseValueObjects()` sends `{}`, which a wired server refuses.
- Under `MessagePackSecurity.UntrustedData`, which SignalR sets, a dictionary keyed by a value object or a set of them
  is refused (`TypeAccessException`): key by the underlying type, or derive `MessagePackSecurity` and override
  `GetHashCollisionResistantEqualityComparer<T>()` to return `EqualityComparer<T>.Default` for a value object (and
  `Clone()`), at the cost of collision resistance for those keys. Keys normalize on a strict read: two the sender held
  apart that normalize to one value object fail the dictionary with MessagePack's `ArgumentException`, which quotes the
  key and carries no code.
- MessagePack's analyzer comes with the package: `MsgPack003` fails a `[MessagePackObject]` type holding a value object
  of the same assembly. Declare value objects in a referenced project, or add
  `[assembly: MessagePackAssumedFormattable(typeof(Iban))]` per type.
- Not covered: MassTransit.MessagePack (use MassTransit's System.Text.Json serializer), `[MessagePackKnownFormatter]`
  (list `ValueObjectResolver.Instance` first in a compile-time composite instead), `MessagePackSerializer.Typeless`,
  `AnyEntityId`, Nerdbank.MessagePack.
- A generic construction or a value object written by hand is described by reflection on first use. Not
  AOT-compatible: MessagePack and SignalR's MessagePack hub protocol are not.

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
`minimum`, `format`, `enum` for a closed set, plus the example `IValueObjectExample<TSelf>` declares and
`Description`. A closed set names its values after its known values in `x-enum-varnames` (openapi-generator,
Scalar), `x-enumNames` (NSwag) and `x-ms-enum` (Kiota,
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

### Swashbuckle

```csharp skip
builder.Services.AddSwaggerGen(o =>
{
    o.IncludeXmlComments(xmlCommentsPath);   // if used: before, or its filter replaces the value objects' descriptions
    o.AddValueObjects();                     // or o.AddValueObjects(serializerOptions) when MVC's and minimal API's differ
});
```

`AdCodicem.ValueObjects.Swashbuckle`, for Swashbuckle 10 and later, describes a value object as the built-in package
does, with these differences, all Swashbuckle's own:

- A number is a number (`integer` or `number`, no numeric pattern), as Swashbuckle documents the underlying type,
  whatever the options let be read from text; examples and known values are written under the minimal API options. An
  `Int128` or `UInt128` value object travels as a string and stays a `string` with `x-minimum`/`x-maximum`, where
  Swashbuckle documents a bare one as `integer`/`int128`.
- A route, query or header parameter refers to the component (`$ref`), minimal APIs included; a route constraint beside
  it is dropped, unless `UseAllOfToExtendReferenceSchemas` keeps it beside an `allOf`.
- A nullable property, element or dictionary value (an enumeration-keyed dictionary's included) is described in place
  with `null` allowed, as a nullable `int` is, but without the member's validation attributes (`[MaxLength]`, `[Range]`),
  which Swashbuckle never applies beside a value object's reference; put the rules on the type. Under
  `UseAllOfToExtendReferenceSchemas` it is described inside Swashbuckle's `allOf` wrapper, which keeps them. A
  `[Required]` property (on the member or its `[ModelMetadataType]`), a nullable parameter and a nullable whole body keep
  the reference.
- The document is OpenAPI 3.0 by default (`example`, `nullable`, `x-jsonschema-propertyNames`);
  `UseSwagger(o => o.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1)` writes 3.1.
- A closed set's `x-ms-enum` is named after Swashbuckle's component, `AccountFilterNoticeChannel` for
  `NoticeChannel<AccountFilter>`, or the identifier `CustomSchemaIds` gives it.
- Swashbuckle 10 needs `Microsoft.OpenApi` 2: never reference `Microsoft.AspNetCore.OpenApi` 11 beside it, which brings
  3 and makes Swashbuckle throw `MissingMethodException` when it builds a document.

## Serilog

```csharp skip
using AdCodicem.ValueObjects.Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()                       // every enricher adding value objects: before the call
    .Destructure.ValueObjects(o =>
    {
        o.CaptureAsUnderlyingValue = true;          // {Qty} too, not only {@Qty}
        o.Assemblies.Add(typeof(Iban).Assembly);    // under the JIT, when the logger is built before the domain is used
    })
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();
```

- `Destructure.ValueObjects()` alone: `{@Qty}`, and every value object inside what is logged with `@`, is its
  underlying value, `"Qty":42`, any `IValueObject` included, nothing registered. `{Qty}` stays its text, `"42"`.
- `CaptureAsUnderlyingValue`: `{Qty}` is the underlying value too, for filters (`Qty > 10`), queries and template
  formats (`{Qty:000}`), and for `LogInformation`, `[LoggerMessage]` and scopes with Serilog behind
  Microsoft.Extensions.Logging. It reads the registry when the call runs: name the domain's assemblies in
  `Assemblies`, and add `.Destructure.AsScalar<Code<Order>>()` for a construction of a generic value object that
  nothing registered or resolved before the call, or a value object written by hand that nothing registered.
- Never `Destructure.AsScalar<T>()` a value object without the option: `{@Qty}` then becomes `"42"`.
- Declare an application's own `.Destructure.ByTransforming<Iban>(…)` (a mask) or policy before
  `Destructure.ValueObjects`, or the package's policy runs first. Under `CaptureAsUnderlyingValue` it never runs for a
  value object the registry holds, nested or not: log `iban.ToString("M", null)` instead.
- Call `Enrich.FromLogContext()` before `Destructure.ValueObjects`: a property an enricher added after it is the value
  object's text.
- A value object is logged as its underlying value, never its formatting hook's text: `{$Temp}` or `temp.ToString()`
  for the text. A named format, a mask included (`{Iban:M}`), is ignored: log `iban.ToString("M", null)`.
- `Int128`/`UInt128` are digits in a JSON string; a default instance is the default of its underlying type; a
  dictionary keyed by a value object needs `.Destructure.AsDictionary<Dictionary<Quantity, int>>()`; `AnyEntityId` is
  logged without `@`.
- Under native AOT, Serilog destructures no object: an object logged with `@` is its `ToString()`. A value object alone
  or in a collection is logged as under the JIT.
- A value object classified as personal data is logged in clear: redaction is not done by this package yet.

## Language models (Microsoft.Extensions.AI)

```csharp skip
using AdCodicem.ValueObjects.AI;

AIFunction placeOrder = AIFunctionFactory.Create(
        tools.PlaceOrder,
        new AIFunctionFactoryOptions { JsonSchemaCreateOptions = new AIJsonSchemaCreateOptions().WithValueObjects() })
    .WithValueObjectValidation();

// Structured output: the format, then the answer read as GetResponseAsync<T> reads it.
var response = await chatClient.GetResponseAsync(messages,
    new ChatOptions { ResponseFormat = ValueObjectResponseFormat.ForJsonSchema<Order>(AppJsonContext.Default.Options) });
var order = new ChatResponse<Order>(response, AppJsonContext.Default.Options).Result; // ValueObjectJsonException on a refusal
```

- `WithValueObjects()` copies the options (`null` = `AIJsonSchemaCreateOptions.Default`, left untouched), runs any
  transform they carry first, and describes each value object under the language-model profile, a `[Description]` on
  the parameter first.
- `WithValueObjectValidation()` checks each argument through the parameter's contract in the function's serializer
  options, as binding does, and returns the first refused, in parameter order, instead of calling the function:
  `{"error":"invalid_argument","argument":"quantity","code":"value_object.out_of_range","message":"…"}`. The message is
  the converter's, never the value, or `The value is not a valid Quantity.` where the converter writes none (a number
  the underlying type cannot hold) or a converter of your own refuses with its own exception (code from its `Data`, or
  `value_object.not_parsable`). An absent argument with no default, and a `null` for a non-nullable value object,
  are `value_object.required`. A collection, dictionary or object parameter is refused only for a value object it
  holds. An argument handed over as text or as a number is read as the binding converts it, JSON first. Wrapping twice
  returns the same function.
- Without the wrapper, `FunctionInvokingChatClient` answers a refusal "Error: Function failed.", and `AIFunctionFactory`'s
  binding hands a `null` to a non-nullable value object as its uninitialized instance. Keep `IncludeDetailedErrors` off.
- Agent Framework: the same two calls on the tools of a `ChatClientAgent`. Its `RunAsync<T>` replaces the response
  format with one without the rules: pass
  `new AgentRunOptions { ResponseFormat = ValueObjectResponseFormat.ForJsonSchema<T>(options) }` to a plain
  `RunAsync`, read the answer with `new AgentResponse<T>(response, options).Result`, and give an answer that is no
  object (a list, a lone value object) a record of its own.
- Reflection-free or native AOT: `AIFunctionFactory`'s default options throw `NotSupportedException`. Pass
  `SerializerOptions` from a source-generated context with `Converters = [typeof(ValueObjectJsonConverterFactory)]`
  listing the parameter types (the value objects, not their underlying types) and the result type.

## Model Context Protocol servers

```csharp skip
using AdCodicem.ValueObjects.ModelContextProtocol;

// A tool type: in place of WithTools<OrderTools>().
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithValueObjectTools<OrderTools>();
```

```csharp skip
// A tool created by hand, here on one instance: Create, then WithValueObjectTools(tools), not the SDK's
// WithTools(tools) alone, and chained on the same builder as the other registrations.
var placeOrder = ValueObjectMcpServerTool.Create(
    typeof(OrderTools).GetMethod(nameof(OrderTools.PlaceOrder))!,
    orderTools,
    new McpServerToolCreateOptions { Name = "place_order" });

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithValueObjectTools([placeOrder]);

// A server created without dependency injection: AddValueObjectValidation() on its options, before McpServer.Create.
var options = new McpServerOptions { ToolCollection = [placeOrder] }.AddValueObjectValidation();
```

- `WithValueObjectTools<T>()`, `WithValueObjectTools(IEnumerable<Type>)` (static classes too) and
  `WithValueObjectToolsFromAssembly()` mirror the SDK's `WithTools<T>()`, `WithTools(types)` and
  `WithToolsFromAssembly()`: same `[McpServerTool]` methods, public or not, an instance of `T` per call of an instance
  method, never taken from the services. The two non-generic ones are `[RequiresUnreferencedCode]`. The SDK's
  `WithTools<T>(T target)` has no counterpart: `Create(method, target)` for each method, then
  `WithValueObjectTools(tools)`.
- The rules go into each tool's `inputSchema`, and into its `outputSchema` with `UseStructuredContent` (an
  `OutputSchemaType` too), under the language-model profile. The tools stay the SDK's own: never wrap one in a
  `DelegatingMcpServerTool`, which loses the SDK's output schema for clients before protocol `2026-07-28` and the check.
- A refused argument, the first in parameter order, is answered with `isError: true`, the text
  `Argument 'quantity' rejected (value_object.out_of_range): The value is not a valid Quantity: …`, and the structured
  content `{"error":"invalid_argument","argument":…,"code":…,"message":…}` only when the tool declares no output
  schema. Same codes and messages as `WithValueObjectValidation()` above; the tool is not called, nor its instance
  created. Parameters the SDK binds itself (`McpServer`, `RequestContext<…>`, `IProgress<…>`, services,
  `CancellationToken`) are never read.
- The check is a call-tool filter added once, after the filters the options are configured with, innermost. A filter
  added in `HttpServerTransportOptions.ConfigureSessionOptions` runs inside it: call
  `options.AddValueObjectValidation()` after adding it. The SDK refuses the check beside an explicit
  `CallToolWithAlternateHandler` (`MCPEXP002`), and throws when it creates the server: at start over stdio, for each
  session (each request when stateless) over HTTP.
- A value object returned as structured content is held to the `outputSchema`, which a client may validate it against:
  give a closed set looked up ignoring case a normalizer that returns each known value in its declared spelling, or a
  result spelled otherwise falls outside the schema's `enum`.
- Reflection-free or native AOT: pass `serializerOptions`, a copy of `McpJsonUtilities.DefaultOptions` with your
  source-generated context inserted first in `TypeInfoResolverChain` and `new ValueObjectJsonConverterFactory()` added
  to `Converters`; the context lists the parameter and result types. Without it the server throws
  `NotSupportedException` when first resolved.

## XML

`XmlSerializer` and `DataContractSerializer` write a value object as an empty element and read back a default instance,
unless its assembly opts in, once, beside the value objects:

```csharp
using AdCodicem.ValueObjects;
using AdCodicem.ValueObjects.Annotations;

[assembly: ValueObjectXmlSerialization(Namespace = "urn:shipping")]

namespace Shipping;

[ValueObject<int>]
public readonly partial struct Quantity : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    public static int Minimum => 1;

    public static int Maximum => 100;
}
```

- Every value object and `[EntityId]` of the assembly then implements `IXmlSerializable`: no package, no wiring. A
  project file sets the same attribute with
  `<AssemblyAttribute Include="AdCodicem.ValueObjects.Annotations.ValueObjectXmlSerializationAttribute" />`.
  `Namespace` names the XSD namespace of the schema types; without it, `DataContractSerializer`'s
  `http://schemas.datacontract.org/2004/07/{CLR namespace}`. With it, value objects of one name in two CLR namespaces
  (`Billing.Code`, `Freight.Code`) share one schema type: give them distinct names, or the schema provider throws
  `InvalidOperationException` when their rules differ.
- Written as the underlying value, in `XmlSerializer`'s form for that type: a `char` as its code, a `TimeSpan` as
  ISO 8601 (`PT1H30M`, not JSON's `01:30:00`), a `double` infinity as `INF`, `Int128` as digits. A document written
  while the member was the primitive reads back, but a bare `DateTimeOffset` `DataContractSerializer` wrote (a pair of
  elements) and a bare `Int128` (written empty) are refused as `value_object.not_parsable`.
- Read through `TryCreate`: a refused value is an `XmlException` carrying the rule's code in its `Data`, inside
  `XmlSerializer`'s `InvalidOperationException` or `DataContractSerializer`'s `SerializationException`; read it with
  `ValueObjectErrors.TryGetCode`. An empty element is `""` (refused unless `AllowEmpty`) or not parsable; `xsi:nil` on
  a member that cannot be null is `value_object.required` through `XmlSerializer`, and `DataContractSerializer`'s own
  `SerializationException`, without a code; a missing element is never read, so the member keeps its default instance:
  `[DataMember(IsRequired = true)]`.
- A default instance its type refuses fails the write, with its code.
- `GetXmlSchema` describes the type as an `xs:simpleType`: lengths, bounds, a closed set's values, the description,
  and the pattern when XSD reads it alike (anchored at both ends; else left out). `XsdDataContractExporter` includes
  it; `XmlSchemaExporter` only refers to it: call `GetXmlSchema` on the set to complete it.
- Never mark a value-object member `[XmlAttribute]` or `[XmlText]`: `XmlSerializer` refuses the type.
- The MVC XML formatters answer a refused body 400 with no code. `DataContractSerializer` needs dynamic code to read
  a value object: not under native AOT.
- Never call `ReadXml` on a variable: it changes the instance it is called on.
- A value object written by hand implements `IXmlSerializable` through `ValueObjectXml.Read<TSelf, TValue>`,
  `Write<TSelf, TValue>` and `ProvideSchema<TSelf, TValue>`.

## Test data

Never write a generator, a fixture registration or a Bogus rule that restates a value object's rules.

```csharp skip
var options = new ValueObjectSamplerOptions().Use<Iban, string>(random => /* an IBAN with its check digits */ ...);

// AutoFixture
var fixture = new Fixture().Customize(new ValueObjectCustomization(options));

// Bogus
var orders = new Faker<Order>()
    .StrictMode(true)
    .RuleForValueObjects(options)              // first: a rule written before it is replaced
    .RuleFor(o => o.Note, f => f.Lorem.Sentence());
var iban = new Faker().ValueObject<Iban, string>(f => f.Finance.Iban());

// FsCheck, from a [Fact]
var arbitraries = ArbMap.Default
    .MergeValueObjects(typeof(Iban).Assembly, options)
    .MergeValueObject<Reference<PurchaseOrder>, string>(); // a construction, or a value object written by hand
Prop.ForAll(arbitraries.ArbFor<Order>(), order => /* ... */).Check(Config.QuickThrowOnFailure);
```

- A checksum or another validator no schema carries: `Use<TSelf, TValue>` on the options, or `fixture.Register(...)`
  in AutoFixture, which wins over the customization before or after it.
- Bogus: a nullable member gets a value, an array or a `List<T>` one to three; an array or list of a nullable value
  object gets no rule. `new Faker<Iban>()` does not compile (CS0452): use `faker.ValueObject<Iban>()`.
- FsCheck: `MergeValueObjects` merges what the assembly's generated registration lists; a construction of a generic
  value object, or a hand-written one nothing registers, needs `MergeValueObject<TSelf, TValue>()`, or FsCheck answers
  "not handled automatically". One value in four is a boundary. A counterexample shrinks to accepted values: the
  declared ones (`Minimum`, first known value, example), then FsCheck's shrinks of the underlying value the normalizer
  leaves unchanged; over `bool`, `Guid`, `Int128`, `UInt128`, `DateOnly` or `TimeOnly`, to the declared ones alone.
  A property also runs from FsCheck.Xunit.v3's `[Property(Arbitrary = [typeof(Arbitraries)])]`, `Arbitraries` being
  a static class of yours whose static members return `ValueObjectArbitrary.For<TSelf, TValue>()`; FsCheck.Xunit is
  for xUnit 2.
- A `ValueObjectSamplingException` names the registration to add, in the library's terms.

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

An entity identifier's descriptor, from `EntityIdRegistry`, has its own `Accept(IEntityIdVisitor<TResult>)`, whose
`Visit<TId>() where TId : struct, IEntityId<TId>` reaches the members only an identifier has, `TId.New()` among them;
use it rather than `MakeGenericType` too (this is how `EntityIdBson.Register` closes its id generators):

```csharp skip
sealed class GeneratorFor : IEntityIdVisitor<IIdGenerator>
{
    public IIdGenerator Visit<TId>()
        where TId : struct, IEntityId<TId>
        => new EntityIdGenerator<TId>(); // mints through TId.New()
}

foreach (var identifier in EntityIdRegistry.GetRegistered())
{
    BsonSerializer.RegisterIdGenerator(identifier.ValueObjectType, identifier.Accept(new GeneratorFor()));
}
```

Inside typed code, the rules are `TSelf.Schema` (`TSelf.Schema.MaxLength`), a static member of
`IValueObject<TSelf, TValue>`: no registry lookup.

Registration happens through a generated `[ModuleInitializer]`, so nothing needs registering by hand — but a
module initializer only runs once its assembly is loaded, which is what `EnsureAssemblyRegistered(assembly)`
forces (the EF Core, Dapper and MongoDB entry points already call it). A generic value object registers its definition
(`GetRegisteredGenericDefinitions()`), and `TryResolve` describes a construction, by reflection, once asked for it;
`TryGet` finds it from then on. A hand-written value object is described by reflection the same way. Under native AOT,
register each construction instead,
`ValueObjectRegistry.Register<Code<Order>, string>(static () => new Code<Order>.ValueJsonConverter())`, and each
hand-written value object, `ValueObjectRegistry.Register<Link, Uri>(Link.Schema)`. `TryGet` and `TryResolve` also
unwrap `Nullable<T>`, and return the descriptor of the value object itself: close a generic type over
`descriptor.ValueObjectType`, never over the type asked for, and handle `null` before calling `GetValue`, `Format` or
`ValidateWrite`, which take a non-null instance (leave it to the host's nullable wrapper where it has one).
`IsValueObject` and `GetUnderlyingType` answer the cheap questions. A value
object is a struct implementing `IValueObject<TSelf, TValue>` over itself: `IsValueObject` is `true`, and
`GetUnderlyingType` other than `null`, exactly for what `TryResolve` describes, and every integration claims a type by
that rule.

This boxed path is for callers that only know a `Type` at run time. Domain code and the integrations above use
the typed path — the static abstract members of `IValueObject<TSelf, TValue>` — which neither boxes nor
allocates.
