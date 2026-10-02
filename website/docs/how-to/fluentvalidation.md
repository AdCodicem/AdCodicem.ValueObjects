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
element of a collection. A message chained alone keeps the value object's code, so a client still branches on
`value_object.invalid_format` while a person reads your wording. `WithName` and `WithState` apply too, and so do the
same options on `MustSatisfy`. So do the global options: `ValidatorOptions.Global.Severity` when no severity is
chained, and `ValidatorOptions.Global.OnFailureCreated`. A message builder set on the rule through `Configure` is not
applied, since FluentValidation does not let it be read back.

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
reflection, an array element.

## Returning the codes

The codes reach an API client if you put them in the response.
[ASP.NET Core](./aspnet-core.md#codes-for-a-payload-you-validate-yourself) shows how to return them under the
same `errorCodes` member that model binding uses.
