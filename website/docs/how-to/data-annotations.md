---
title: Use with DataAnnotations
sidebar_label: DataAnnotations
slug: /how-to/data-annotations
description: Hold raw text in a DTO, an options class or a form model to a value object's rules with [ParsesAs], refuse a value object left at its default with [NotDefault], and read the rule's code off the result, wherever DataAnnotations runs.
---

# Use with DataAnnotations

A DTO, an options class or a form model often holds the text a value object is made from later. Restating the IBAN's
lengths and pattern in `[StringLength]` and `[RegularExpression]` is the drift the library exists to prevent, and
`[Required]` lets through a value object that was never created, since a struct is never `null`. Two attributes of
`AdCodicem.ValueObjects.Abstractions`, which every application already has, close both gaps, in namespace
`AdCodicem.ValueObjects.Annotations`, beside `[ValueObject<T>]`:

- `[ParsesAs(typeof(Iban))]`, and its generic form `[ParsesAs<Iban, string>]`, hold text to the rules the value object
  declares, its normalization included;
- `[NotDefault]` refuses a value-object member holding the instance equal to `default(TSelf)`.

They run wherever DataAnnotations runs: `Validator`, MVC, a minimal API under `AddValidation()`, options validation and
CommunityToolkit.Mvvm's `ObservableValidator`. No package is added: DataAnnotations is part of .NET.

```csharp
using System.ComponentModel.DataAnnotations;

[ValueObject<string>(MinLength = 15, MaxLength = 34)]
public readonly partial struct Iban;

[ValueObject<int>]
public readonly partial struct Quantity : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    public static int Minimum => 1;

    public static int Maximum => 100;
}

public sealed class SettlementOptions
{
    [Required, ParsesAs(typeof(Iban))]
    public string? Account { get; set; }

    [ParsesAs<Quantity, int>]
    public string? MaxItems { get; set; }

    [NotDefault]
    public Iban Fallback { get; set; }
}
```

## Text that must become a value object

The attribute validates and changes nothing: the member keeps the text as it was sent, and the application creates the
value object where it reads the options or the form, from the same text.

```csharp skip
// Options, checked as the host starts: an IBAN of the wrong length stops it, with the message of the rule.
builder.Services.AddOptions<SettlementOptions>()
    .BindConfiguration("Settlement")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// The validator the options generator writes, which creates the attributes in its own code, either form.
[OptionsValidator]
public partial class SettlementOptionsValidator : IValidateOptions<SettlementOptions>;

// A minimal API under the validation of Microsoft.Extensions.Validation.
builder.Services.AddValidation();
app.MapGet("/accounts", ([ParsesAs(typeof(Iban))] string? iban) => ...);
```

```text
DataAnnotation validation failed for 'SettlementOptions' members: 'Account' with the error: 'The value must be at least 15 characters long.'.
```

