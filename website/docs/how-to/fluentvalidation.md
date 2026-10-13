---
title: Use with FluentValidation
sidebar_label: FluentValidation
slug: /how-to/fluentvalidation
description: Validate commands and payloads against the rules value objects already own, and report their stable error codes.
---

# Use with FluentValidation

A command or an inbound message often carries raw text rather than value objects. Its validator should not state
the length, the pattern and the check-digit rule of an IBAN a second time: it should defer to the type that owns
them, and report the same error codes as the rest of the system.

```bash
dotnet add package AdCodicem.ValueObjects.FluentValidation
```

An application validating with DataAnnotations rather than FluentValidation has the same two checks as attributes,
[`[ParsesAs]` and `[NotDefault]`](./data-annotations.md), in the contracts.

## Text that must become a value object

```csharp skip
public sealed class ImportAccountValidator : AbstractValidator<ImportAccountRequest>
{
    public ImportAccountValidator()
    {
        RuleFor(request => request.Iban)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustParseAs(typeof(Iban));
    }
}
```

`MustParseAs` runs the type's own parsing: normalization, the declared rules, the validator hook. A failure
carries the value object's error code — `value_object.invalid_format` for a wrong check digit — as the
FluentValidation `ErrorCode`.

A `null` passes `MustParseAs`, as it passes every FluentValidation rule but `NotNull` and `NotEmpty`: whether the
member is required is theirs to say, which is why the validator above chains `NotEmpty()` first.

Empty text is the value object's to judge, like any other text:

- a `string` value object refuses it with `value_object.required`, unless it declares `AllowEmpty = true`, in which
  case it passes;
- a value object over any other type — a `Guid`, a number, a date — cannot parse it, and refuses it with
  `value_object.not_parsable`.

So with `NotEmpty()` chained before it, empty text fails both rules, and would be reported twice under one member.
`Cascade(CascadeMode.Stop)` stops the member at its first failure: empty text is reported once, by `NotEmpty()`, and
`MustParseAs` only ever sees text that is there.

The type is passed as a `Type` rather than a type argument so the rule stays readable: C# cannot infer one type
argument while another is given explicitly.

