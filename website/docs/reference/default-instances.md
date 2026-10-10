---
title: Where a Default Instance Can Come From at Run Time
sidebar_label: Default instances at run time
slug: /reference/default-instances
description: The libraries and framework paths that hand an application a value object no rule has checked — a default instance, or an initialized one breaking its own rules — and the line of configuration that closes each.
---

# Where a default instance can come from at run time

A non-default instance is normalized and valid by construction, and `VO0010` keeps the default out of source: it
refuses `default(T)` and `new T()` where they are written, and `VO0032` does the same in the code Riok.Mapperly and the
configuration binding generator write. Neither can see what other code does at run time. A reflection-based
serializer that finds no settable member, a binder that falls back to the type's default, a source generator that
cannot see the generated members: each can build a value object without going through `Create`. Some hand the
application a default instance, `IsDefault` true. Others hand it an instance that looks initialized and breaks its own
rules.

Each source below was observed in a probe against packages built from this repository's sources, with the version
tested, unless its row says otherwise. Several are another library's default, stated as it stands, and a later version
of that library may change it. What this library's own writers do
with such an instance is in [A value refused on write](./errors.md#a-value-refused-on-write): they refuse to write a
value its type rejects.

## Sources of a default instance

| Source | What reaches the application | Fix |
| --- | --- | --- |
| An array element, or a field or a property never assigned | `default(T)`. `VO0010` sees only a `default` or a `new` written in source. | Express absence as `T?`, and guard at the edge. |
| A JSON property missing from the payload, under System.Text.Json | `default(T)`, which no rule checks: `{"OrderId":"3f2504e0-…"}` read into a record of three value objects leaves the other two default. | `RespectRequiredConstructorParameters = true` for a positional record, which turns the missing members into a `JsonException`; `[JsonRequired]` or C# `required` for a settable property. [JSON](../how-to/json.md#a-member-missing-from-the-payload) has the recommendation. |
| Riok.Mapperly 4.3.1, mapper in the value objects' own project | `var target = new global::Iban(); return target;` for every mapping into a value object. | `VO0032` fails the build. Give Mapperly [static mapping methods](../how-to/mapping.md#mapperly) that call `Create`. |
| Mapperly, value objects in a referenced project | Mappings into a value object validate, through `Create` or `Parse`, but `OrderId` to `Guid` maps to `new Guid()`, `Guid.Empty`. | Static mapping methods out of the value objects too, returning `.Value`. |
| The configuration binding generator, which `PublishAot` turns on | `new global::Domain.Iban()`: every value-object property of the options is default, even with valid input. | `VO0032` fails the build. Keep the underlying types in the options class. |
| The reflection-based configuration binder, a missing key | A default instance, silently: `Iban=''`. | Declare the property `T?` and check it, or validate the options, with FluentValidation's [`NotDefault`](../how-to/fluentvalidation.md#an-uninitialized-value-object) for one. |
| Bogus `Faker<T>` with no rule for a property | Every value-object property left without a rule is default. | `StrictMode(true)`, which refuses a `Faker<T>` that leaves a property without a rule, and `RuleForValueObjects()` from [`AdCodicem.ValueObjects.Bogus`](../how-to/test-value-objects.md#bogus), which gives each a value its rules accept. |
| MongoDB.Driver 3.12, no serializer registered | Written as `{}`, read back as default, and `x.Iban == iban` matches every document. | [`AdCodicem.ValueObjects.MongoDB`](../how-to/mongodb.md): a value object is stored as its underlying value, and a default its type rejects is refused on write. |
| MongoDB.Driver, a field missing from a stored document | The class map leaves the member as it was: a default instance, which no rule checks. | `[BsonRequired]` on the member, which makes the driver refuse the document: [MongoDB](../how-to/mongodb.md#reading). The [validator built from the rules](../how-to/mongodb.md#a-collection-validator-from-the-rules) lists the member as `required`, so the server refuses to store such a document in the first place. |
| MongoDB.EntityFrameworkCore 10.0.4 without `ConfigureValueObjects` | Written as `{}`, read back as default. | Call [`ConfigureValueObjects`](../how-to/ef-core.md#other-providers). It is mandatory with this provider, while a relational provider refuses to build a model without it. |
| Microsoft.Extensions.AI 10.10, a tool argument sent as JSON `null`, which the OpenAI adapter hands over as a C# `null` | `AIFunctionFactory`'s binding passes the `null` on to a value-object parameter that cannot be `null`, which then holds a default instance, without calling the converter. | [`WithValueObjectValidation()`](../how-to/language-models.md#a-refused-argument) from `AdCodicem.ValueObjects.AI`, which answers it `value_object.required`, as a result the model reads. |
| SignalR, MessagePack hub protocol | `{}` on the wire: the hub receives `Sku='' Quantity=0`. | [`UseValueObjects()`](../how-to/messagepack.md#signalr) in `AddMessagePackProtocol`, on the server and on the .NET client: the values travel bare and are read through their rules. |
| MessagePack, `ContractlessStandardResolver` | `{"Sku":{}}`, read back as default. | [`WithValueObjects()`](../how-to/messagepack.md) on the options: a value object is written as its underlying value, and read through its rules. |
| `XmlSerializer`, and the MVC XML formatters built on it, in an assembly that does not opt in | An empty element on write; a default on read, even from an element with content. | `[assembly: ValueObjectXmlSerialization]`: the value objects implement `IXmlSerializable`, [XML](../how-to/xml.md). |
| `DataContractSerializer`, CoreWCF, Dapr actor remoting, in an assembly that does not opt in | As `XmlSerializer`. | As `XmlSerializer`; for Dapr actors, also the JSON serialization Dapr documents for actor remoting (not run here): [Dapr](../how-to/messaging.md#dapr). |
| `XmlSerializer` or `DataContractSerializer`, an element missing from the document | Nothing reads the missing element: the member keeps the default instance its container left. | `[DataMember(IsRequired = true)]` with `DataContractSerializer`; with `XmlSerializer`, check the member after reading. [XML](../how-to/xml.md#reading). |
| CsvHelper 33.1.0 | A `Value` column per value object on write; a default on read. The probe also wrote an `IsDefault` column, a public property then, and no longer is (inferred, not re-run). | A `DefaultTypeConverter` of your own over `TryParse`. |
| YamlDotNet 18.1.0, static builder | `Iban: {}`, and one `YDNG001` warning per value object. | A typed `IYamlTypeConverter` of your own. |
| ServiceStack.Text 10.3.0 | A value the type rejects becomes a default: `{"Quantity":500}` reads `Quantity=0`. | `JsConfig.ThrowOnError = true`. |
| LiteDB | `{"Value":"FR76…"}` written, a default read. The probe also wrote `"IsDefault":false`, a public property then, and no longer is (inferred, not re-run). | `BsonMapper.RegisterType`, writing `.Value` and reading through a factory that calls `Create`. |
| Azure.Data.Tables | Reading into a class leaves every value-object property default; writing one throws. | Read into an entity of underlying types, then `TryCreate`. |
| MassTransit 8.5.11, message initializer fed raw values | The initializer drops what it cannot convert, and the message holds defaults. Under Newtonsoft.Json without the converter, it travels as `{}` and is consumed as defaults. Under System.Text.Json it travelled as `{"orderId":"00000000-…","customer":"","quantity":0}`; the converter refuses to write a default its type rejects, so such a message now fails to publish with a `JsonException`, and only a value object accepting its zero travels as zero (inferred from the converter, not run). | Pass value objects to the initializer, or publish concrete message types: [MassTransit](../how-to/messaging.md#masstransit). |
| Wolverine.Http 6.45.0, query string | A missing value binds the default; so does a rejected one under the default `RejectUnparseableQueryValues = false`. | `RejectUnparseableQueryValues = true`, the default from Wolverine 7, and `T?` for an optional parameter: [Wolverine](../how-to/messaging.md#wolverine). |
| The Request Delegate Generator 10.0.12, minimal API binding | Empty query text, `?quantity=`, binds the default to a parameter that cannot be `null`, or to an array element, and the handler runs: the RDG refuses no empty query text, where the reflection-based binding answers 400. | Declare the parameter `T?`, which it binds to `null`, and check it: [the Request Delegate Generator](../how-to/aspnet-core.md#the-request-delegate-generator). |
| Spectre.Console.Cli, an option left out | A non-nullable value-object option is default. | Declare the option `T?`, or make the value a required `<arg>`: [Spectre.Console.Cli](../how-to/files-and-command-line.md#spectreconsolecli). |
| Blazor `@bind` | Emptying the input calls the setter with `default(Iban)`; an invalid input is ignored, with no message. | Bind to a `string`, and parse it. |
| Quartz 4.3.0, job data | A value the type rejects leaves the property default and the job runs, under the default `PropertyMismatchBehavior.Ignore`; a `null` entry gives a default too. | `PropertyMismatchBehavior.Throw`, or `IJob<TInput>` with required members: [Quartz](../how-to/messaging.md#quartz). |
| JSON Patch `remove`, System.Text.Json and Newtonsoft.Json versions, 10.0.12 | A non-nullable value-object property is set to its default. | Declare the property `T?`, or check the operations before `ApplyTo`. |
| ASP.NET Core OData 9.5.0 | Every value object written as `{}`. | Not supported: project to classes of underlying types. |
| SQL Server dynamic data masking, an integer column | A masked value reads as `0`, a default `Quantity`. | [Strict reads](../how-to/ef-core.md#dynamic-data-masking), or no value object on a masked column. |

## Sources of an initialized but invalid instance

`IsDefault` is `false` for these, so a check for the default does not catch them:

| Source | What reaches the application | Fix |
| --- | --- | --- |
| SQL Server dynamic data masking, a string column | `DEXXXXXXXX3000`, through the default read, which trusts the column: an `Iban` its rules refuse, `IsDefault` false. | [Strict reads](../how-to/ef-core.md#validation-on-read), `ConfigureValueObjects(strict: true, …)`. |
| MassTransit.MessagePack | Each value object written as a wrapper holding `Value` and the private `_value`, and read back without validation. | Not covered: use the System.Text.Json serializer. |

## Closed by the library

These were sources too, and no longer are:

| Source | What happens now |
| --- | --- |
| AutoMapper 16, no explicit map | The mapping throws `AutoMapperMappingException`, "Missing type map configuration or unsupported mapping.", since no constructor takes the value alone. Map each type: `CreateMap<string, Iban>().ConvertUsing(s => Iban.Create(s))`. |
| Entity Framework Core, writing a default | Refused: on a property of the value object's type, `SaveChanges` throws and writes nothing; an optional property stores `NULL`. An element of a collection is refused as a property is, but in a `List<Iban?>`, over text, where it is stored as a JSON `null`. No later read finds an empty `Iban` to trust. EFCore.BulkExtensions writes through the same converter (inferred, not run). |
| Dapper, a default as a parameter | Refused with a `DataException` before the command runs, from an `Iban` and an `Iban?` alike. |
| MongoDB.Driver with `AdCodicem.ValueObjects.MongoDB`, writing a default | Refused with a `BsonSerializationException` before anything is sent, from an `Iban` and an `Iban?` alike, in a document, an update or a query constant. |
| MessagePack and SignalR with `AdCodicem.ValueObjects.MessagePack`, writing a default | Refused with a `MessagePackSerializationException`, from an `Iban`, a dictionary key and an `Iban?` holding one alike: a .NET client's invocation throws before it is sent, and a hub method's result fails to be written, which closes the connection. |
| System.Text.Json and Newtonsoft.Json with its converter, writing a default | Refused with a `JsonException` or a `JsonSerializationException`, so a service reading the payload never receives it. |

A type that accepts its zero, an `Amount` with a minimum of 0 or an unconstrained `Guid`, is still written as zero:
over a value type, nothing tells the default from a constructed zero.

## Guards

- FluentValidation's [`NotDefault`](../how-to/fluentvalidation.md#an-uninitialized-value-object) rule checks
  `IsDefault` at the edge. Over a value type, `IsDefault` is `true` for a valid zero as well, `Amount.Create(0m)`, which
  it refuses too.
- DataAnnotations `[Required]` passes on a default value object: a struct is never `null`. CommunityToolkit.Mvvm's
  `ObservableValidator` showed it.
- A [strict](../how-to/ef-core.md#validation-on-read) Entity Framework Core read refuses what the default read lets
  through.