This is the way out of `VO0032` under the configuration binding generator, which `PublishAot` turns on and which cannot
create a value object: keep its text in the options class, a member such as `Fallback` included, which the
reflection-based binder alone binds as a value object, hold it to the value object's rules with `[ParsesAs]`, and create
the value object where the options are read
([diagnostics](../reference/diagnostics.md#a-value-object-another-generator-creates)).

## Two forms

- `[ParsesAs(typeof(Iban))]` names the value object by its `Type` and finds its descriptor as
  [`MustParseAs`](./fluentvalidation.md) does, registered by the generated code of its assembly, or described by
  reflection where the runtime supports dynamic code, a construction of a generic value object or a value object written
  by hand among them, but the first time a value is validated rather than when the attribute is created, where
  `MustParseAs` looks it up when the rule is built: anything reading a member's attributes creates them, a document
  generator among them. The attribute keeps the descriptor, and the hosts keep their attribute instances (MVC,
  `AddValidation()`, `Validator` and the generated options validator were each seen to), so the lookup is paid once.
- `[ParsesAs<Iban, string>]` names it through its type arguments and reads its static members, `TryParse` and `Schema`,
  with no registry. It derives from the first form, so that anything looking for `[ParsesAs]` finds both.

Under native AOT, the `Type` form finds a generated value object, which its module initializer registers, and nothing
else: register a construction of a generic value object, or a value object written by hand, before a value is validated,
as [Run-time lookup](./runtime-lookup.md#value-objects-written-by-hand) shows, or use the generic form. `VO0042` reports
the case under `PublishAot`.

## What it checks

- A `null` passes, so that `[Required]` says whether the member must be there, and so does an `ImmutableArray<string>`
  left at its default, as an absent member of that type is left; `[Required]`, which refuses `null` alone, lets that one
  through.
- Empty text is the value object's to judge, as any text is: a value object over `string` refuses it with
  `value_object.required`, unless it declares `AllowEmpty = true`, and one over another type with
  `value_object.not_parsable`.
- A `string` is parsed in the invariant culture, as the binders and the configuration binder read text.
  `UseCurrentCulture = true` reads it in the current culture, for a form a person types in: `12,5` is then an amount in
  French. Create the value object in the same culture, `Amount.Parse(text, CultureInfo.CurrentCulture)`, when you read
  the member.
- An `IEnumerable<string?>`, an array, a list or an immutable array of text, has each element checked, a `null` one
  skipped, and the first refused one reported under the member's own name. MVC prefixes a member name a result gives, so
  an element's index would land under `Ibans.Ibans[1]`; per-element keys are FluentValidation's `RuleForEach`.
- A member of any other type, and a type that is no value object, throw `InvalidOperationException` when a value is
  validated, never quoting the value: the first names the member and its type, the second the type. `VO0039` and
  `VO0040` report both at build time.

## An uninitialized value object

`[Required]` passes on a value object left at its default, everywhere DataAnnotations runs, since a struct is never
`null`. `[NotDefault]` refuses it, with `value_object.required` and `[Required]`'s message, `The {0} field is required.`,
so that an answer reads the same whichever refused:

```csharp skip
public sealed class TransferCommand
{
    [NotDefault] public Iban Account { get; set; }      // default(Iban) refused
    [NotDefault] public Iban? Fallback { get; set; }    // null passes, default(Iban) refused
}
```

- A `null` passes, so that an absent `TSelf?` is `[Required]`'s to judge; a `TSelf?` holding the default instance is
  refused.
- It reads `IsDefault` through the descriptor of the value's type, closed once per type through
  [the visitor](./runtime-lookup.md), with no `MakeGenericType`. Over a value type, a constructed zero equals the
  default and is refused too: `OrderId.Create(Guid.Empty)` fails it, as it fails FluentValidation's `NotDefault`. Use it
  on a value object over a value type only when its rules refuse the zero anyway.
- A member that is no value object, a collection of value objects included, whose elements it does not check, throws
  `InvalidOperationException`; `VO0041` reports it at build time.

## Messages and codes

| Declared on the attribute | Message |
| --- | --- |
| nothing | The English message of the rule that refused the text: `The value must be at least 15 characters long.` |
| `ErrorMessage`, or `ErrorMessageResourceType` with `ErrorMessageResourceName` | DataAnnotations' own single message, `{0}` the display name of the member |
| `ErrorMessageResourceType` alone | The resource's message for the code of the rule, as below |

A refusal is a `ValueObjectValidationResult`, DataAnnotations' `ValidationResult` carrying `ErrorCode` and
`ValueObjectType` beside the message, which never carries the code, nor the refused value. Whether the code reaches you
depends on the host:

- `Validator.TryValidateObject`, `TryValidateProperty` and `TryValidateValue` list the result as it is, and so does
  `ObservableValidator.GetErrors`: read `ErrorCode` off it.
- `Validator.ValidateObject`, `ValidateProperty` and `ValidateValue` throw a `ValidationException` around it, whose code
  `ValueObjectErrors.TryGetCode` reads, from it or from an exception wrapping it. The exception's `Value` holds the
  value validated, as DataAnnotations sets it for any attribute.
- MVC's model state, the errors of `AddValidation()` and the failures of options validation keep the message alone. A
  refusal of `[ParsesAs]` reaches MVC's problem details in `errors` without an `errorCodes` entry. A client that branches
  on codes binds a member typed as the value object instead, whose refusal [the binder](./aspnet-core.md) records with
  its code.

## Messages in another language

`ErrorMessageResourceType` named alone has the message looked up by the code of the rule, in the resource an application
hands MVC's and the minimal APIs' `UseResource<T>()`:

```csharp skip
[ParsesAs(typeof(Quantity), ErrorMessageResourceType = typeof(ValidationMessages))]
public string? MaxItems { get; set; }
```

```text
MaxItems: "500"  →  value_object.out_of_range  « MaxItems » doit être compris entre 1 et 100.
```

- The keys are those of [the errors reference](../reference/errors.md#a-message-in-the-applications-language),
  `ValueObject.` followed by the code, `.minimum` or `.maximum` first for a type declaring one bound, and
  `ValueObject.value_object.required` for `[NotDefault]`. `{0}` is the display name of the member, `[Display(Name = …)]`
  included, which the host chooses otherwise: the validator `[OptionsValidator]` writes passes `SettlementOptions.Fallback`
  on .NET 10 and `Fallback` on .NET 11. The rule's arguments follow from `{1}`, formatted in the current culture; the
  message is read in the current UI culture.
- A key the resource lacks keeps the English message, and so does a culture it holds nothing for, a resource with no
  neutral culture included. A template quoting an argument its code does not supply throws `FormatException`.
- MVC under `AddDataAnnotationsLocalization()`, and the validation of .NET 10 and .NET 11, leave the message of an
  attribute naming a resource type as the attribute gives it, so the message read by code reaches the answer.
- DataAnnotations requires `ErrorMessageResourceName` beside `ErrorMessageResourceType` on its own attributes; these
  read the type alone, and `FormatErrorMessage`, which `ValidationAttribute.Validate(object, string)` calls on its own,
  answers `The field {0} is invalid.` for them, and `[Required]`'s message for `[NotDefault]`.
- The resource is read through a `ResourceManager` over the type, whose base name is the type's full name: the base name
  the localizer factory reads while `LocalizationOptions.ResourcesPath` is unset. Name the resource type after the
  folder, its namespace being the root namespace followed by the folder, as
  [the ASP.NET Core guide](./aspnet-core.md#messages-in-the-applications-language) recommends, and one
  type serves MVC, the minimal APIs and these attributes. With a `ResourcesPath`, the localizer reads
  `{RootNamespace}.{ResourcesPath}.{Type}` while the attribute reads the type's full name: give the attributes a type of
  their own whose full name is the resource's manifest name.

## .NET 11

The validation of Microsoft.Extensions.Validation 11 looks the message of an attribute naming no resource up in the
localizer of `ValidationOptions.LocalizerProvider`, under `{Type}_{Member}_{Attribute}_Error`, `{Type}_{Attribute}_Error`
and `{Attribute}_Error`, the attribute's type name being `ParsesAsAttribute`, ``ParsesAsAttribute`2`` for the generic
form, and `NotDefaultAttribute`. A resource holding `ParsesAsAttribute_Error` gives one template to every rule of every
`[ParsesAs(typeof(T))]` member that names no resource, the generic form reading ``ParsesAsAttribute`2_Error``: name
`ErrorMessageResourceType` on the attribute to keep the message of each rule, which .NET 11 leaves alone. .NET 11 also
lets an exception an attribute throws, a misuse `VO0039` to `VO0042` report, out of the request, where .NET 10 answers
it with a 400 carrying the exception's message. The compatibility island pins both.

## Records, `[AsParameters]` and minimal APIs

- MVC refuses a validation attribute on a record's property: it throws `InvalidOperationException` on the first request
  validating the record. Put the attributes on the primary constructor's parameters,
  `record Transfer([ParsesAs(typeof(Iban))] string? Iban)`, which is where they land without a target.
- Under `[AsParameters]`, put them on the properties, `[property: ParsesAs(typeof(Iban))]`, or use a class: the
  minimal API's validation reads them there, and Microsoft.AspNetCore.OpenApi 10.0.12 fails to build the whole document
  for an `[AsParameters]` record with a validation attribute on a constructor parameter, `[Required]` alone included
  (dotnet/aspnetcore#65348). The Request Delegate Generator of 10.0.12 does not compile an `[AsParameters]` positional
  record of `string?` members (CS8639), so the RDG suite uses a class.
- The validations generator of .NET 10 describes public types only: a body of an internal type is not validated.

## View models

CommunityToolkit.Mvvm's `[ObservableProperty]` copies the attributes onto the property it generates, on a partial
property, and `ObservableValidator` keeps the results, codes included:

```csharp skip
public partial class TransferViewModel : ObservableValidator
{
    [ObservableProperty, NotifyDataErrorInfo, ParsesAs(typeof(Iban))]
    public partial string? AccountText { get; set; }

    [ObservableProperty, NotifyDataErrorInfo, NotDefault]
    public partial Iban Account { get; set; }
}

var code = viewModel.GetErrors(nameof(TransferViewModel.AccountText)).OfType<ValueObjectValidationResult>().First().ErrorCode;
```

`ValidateDataAnnotations()` and `ObservableValidator` rely on reflection that native AOT trims; `Validator.TryValidateValue`
and the validator `[OptionsValidator]` writes do not, and the native AOT application runs both.

## In the documents

The [OpenAPI transformer and the Swashbuckle filters](./openapi.md#text-held-to-a-value-objects-rules) describe a member
or a parameter carrying `[ParsesAs]` as the text of its value object: a string with its pattern, lengths, format, bounds,
example and known values. So does [NSwag](./openapi.md#nswag), through `AdCodicem.ValueObjects.NJsonSchema`, but a
parameter's own validation attributes replace the value object's lengths and pattern there; the JSON Schema Confluent's
serdes register describes such a member alike.

## Found at build time

| Id | Severity | Reports |
| --- | --- | --- |
| `VO0039` | Error | `[ParsesAs]` on a member that is neither text nor a collection of text, `object` included, which a JSON body fills with a `JsonElement` |
| `VO0040` | Error | `[ParsesAs(typeof(T))]` naming a type that is no value object: text, a class, a generic definition, `null` |
| `VO0041` | Error | `[NotDefault]` on a member that is neither a value object nor its nullable type, a collection of value objects included |
| `VO0042` | Warning | Under `PublishAot`, `[ParsesAs(typeof(T))]` naming a construction of a generic value object, one nested in a generic type, or one written by hand, which a native binary finds only once registered: write `[ParsesAs<T, TValue>]` |

The analyzer reads the attribute where it is written, on a property, a field or a parameter, a lambda's included. A type
parameter, which a construction may make text or a value object, is left alone. `VO0042` reads `PublishAot`, which the
package's build props make visible to the compiler.

## Limits

- The code reaches a host that keeps results, never one that keeps messages: no `errorCodes` in problem details for a
  `[ParsesAs]` refusal.
- A member of the underlying type, an `int MaxItems` held to `Quantity`'s rules, takes no attribute here; FluentValidation's
  `MustSatisfy` checks one.
- The JSON Schema transform of AdCodicem.ValueObjects.Json, and with it the schemas of language-model tools, describes a
  `[ParsesAs]` member as plain text.
- Third-party libraries ship a `NotDefaultAttribute` too; a file importing one beside `AdCodicem.ValueObjects.Annotations`
  names this one through an alias of another name,
  `using ValueObjectNotDefault = AdCodicem.ValueObjects.Annotations.NotDefaultAttribute;`, then
  `[ValueObjectNotDefault]`.
- Blazor's `DataAnnotationsValidator` was not checked: it goes through `Validator`, and its `EditContext` keeps messages
  alone.
