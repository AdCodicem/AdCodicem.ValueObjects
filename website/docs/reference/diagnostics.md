---
title: Diagnostics
sidebar_label: Diagnostics
slug: /reference/diagnostics
description: Every VO diagnostic the generator and the analyzers report, what it means, and how to fix it.
---

# Diagnostics

Every rule the generator, the analyzers and the compiler enforce. `VO0008`, `VO0011`, `VO0025`, `VO0026`, `VO0033`,
`VO0042` and `VO0044` are warnings; every other one is an error. There is no `VO0012`, and `VO0004`, `VO0006`, `VO0013`, `VO0014`, `VO0022`
and `VO0029` are [retired](#retired-diagnostics).

| Id | Meaning | Fix |
| --- | --- | --- |
| `VO0001` | The type is not `partial`. | Add `partial`: the generated members are added to the same type. |
| `VO0002` | The type is not a `readonly struct`, or is a record or a `ref struct`. | Declare a `readonly partial struct`. A `record struct` is refused on purpose, because `with` and field-wise equality would bypass validation and the declared comparison; a class, because a value object is a value; a `ref struct`, because it can be neither boxed nor a type argument, and the generated members make it both. |
| `VO0003` | Unsupported underlying type. | Use one of the [supported types](../authoring-guide.md#supported-underlying-types). |
| `VO0005` | A closed value set declares no value. | Mark at least one member `[KnownValue]`, `public static readonly CountryCode France = Known("FR");`, or drop `ValueSet = ValueSetKind.Closed`: as declared, no value could be valid. It is not reported when every member marked `[KnownValue]` is refused by `VO0036`, which says what to fix. |
| `VO0007` | Arithmetic requested on a non-numeric type. | Remove `Arithmetic = true`, or change the underlying type. |
| `VO0008` | Length constraints on a non-string type (warning). | `MinLength` and `MaxLength` apply to `string` only; bound a number through `IValueObjectMinimum<T>` and `IValueObjectMaximum<T>`. |
| `VO0009` | A containing type is not `partial`. | Every enclosing type must be `partial`, not only the value object. |
| `VO0010` | An uninitialized value object or entity identifier: `default(T)` or `new T()`. | Construct through `Create`, `TryCreate` or `Parse` (or `New()` for an identifier), and express absence as `T?`. If the zero state is genuinely meaningful, set `AllowDefault = true` on the type, on `[ValueObject<T>]` or `[EntityId]` alike. It is reported where `default` or `new` is written: a parameter declared `Iban iban = default`, a method's, a lambda's or a local function's alike, is reported once, on its declaration, and not at each call that leaves the argument out. It sees source only: [Where a default instance can come from](./default-instances.md) lists what builds one at run time. |
| `VO0011` | A rule written without its hook interface (warning), on a value object or an entity identifier. | Declare the interface — `IValueObjectNormalizer<T>`, `IValueObjectValidator<T>`, `IValueObjectFormatter<T>`, `IValueObjectStringFormatter<T>`, `IValueObjectPatternValidator` for a public `static Regex Pattern` on a string value object, which is left alone when it is not public, or when the type implements another hook, since that hook may already run it, or `IValueObjectMinimum<T>` and `IValueObjectMaximum<T>` for a public static `Minimum` or `Maximum` property of the underlying type on a value object that takes a bound, which is left alone when the type implements `IValueObjectValidator<T>` or `IValueObjectNormalizer<T>`, since the validator may already check it and the normalizer clamp to it, and `IValueObjectExample<TSelf>` for a public static `Example` property of the type's own type. A field is left alone: it could not implement the hook. Until then the rule never runs. On an `[EntityId]` it reports a validator or a formatter; a normalizer there never runs at all, interface or not, and is `VO0017`'s. |
| `VO0015` | A malformed entity identifier prefix. | One or more lower-case segments separated by `_`, each starting with a letter: `"acc"`, `"sk_live"`. |
| `VO0016` | Two types claim the same prefix. | Give each identifier type its own prefix, or one kind of identifier would parse as another. |
| `VO0017` | A normalization hook on an entity identifier. | `[EntityId]` normalizes its own format and would never call the hook: remove it. |
| `VO0018` | Both `[EntityId]` and `[ValueObject<T>]` on one type. | Each generates a whole implementation; keep the one that describes the type. |
| `VO0019` | The generated code cannot reopen, reach or name the type. | Nothing is generated for the type, and the message says which rule it broke and what to do. The generated code reopens the type in a file of its own, so neither it nor a type around it can be `file`-local. It reaches a `private`, `protected` or `private protected` type through a step nested in each type around it, called by name, so no such type can sit inside a generic type: make it `internal` or `public`, or move it out. An `[EntityId]` takes no type parameters and sits in no generic type, since its prefix names one type, which every construction would claim. The generated code names the types around the value object through their type parameters, from inside it, and from inside the converters it nests in it, so a nested type, declared or inherited, or a type parameter of the same name between them is refused, and so is a type parameter named after a nested type of `TypeConverter`, and it writes members on the type, so neither the type nor one of its type parameters can take the name of one of them: `Value`, `Schema`, `Create`, `Known`, `Parse`, `op_Equality` and the other names [generated members](./generated-members.md) lists, `Zero` with `Arithmetic`, `Prefix` or `New` on an `[EntityId]`. Its statements name the type of every local and discard nothing, so `var` and `_` are names like any other. [Where a value object can be declared](../authoring-guide.md#where-a-value-object-can-be-declared) lists what is supported. |
| `VO0020` | `Comparison`, `ValueSet` or `Granularity` holds a value its enum does not define, such as `(StringComparison)42`. | Use one of the enum's named members. Nothing is generated for the type until then, rather than a default nobody chose. |
| `VO0021` | The `Pattern` option of `[ValueObject<T>]`, an error the compiler reports. | Implement `IValueObjectPatternValidator` with `[GeneratedRegex("X", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)] public static partial Regex Pattern { get; }` and remove `Pattern = "X"`. Nothing reads the option, and any minor version may remove it before 1.0.0. [Moving off `Pattern`](#moving-off-pattern) shows the change. |
| `VO0023` | `IValueObjectPatternValidator` on a value object whose underlying type is not `string`. | A pattern only applies to text, so it would never run. Remove the interface, and validate a number or a date in `IValueObjectValidator<T>`. |
| `VO0024` | `IValueObjectPatternValidator` on an `[EntityId]`. | `[EntityId]` validates its format itself and publishes its own OpenAPI pattern. Remove the interface, or drop `[EntityId]` and declare an ordinary value object. Nothing is generated for the type until then. |
| `VO0025` | The `[GeneratedRegex]` behind `Pattern` sets `IgnoreCase`, `Multiline`, `Singleline` or `IgnorePatternWhitespace` (warning). | The OpenAPI `pattern` is the text of the regular expression, which carries no option, so clients would check values differently. Write the rule into the pattern itself: `[A-Za-z]` rather than `IgnoreCase`. |
| `VO0026` | The `[GeneratedRegex]` behind `Pattern` sets no `matchTimeoutMilliseconds` (warning). | Set one, such as `matchTimeoutMilliseconds: 1000`. Without it, a pathological input holds a request thread for as long as the match runs. |
| `VO0027` | `[KnownValue]` on a member of an `[EntityId]`. | An identifier is minted, not chosen from a set, and `[EntityId]` generates no known values: the attribute would be read by no one. Remove it: the member stays a well-known identifier of the type, `public static readonly AccountId System = Parse("acc_…", null);`. The identifier still generates. |
| `VO0028` | The `Minimum` or `Maximum` option of `[ValueObject<T>]`, an error the compiler reports. | Implement `IValueObjectMinimum<T>` with `public static T Minimum => …;`, or `IValueObjectMaximum<T>` with `Maximum`, and remove `Minimum = "…"`. The bound is a value of the underlying type, which the compiler checks. Nothing reads the options, and any minor version may remove them before 1.0.0. [Moving off `Minimum` and `Maximum`](#moving-off-minimum-and-maximum) shows the change. |
| `VO0030` | `IValueObjectMinimum<T>` or `IValueObjectMaximum<T>` that cannot bound the type. | A `string`, a `Guid`, a `bool` and an `[EntityId]` take no bound, and a hook over another type than the underlying one, `IValueObjectMinimum<int>` on a `[ValueObject<long>]`, would never be checked. Constrain a string with `MinLength`, `MaxLength` or `IValueObjectPatternValidator`; check a `Guid` or a `bool` in `IValueObjectValidator<T>`, or remove the hook; an identifier's format is fixed, and anything more goes in `IValueObjectValidator<T>`. Otherwise implement the hook over the underlying type itself. The type still generates, without the bound. |
| `VO0031` | The example or a known value declared on the type is one its own rules refuse. | Fix the value, or the rule. The example is the OpenAPI example, which generated clients, mock servers and readers take at its word, and a refused known value throws from the type initializer, which the registration of the assembly runs as it loads: the application would not start. The message names the value, then the code and the message of the rule it breaks, as the type would answer at run time. The generator checks a value the compiler evaluates to a constant, `Known("FR")` or an example whose getter returns `Create(42)`, against what it can evaluate on its own: `MinLength`, `MaxLength`, and an empty string without `AllowEmpty`; a bound returned as a constant, `public static int Maximum => 100;` written as a getter, an explicit implementation or a constant field alike; and, for the example, a closed value set, compared under the type's `Comparison`, except under the current culture, which is the application's. What runs only at run time is left to the [contract kit](../how-to/test-value-objects.md#what-it-checks), which checks the example and every known value: a value the compiler does not evaluate, such as `new DateOnly(2024, 1, 31)`, a pattern, a validator, a bound computed by its hook or initialized as a property, a type with a normalization hook, which may turn a refused value into an accepted one, and an `[EntityId]`. |
| `VO0032` | A value object or entity identifier created uninitialized, `default(T)` or `new T()`, in code another source generator wrote: Riok.Mapperly, the configuration binding generator, or a tool you list. | It is reported in the generated file, which cannot be edited, so the fix is on your side: give Mapperly a method that calls `Create`, keep the underlying type in an options class the binding generator binds, or `AllowDefault = true` when the zero state is meaningful. [A value object another generator creates](#a-value-object-another-generator-creates) shows each fix, and how to add a generator to the list through a `.globalconfig`. |
| `VO0033` | A value object or entity identifier whose own declaration lists no interface bringing `IParsable<TSelf>`, in a project where the Request Delegate Generator runs (`PublishAot`, `PublishTrimmed` or `EnableRequestDelegateGenerator`) and that references ASP.NET Core's endpoint routing. | The RDG cannot see the `IParsable<T>` the generator adds, so a minimal API would bind the value object from the request body: a route value gets a 400, and a query value is bound to `null`. List the contract on the declaration, `public readonly partial struct Sku : IValueObject<Sku, string>;`, which the code fix does: `INumericValueObject<TSelf, TValue>` for an arithmetic value object, `IEntityId<TSelf>` for an identifier. Or declare the value object in another project. [The Request Delegate Generator](../how-to/aspnet-core.md#the-request-delegate-generator) has the details. |
| `VO0034` | `[KnownValue("France", "FR")]` on the type, the form that took a name and a value written as text, an error the compiler reports. | Declare each known value as a member of the type, `[KnownValue] public static readonly CountryCode France = Known("FR");`, keeping `Description` on the attribute. The code fix does it, writing the value as an expression of the underlying type, `new Guid("…")`, `19.99m` or `new DateOnly(2024, 1, 31)`, and fixes every one in a document, a project or the solution at once. Nothing reads the form, and any minor version may remove it before 1.0.0. [Moving off known values written as text](#moving-off-known-values-written-as-text) shows the change. |
| `VO0035` | The `Example` option of `[ValueObject<T>]` or `[EntityId]`, an error the compiler reports. | Implement `IValueObjectExample<TSelf>` with `public static Percentage Example => Create(42);`, or return a known value. Nothing reads the option, and any minor version may remove it before 1.0.0. Without the hook, a value object publishes no example, and an identifier one of the right shape, derived from its prefix. |
| `VO0036` | A member marked `[KnownValue]` that cannot be a known value. The message names the rule it breaks: it is not static; it can be written, a field that is not `readonly` or a property with a setter; it is of another type than the value object, `CountryCode?` included; or it is not initialized through `Known(...)` with the value as its one argument, which leaves out a property with a body. | Declare it as `public static readonly CountryCode France = Known("FR");`, or as `public static CountryCode France { get; } = Known("FR");`, of any accessibility. The type still generates, from its other known values. |
| `VO0037` | `Known` called anywhere but in the initializer of a member marked `[KnownValue]`. | `Known` applies every rule of the type but membership, which a known value satisfies by declaration: anywhere else it would create a value a closed set refuses. Mark the member `[KnownValue]`, or create the value through `Create` or `TryCreate`. |
| `VO0038` | `IValueObjectExample<T>` over another type than the value object itself. | The example would be published nowhere: implement `IValueObjectExample<TSelf>`, `Percentage : IValueObjectExample<Percentage>`. The type still generates, without an example. |
| `VO0039` | `[ParsesAs]`, of either form, on a member, a field or a parameter whose declared type is neither `string` nor a collection of `string`, `object` included. | The attribute holds text to a value object's rules and would throw `InvalidOperationException` when a value is validated: under System.Text.Json an `object` member holds a `JsonElement`, never text. Declare the member `string?` or a collection of `string`, or declare it as the value object itself and drop the attribute. A type parameter is left alone. [DataAnnotations](../how-to/data-annotations.md#found-at-build-time) has the details. |
| `VO0040` | `[ParsesAs(typeof(T))]` naming a type that is no value object: text, a class, a struct that does not implement `IValueObject<TSelf, TValue>` over itself, a generic definition such as `typeof(Reference<>)`, or `null`. | The attribute would throw `InvalidOperationException` when a value is validated. Name the value object, constructed when it is generic, `typeof(Reference<PurchaseOrder>)`; its nullable type is accepted. |
| `VO0041` | `[NotDefault]` on a member, a field or a parameter that is neither a value object nor its nullable type: a number, text, an interface, or a collection of value objects, whose elements it does not check. | The attribute would throw `InvalidOperationException` when a value is validated. Declare the member as the value object or its nullable type, or drop the attribute; check the elements of a collection with FluentValidation's `RuleForEach`. A type parameter is left alone. |
| `VO0042` | Under `PublishAot`, `[ParsesAs(typeof(T))]` naming a construction of a generic value object, a value object nested in a generic type, or a value object written by hand (warning). | A native binary finds them in the registry only once the application registers them, and the attribute throws `InvalidOperationException` until then: write `[ParsesAs<T, TValue>]`, which reads the type's static members and needs no registry, or register the type before a value is validated. A generated value object and an entity identifier are registered by their assembly. The package's build props make `PublishAot` visible to the analyzer. |
| `VO0043` | A serializer's contract attribute on a value object or an entity identifier the generator writes: Orleans' `[GenerateSerializer]`, protobuf-net's `[ProtoContract]` naming no serializer of its own, or another value object's, `[DataContract]`, `[XmlType]`, `[MessagePackObject]`, Newtonsoft.Json's `[JsonObject]`. | The serializer describes the type by the generator's members: it writes the value object with no member and reads it back as a default instance, or refuses it. The code fix names protobuf-net's serializer, strict at `Level200`, `[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Sku, string>))]`, or removes the attribute. [A serializer's contract attribute](#a-serializers-contract-attribute) gives each remedy, and the profile to name on a model at another level or for a trusted registration. |
| `VO0044` | MemoryPack's `[MemoryPackable]`, or `[Serializable]` on a value object that does not implement `IXmlSerializable` (warning). | The serializer reads the value object back without its rules. Remove `[MemoryPackable]`, which the code fix does. For `[Serializable]`, opt the assembly into XML serialization: removing it, which the code fix also offers, is right only where no `DataContractSerializer` writes the type, which would then write an empty element and read back a default instance. |

`VO0011` deserves its warning more than most. The code it reports compiles and looks right, and in a project
without `TreatWarningsAsErrors` it ships with the rule silently absent. It also recognizes the names hooks had
before they became interfaces — `NormalizeCore`, `ValidateCore`, `TryFormatCore`, `FormatCore` — which should be
renamed to `NormalizeValue`, `ValidateValue`, `TryFormatValue` and `FormatValue`.

## Moving off `Pattern`

The `Pattern` option of `[ValueObject<T>]` built its `Regex` at run time with `RegexOptions.Compiled`, which native
AOT cannot honour, so there the expression was interpreted. Nothing reads it any more: the compiler reports each use
as `VO0021`, an error, and any minor version may remove the option before 1.0.0.

A value object declared with the option:

```csharp skip
[ValueObject<string>(MaxLength = 5, Pattern = "^[0-9]{5}$")]
public readonly partial struct PostalCode;
```

moves the expression to `IValueObjectPatternValidator`, as a `[GeneratedRegex]` property the regex source
generator compiles:

```csharp
using System.Text.RegularExpressions;

[ValueObject<string>(MaxLength = 5)]
public readonly partial struct PostalCode : IValueObjectPatternValidator
{
    [GeneratedRegex("^[0-9]{5}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}
```

Those are the options and the timeout the option used, so the behaviour is unchanged: the pattern runs after
`MinLength` and `MaxLength`, before the known values and `ValidateValue`, and a value it does not match is
rejected as `value_object.invalid_format` with the message "The value does not match the expected format." Its
text, read off the attribute when the type compiles, is still the OpenAPI `pattern`. Keep the regular expression
as it was, and add the `using`: `System.Text.RegularExpressions` is not among the implicit usings.

## Moving off `Minimum` and `Maximum`

The `Minimum` and `Maximum` options of `[ValueObject<T>]` held a bound as text, read under a grammar of its own for
each underlying type — no white space, no exponent on a `decimal`, an offset on a `DateTimeOffset` but never on a
`DateTime` — which the compiler knew nothing of, so a mistake surfaced as a diagnostic of the generator rather than
as a type error, and a bound computed from anything was out of reach. Nothing reads them any more: the compiler
reports each use as `VO0028`, an error, and any minor version may remove the options before 1.0.0.

A value object declared with the options:

```csharp skip
[ValueObject<DateOnly>(Minimum = "1900-01-01", Maximum = "2100-12-31")]
public readonly partial struct BirthDate;
```

declares each bound as a static property of the underlying type instead:

```csharp
[ValueObject<DateOnly>]
public readonly partial struct BirthDate : IValueObjectMinimum<DateOnly>, IValueObjectMaximum<DateOnly>
{
    public static DateOnly Minimum => new(1900, 1, 1);

    public static DateOnly Maximum => new(2100, 12, 31);
}
```

The rule is unchanged: the bounds are inclusive, checked after normalization and before the known values and
`ValidateValue`, and a value outside them is rejected as `value_object.out_of_range` with the message "The value
must be greater than or equal to 1900-01-01." They are still the OpenAPI `minimum` and `maximum`, or for a type
JSON writes as a string, the `x-minimum` and `x-maximum` extensions and a sentence of the description. The message
quotes the bound in one invariant form per type, which is the option's text for an integer, a `decimal`, a `char`,
a `DateOnly` or a `TimeSpan`, but the round-trip form for a real (`1E-05` for `"1e-5"`), a time (`06:00:00.0000000`
for `"06:00"`) or a date and time, a `DateTime` without its kind: a client matching the message of such a type sees
it change.

A bound is a constant, written as an expression-bodied property as above. The check reads it each time it runs, and
the schema once, as the assembly loads, so a bound relative to the clock, such as "not in the future", belongs in
`IValueObjectValidator<T>`, and a bound must not throw. An initialized property, `{ get; } = ...`, is assigned with
the type's other static fields, in declaration order, so an instance a static field declared before it creates
would be checked against the default of the type.

## Moving off known values written as text

A known value was declared on the type, its name and its value given to `[KnownValue]` as constants, and the value of a
`Guid`, a `decimal` or a date written as text the generator parsed. Nothing reads that form any more: the compiler
reports each use as `VO0034`, an error, and any minor version may remove it before 1.0.0.

A value object declared with it:

```csharp skip
[ValueObject<decimal>(ValueSet = ValueSetKind.Closed)]
[KnownValue("Standard", "0.20", Description = "The standard rate.")]
[KnownValue("Reduced", "0.055")]
public readonly partial struct VatRate;
```

declares each known value as a member of the type instead, initialized through the generated `Known`:

```csharp
[ValueObject<decimal>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct VatRate
{
    [KnownValue(Description = "The standard rate.")]
    public static readonly VatRate Standard = Known(0.20m);

    [KnownValue]
    public static readonly VatRate Reduced = Known(0.055m);
}
```

The code fix of `VO0034` writes that change, the value as an expression of the underlying type, and fixes every
attribute of a document, a project or the solution at once. The known values keep their names, their order, their
descriptions and their place in the schema. They were properties, and are now the fields the fix writes: code that
reads `VatRate.Standard` compiles as before, and a reflection over the type's properties no longer finds them.

## An example as an instance of the type

The `Example` option of `[ValueObject<T>]` and `[EntityId]` held the OpenAPI example as text the type parsed. Nothing
reads it any more: the compiler reports each use as `VO0035`, an error, and any minor version may remove it before
1.0.0. Declare the example through `IValueObjectExample<TSelf>`, which the type's own rules create:

```csharp
[ValueObject<int>]
public readonly partial struct Percentage : IValueObjectMaximum<int>, IValueObjectExample<Percentage>
{
    public static int Maximum => 100;

    public static Percentage Example => Create(42);
}
```

The schema publishes the underlying value of the instance, as the JSON converter writes it. Without the hook, a value
object publishes no example, and an `[EntityId]` one of the right shape, derived from its prefix.

## Retired diagnostics

These reported attribute options written as text, which no longer compile. Their identifiers are not reused.

| Id | Reported |
| --- | --- |
| `VO0004` | A bound of the `Minimum` or `Maximum` option not written in the form of its type. |
| `VO0006` | A known value written on the type whose name was unusable; the compiler checks a member's name now. |
| `VO0013` | A known value written on the type not written in the form of its type. |
| `VO0014` | An invalid regular expression in the `Pattern` option. |
| `VO0022` | The `Pattern` option beside `IValueObjectPatternValidator`. |
| `VO0029` | The `Minimum` or `Maximum` option beside its hook. |

## A value object another generator creates

Source generators never see each other's output. A generator that builds objects finds a value object without the
`Create`, `Parse` and conversions this generator adds to it, and writes `new T()` in their place, which skips every
rule the type declares. `VO0010` leaves generated code alone, since this generator's own `TryCreate` assigns
`default` on its rejection path, so `VO0032` reports the `new` or the `default` there, in the code of two
generators:

| Generator | What it writes |
| --- | --- |
| Riok.Mapperly | `var target = new global::CountryCode();` for a mapping from a `string` to a value object declared in the mapper's own project. The value of the source is dropped, and every value object of the target is a default instance. |
| The configuration binding generator, which `PublishAot` turns on | `var temp7 = new global::Quantity();` for a property of an options class. The options bind to default instances, which `ValidateOnStart` lets through unless the member carries `[NotDefault]`. |

It is reported in the generated file, which cannot be edited: the fix is on your side.

**Mapperly.** Give it a method for each direction, which it picks by source and target type. This works on
Mapperly 4.3.1 and 5.0.0-next.11, whether the value objects are declared in the mapper's project or in a project it
references, and [Object mappers](../how-to/mapping.md) covers Mapster and AutoMapper too:

```csharp skip
public static class ValueObjectMappings
{
    public static Iban ToIban(string value) => Iban.Create(value);
    public static string FromIban(Iban value) => value.Value;
    public static OrderId ToOrderId(Guid value) => OrderId.Create(value);
    public static Guid FromOrderId(OrderId value) => value.Value;
}

[Mapper]
[UseStaticMapper(typeof(ValueObjectMappings))]
public partial class OrderMapper
{
    public partial Order ToDomain(OrderDto dto);
    public partial OrderDto ToDto(Order order);
}
```

Write the methods out of a value object too. Without `FromOrderId`, Mapperly can map an `OrderId` to a `Guid` as
`new Guid()`, which is `Guid.Empty`, silently: `VO0032` cannot see it, since a `Guid` is not a value object.

**The configuration binding generator.** Keep the underlying type in the options class, hold it to the value object's
rules with [`[ParsesAs(typeof(T))]`](../how-to/data-annotations.md), which `ValidateDataAnnotations().ValidateOnStart()`
and an `[OptionsValidator]` run, and create the value object where the options are read; or bind the value object in an
`IConfigureOptions<T>` of your own that calls `Parse`.
Without the generator, the binder that works by reflection goes through the generated `TypeConverter`, and reports
a value its type rejects.

**Another generator.** The list holds the names these two write as the first argument of `[GeneratedCode]`,
`Riok.Mapperly` and `Microsoft.Extensions.Configuration.Binder.SourceGeneration`. Add others, comma-separated, under
`adcodicem_value_objects.generated_code_tools` in a `.globalconfig` file, which the SDK reads from the project's
directory or from any directory above it:

```ini
is_global = true
adcodicem_value_objects.generated_code_tools = My.Generator, Other.Generator
```

The names add to the list, never replace it, and each must be the tool's name in full and in the same case. The
`[GeneratedCode]` that counts is the nearest one: on the member holding the `new` or the `default`, or else on the
type containing it, and so on outwards. The key is not read from an `.editorconfig`: a `[*.cs]` section reaches a
generated file only when the project's intermediate output directory lies beneath that `.editorconfig`, which the
artifacts output layout, for one, does not. A team that wants none of this suppresses `VO0032` as a whole:
`dotnet_diagnostic.VO0032.severity = none` in that `.globalconfig`, or `<NoWarn>$(NoWarn);VO0032</NoWarn>` in the
project. In an `.editorconfig` section, the same line misses the generated files, for the same reason.

The System.Text.Json generator is not in the list, and should not be added to it. It writes
`ObjectCreator = () => new global::Sku()` into the metadata of a context for every type it is given, and a context
wired as [the JSON guide](../how-to/json.md#systemtextjson-source-generated) shows reads a value object through its
converter, never through that line.

## A serializer's contract attribute

People put the serializer's contract attribute on every type they send. On a value object, it makes the serializer
describe the type by its members, and those are the generator's: `Value`, the private field holding it and the
explicit `IsDefault`, which the author has nothing to put `[Id]`, `[ProtoMember]`, `[Key]` or `[DataMember]` on. The
serializers' own analyzers report none of it, so `VO0043` and `VO0044` do, on the attribute, on whichever partial
declaration carries it:

| Attribute | Id | What the serializer does | Remedy |
| --- | --- | --- | --- |
| Orleans' `[GenerateSerializer]` | `VO0043` | Writes the value object with no field and reads it back as a default instance: a holder of two value objects is written `2020E021E0E0`. `DeepCopier` keeps the value, so a call Orleans copies rather than serializes does not show the loss. | Remove it, and give the value object a surrogate ([Orleans](#orleans)). |
| protobuf-net's `[ProtoContract]`, naming no serializer | `VO0043` | Writes an empty message, `0A00`, read back as a default instance; under protobuf-net's compile-time model, whatever the registration. | Name the serializer, which the code fix does ([protobuf-net](#protobuf-net)). |
| `[DataContract]` | `VO0043` | protobuf-net writes `0A00` and MessagePack's standard resolver `[]`, each read back as a default instance; in an assembly opted into XML serialization, `DataContractSerializer` refuses the type: "Type '…' cannot be IXmlSerializable and have DataContractAttribute attribute." | Remove it: [`[assembly: ValueObjectXmlSerialization]`](../how-to/xml.md) writes the value object as its value. |
| `[XmlType]` | `VO0043` | protobuf-net writes `0A00`, read back as a default instance; in an assembly opted into XML serialization, `XmlSerializer` refuses the type: "Only XmlRoot attribute may be specified for the type …". | Remove it: the schema provider names the type, and `[XmlRoot]` names the element of a root value object. |
| MessagePack's `[MessagePackObject]` | `VO0043` | MessagePack's analyzer refuses the generated members, `MsgPack004` in the generated file; keyed by name, it compiles and writes `{}`, read back as a default instance. | Remove it, and serialize with [`WithValueObjects()`](../how-to/messagepack.md). |
| Newtonsoft.Json's `[JsonObject]` | `VO0043` | Writes `{"Value":"ab"}`, read back as a default instance, unless `ValueObjectConverter` is in the settings. | Remove it: Newtonsoft.Json then writes the value, as text through the generated type converter, or as its underlying type with [`ValueObjectConverter`](../how-to/json.md#newtonsoftjson). |
| MemoryPack's `[MemoryPackable]` | `VO0044` | Over text, MemoryPack's generated formatter does not compile (`CS8377`); over any other type, MemoryPack copies the memory of the struct, attribute or not, and reads it back without the rules. | Remove it ([MemoryPack](#memorypack)). |
| `[Serializable]`, on a value object that does not implement `IXmlSerializable` | `VO0044` | `DataContractSerializer` writes the private field, an element named `_value`, and reads it back without the rules: `abcdef` comes back into a value object whose maximum length is 3. | Opt the assembly into [XML serialization](../how-to/xml.md). Remove it only where no `DataContractSerializer` writes the type ([`[Serializable]`](#serializable)). |

The figures were measured on Orleans 10.4.0, protobuf-net 3.4.30, MessagePack 3.1.12, MemoryPack 1.21.4 and
Newtonsoft.Json 13.0.4. Each attribute is matched by its full name, so one of the same name in another namespace, or
nested in another type, is left alone, and so are a type holding value objects, a value object written by hand, which owns its members, and one of a
referenced assembly, which is reported in its own project. `[XmlRoot]`, Orleans' `[Immutable]` and `[Alias]`, MessagePack's
`[MessagePackFormatter]` and Newtonsoft.Json's `[JsonConverter]` describe nothing by the members, and are left alone too.
System.Text.Json needs nothing: `[JsonSerializable]` on a struct is `CS0592` already, and it ignores `[DataContract]` and
`[JsonObject]`.

Removing a `VO0043` attribute never makes things worse. Where nothing registers the value object, protobuf-net,
MessagePack and Orleans then fail loudly, "No serializer defined for type", `FormatterNotRegisteredException`, "Could not
find a codec for type", where they lost the value silently. `VO0043` is an error even where a build works today: a bare
`[ProtoContract]` that `AddValueObjects` takes over on a runtime model before its first use, or a `[DataContract]` value
object only ever written through `WithValueObjects()`. Such a build loses the value as soon as another route meets the
type, protobuf-net's `[ProtoModel]` or MessagePack's standard resolver. The code fix repairs it, by removing the
attribute or by naming protobuf-net's serializer strict at `Level200`. That serializer writes what `AddValueObjects`
wrote only where the model is at `Level200` and the registration strict: elsewhere, name the profile the registration
used ([protobuf-net](#protobuf-net)). Removing `[Serializable]`, which `VO0044` reports, can lose the value
([`[Serializable]`](#serializable)).

### protobuf-net

The code fix "Name the value object's protobuf-net serializer, strict at Level200" writes the attribute route of
`AdCodicem.ValueObjects.ProtobufNet`, which protobuf-net's compile-time model, and so
[native AOT](../how-to/protobuf-net.md#native-aot), takes:

```csharp skip
[ValueObject<string>(MaxLength = 8)]
[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<Sku, string>))]
public readonly partial struct Sku;
```

It keeps the attribute's other arguments, `Name` included, and imports `AdCodicem.ValueObjects.ProtobufNet`. The serializer
it names is the strict profile at protobuf-net's default level, `Level200`, as its title says. For `Level240` or
`Level300`, or for bytes only the application writes, name a profile of `ValueObjectProtoProfiles` instead:

```csharp skip
[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<CustomerId, Guid, ValueObjectProtoProfiles.StrictLevel300>))]
public readonly partial struct CustomerId;
```

That is the case of a bare `[ProtoContract]` that `AddValueObjects` took over on a model of the application's own at
`Level240` or `Level300`, or with `trusted: true`: the registration wrote the value object through the profile of the
model's level and of that trust, and the attribute must name the same one. The two-argument serializer the code fix
writes would move a value object over `DateTime` or `TimeSpan`, and at `Level300` over `Guid` or `decimal` too, to the
`bcl` messages, which neither the bytes already stored nor the peers' `.proto` match, and would make a trusted read
strict. Name the profile the registration used, `StrictLevel240`, `StrictLevel300` or the `Trusted…` one of the level, or
remove the attribute and keep the registration. Once the attribute names the two-argument serializer, an
`AddValueObject<TSelf, TValue>()` of the same value object throws at start, naming both serializers: drop that call.

The message gives the remedy the compilation allows, and the code fix offers the removal alone where it cannot name the
serializer:

- **Without the package**, it says to reference `AdCodicem.ValueObjects.ProtobufNet` and name the serializer.
- **Over `Int128`, `UInt128` or `DateTimeOffset`**, which protobuf-net has no serializer for, generic or not: remove the
  attribute. `AddValueObject<TSelf, TValue>()` refuses such a value object too.
- **A generic value object** over any other type, or one nested in a generic type: an attribute cannot name a serializer
  through a type parameter. Remove it, and register each construction with `AddValueObject<TSelf, TValue>()`.
- **A serializer copied from another value object**, `ValueObjectProtoSerializer<Other, string>` or the unbound
  `ValueObjectProtoSerializer<,>`, which protobuf-net refuses at run time, "No serializer for type … is available for
  model", is reported too: the code fix closes it over the value object, and keeps a profile it names.

A `Surrogate`, a serializer of the application's own, and the package's serializer closed over the value object itself are
the author's, and left alone, the last over `Int128` too, where protobuf-net's refusal is loud. So is `IsScalar`, which
[Native AOT](../how-to/protobuf-net.md#native-aot) says to leave out.

### Orleans

No Orleans integration exists yet. Without `[GenerateSerializer]`, Orleans fails loudly, "Could not find a codec for type
…". A surrogate and a converter under `[RegisterConverter]` keep the value, and refuse one the type rejects with the
`ValueObjectException` of `Create`, its code under `ValueObjectErrors.ErrorCodeKey`. This recipe ran on Orleans 10.4.0,
through its serializer and its copier, not in a call between silos, and stands until an integration replaces it:

```csharp skip
[GenerateSerializer]
public struct SkuSurrogate
{
    [Id(0)]
    public string? Value;
}

[RegisterConverter]
public sealed class SkuSurrogateConverter : IConverter<Sku, SkuSurrogate>
{
    public Sku ConvertFromSurrogate(in SkuSurrogate surrogate) => Sku.Create(surrogate.Value!);

    public SkuSurrogate ConvertToSurrogate(in Sku value) => new() { Value = value.Value };
}
```

### MemoryPack

No integration of this library serializes with MemoryPack. Over every underlying type but text, MemoryPack copies the
memory of a value object, `[MemoryPackable]` or not, and assigns it back without the rules: forged bytes read back as
`1000` into a value object whose maximum is 100. Over text, its generated formatter does not compile (`CS8377`).
Serialize value objects with a serializer that reads them through their rules, or hand MemoryPack only bytes the
application itself wrote. `VO0044` can only report the attribute, the sign that a value object is meant for MemoryPack.

### `[Serializable]`

Outside the XML opt-in, `DataContractSerializer` writes the private field of a `[Serializable]` value object and reads it
back without the rules. Under `[assembly: ValueObjectXmlSerialization]`, the `IXmlSerializable` the generator implements
wins and the attribute changes nothing, so it is left alone there, as on a value object implementing `IXmlSerializable`
itself.

Where a `DataContractSerializer` writes the value object, through WCF, CoreWCF or Dapr actor remoting, opt the assembly
in: that is the remedy. Removing the attribute, which the code fix offers under the title "Remove [Serializable]:
DataContractSerializer then writes an empty element", loses the value instead, since `DataContractSerializer` then writes
an empty element and reads back a default instance ([Default instances](default-instances.md)). Remove it only where no
`DataContractSerializer` writes the type. A codebase that puts `[Serializable]` on every type by habit, under
`TreatWarningsAsErrors`, and runs no `DataContractSerializer`, may instead silence the warning with
`dotnet_diagnostic.VO0044.severity = none` in a `.globalconfig`.

### Silencing one on purpose

Both come from an analyzer, so `#pragma` silences them, around the attribute:

```csharp skip
[ValueObject<string>(MaxLength = 5)]
#pragma warning disable VO0043 // The trap this test exercises on purpose: protobuf-net writes an empty message.
[ProtoContract]
#pragma warning restore VO0043
public readonly partial struct BareCode;
```

## Suppressing `VO0010` in a test

A test that must build an uninitialized instance disables the diagnostic on the spot, with the reason:

```csharp skip
#pragma warning disable VO0010 // The guard under test must reject an uninitialized instance.
var missing = default(Iban);
#pragma warning restore VO0010
```