A construction of a generic value object, `Reference<PurchaseOrder>`, and a value object written by hand are described
by reflection the first time a rule names them, as anywhere else. Under native AOT, which cannot, register each before
the validator is built, as
[Where a value object can be declared](../authoring-guide.md#where-a-value-object-can-be-declared) and
[Run-time lookup](./runtime-lookup.md#value-objects-written-by-hand) show: a rule naming one nothing registered fails
with an `ArgumentException` when it is built.

## Your own code, message or severity

By default a failure carries the code and the message of the value object's rule that refused the value. The options
chained on the rule replace them, as they would on any rule:

```csharp skip
RuleFor(request => request.Iban)
    .MustParseAs(typeof(Iban))
    .WithErrorCode("account.invalid")
    .WithMessage("{PropertyName} is not an account number: {Reason}")
    .WithSeverity(Severity.Warning);
```

`{Reason}` is the value object's own message — `The IBAN check digits are incorrect.` — beside the usual
`{PropertyName}`, `{PropertyValue}` and `{PropertyPath}`, and `{CollectionIndex}` in a child validator run for each
element of a collection. A message can also quote `{ValueObject}`, the name of the value object type, and the
arguments of its declared rules for the code that refused the value — `{MinLength}`, `{MaxLength}`, `{Minimum}`,
`{Maximum}`, `{KnownValues}` — as [messages in another language](#messages-in-another-language) lists them. A message
chained alone keeps the value object's code, so a client still branches on
`value_object.invalid_format` while a person reads your wording. `WithName` and `WithState` apply too, and so do the
same options on `MustSatisfy`. So do the global options: `ValidatorOptions.Global.Severity` when no severity is
chained, and `ValidatorOptions.Global.OnFailureCreated`. A message builder set on the rule through `Configure` is not
applied, since FluentValidation does not let it be read back.

## Messages in another language

FluentValidation translates its own messages through `ValidatorOptions.Global.LanguageManager`, and the rules of value
objects read it too. With no message chained, a failure of `MustParseAs` or `MustSatisfy` takes the message the
language manager holds, for the current UI culture, under the key `ValueObject.` followed by the code of the rule, and
the value object's English message, `{Reason}`, when it holds none. An application adds its translations as
FluentValidation documents it:

```csharp skip
public sealed class AppLanguageManager : LanguageManager
{
    public AppLanguageManager()
    {
        AddTranslation("fr", "ValueObject.value_object.too_short", "« {PropertyName} » : au moins {MinLength} caractères.");
        AddTranslation("fr", "ValueObject.value_object.out_of_range", "« {PropertyName} » doit être compris entre {Minimum} et {Maximum}.");
        AddTranslation("fr", "ValueObject.value_object.out_of_range.minimum", "« {PropertyName} » doit valoir au moins {Minimum}.");
        AddTranslation("fr", "ValueObject.value_object.required", "« {PropertyName} » est obligatoire.");
    }
}

ValidatorOptions.Global.LanguageManager = new AppLanguageManager();
```

```text
Iban: "XX"  →  value_object.too_short  « Iban » : au moins 15 caractères.
```

- The placeholders are those of every rule, `{ValueObject}`, the name of the value object type, and the arguments the
  type's declared rules supply for the code: `{MinLength}` for `value_object.too_short`, `{MaxLength}` for
  `value_object.too_long`, `{Minimum}` and `{Maximum}` for `value_object.out_of_range`, either empty when the type
  declares no such bound, and `{KnownValues}` for `value_object.not_a_known_value`. Each is text formatted in the
  current culture, so a format given in the placeholder changes nothing, and a placeholder the code does not supply is
  left as it is. They join the placeholders whatever message is chained: `.WithMessage("Au plus {MaxLength} caractères.")`
  quotes the length declared once on the type.
- For a type declaring one bound alone, `value_object.out_of_range` is looked up under the key followed by `.minimum`
  or `.maximum` first: `ValueObject.value_object.out_of_range.minimum` for `Amount`, which declares a minimum alone.
- The key is the code of the value object's rule, whatever code `WithErrorCode` chains.
- `NotDefault` reads the language manager under `ValueObject.value_object.required`, and keeps
  `'{PropertyName}' is required.` when it holds nothing; `{ValueObject}` is a placeholder there too.
- FluentValidation's language manager falls back from `fr-FR` to `fr`, then to English: an English translation of a
  key, `AddTranslation("en", …)`, answers every specific culture, `fr-FR` or `de-DE`, and the invariant culture, that
  has none of its own, before the key without `.minimum` is looked up, so an English `.minimum` message wins over a
  French message under the key without it. A neutral culture, `fr` or `de` alone, which `UseRequestLocalization()`
  sets when the application lists neutral UI cultures, never falls back to English: without a translation of its own,
  it keeps `{Reason}`.
- `{PropertyValue}` quotes the refused value, as on any FluentValidation rule: a message shown to someone else should
  not quote it.

The keys, the arguments and a French example are in
[the errors reference](../reference/errors.md#a-message-in-the-applications-language), and the
[ASP.NET Core](./aspnet-core.md#messages-in-the-applications-language) integrations read the same keys from a resource.

## An underlying value, without building the value object

```csharp skip
RuleFor(request => request.Amount).MustSatisfy<TransferRequest, Amount, decimal>();
```

`MustSatisfy` checks a raw underlying value against the rules of a value object without constructing one. Like
`MustParseAs`, it lets `null` through.

## An uninitialized value object

```csharp skip
RuleFor(command => command.Account).NotDefault<TransferCommand, Iban, string>();
```

`NotDefault` catches the one thing a struct value object cannot rule out by itself: an instance that was never
constructed, arriving from a place the `VO0010` analyzer cannot see — a deserializer of another library,
reflection, an array element. [Where a default instance can come from](../reference/default-instances.md) lists the
sources observed.

It reads `IsDefault`, which tells an instance equal to `default(TSelf)` and nothing more. Over a `string`, that is
exactly an instance that never went through `Create`. Over a value type, a constructed instance holding the type's
zero equals the default too: `NotDefault` refuses `Amount.Create(0m)` or `OrderId.Create(Guid.Empty)` as
`value_object.required`. Use it on a value object over a value type only when its rules refuse the zero anyway.

## Returning the codes

The codes reach an API client if you put them in the response.
[ASP.NET Core](./aspnet-core.md#codes-for-a-payload-you-validate-yourself) shows how to return them under the
same `errorCodes` member that model binding uses.
