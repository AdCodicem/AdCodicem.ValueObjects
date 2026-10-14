---
title: Use with Blazor forms
sidebar_label: Blazor forms
slug: /how-to/blazor
description: Type value objects into Blazor forms through their rules with InputValueObject, the normalized instance bound, the refused rule's message under the field and its code on the EditContext, in the application's language if it asks.
---

# Use with Blazor forms

```bash
dotnet add package AdCodicem.ValueObjects.Blazor
```

```razor
@using AdCodicem.ValueObjects.Blazor   @* in _Imports.razor *@
```

Razor finds a component through a Razor `@using`, not a C# `global using`: without it, the compiler reports `RZ10012`,
"Found markup element with unexpected name 'InputValueObject'", and fails on `@bind-Value` with `RZ9991`.

```razor
<EditForm Model="Order" OnValidSubmit="Save">
    <DataAnnotationsValidator />
    <InputValueObject @bind-Value="Order.Iban" DisplayName="IBAN" />
    <ValidationMessage For="() => Order.Iban" />
    <InputValueObject @bind-Value="Order.Quantity" />
    <InputValueObject @bind-Value="Order.Reference" />   @* Iban?, Reference<PurchaseOrder>, any value object *@
    <ValidationSummary />
</EditForm>
```

```csharp skip
builder.Services.AddValueObjectInputs(options => options.UseResource<ValidationMessages>());   // optional, messages by code
```

The package references `Microsoft.AspNetCore.Components.Web`, not the ASP.NET Core shared framework, so a Blazor
WebAssembly application installs it as a Blazor Server or a Blazor Web App one does. In a server application, the shared
framework's copy of the Components assemblies is the one loaded.

## Without the package

A value object reaches a Blazor form through `@bind`, which reads text with `BindConverter`, and through it the
`[TypeConverter]` the generator writes:

- **Refused text is dropped without a message.** The converter throws a `ValueObjectException` carrying the rule's code;
  the binder swallows it, never calls the setter, and the field goes back to the last valid value.
- **Empty text sets the default instance.** The binder calls the setter with `default`: an `Iban` becomes the instance
  nobody filled in, and a `Quantity` bounded 1..100 becomes 0, which the input then shows, a value its own rules refuse.
- **The culture drops text silently.** `13,5` typed into a `@bind` without `@bind:culture` disappears the same way.

None of the built-in inputs binds a value object either. `InputText` is for `string` alone, `InputNumber<Quantity>`
fails in its type initializer and `InputDate<DueDate>` throws "Unsupported … type param". `InputSelect` and
`InputRadioGroup` parse an option through `BindConverter` and catch only `InvalidOperationException`, so a refused option
throws the `ValueObjectException` out of the change event, which is fatal to the circuit of a Blazor Server application
([below](#inputselect-and-inputradiogroup)).

## The input in a form

`InputValueObject<TValue>` takes a value object or its nullable type, which Razor infers from `@bind-Value`. It renders a
text `<input>`, as `InputText` does, with the classes `modified`, `valid` and `invalid`, `aria-invalid`, the field's
`name` in a static form and the attributes it is given, and reads its text through the value object's `TryParse`, the
overload that reports the rule:

- **Accepted text binds the normalized instance**, which the input then shows: `fr76 3000 6000 0112 3456 7890 189`
  becomes `FR7630006000011234567890189` in the field, ` po-1042 ` becomes `PO-1042`.
- **Refused text** leaves the model as it was and stays in the field, marked `modified invalid` with
  `aria-invalid="true"`. The rule's message is added to the form's validation messages, so `ValidationMessage` and
  `ValidationSummary` show it, `EditContext.Validate()` returns `false` and `OnValidSubmit` is not called until the text
  is fixed. The message is the rule's own, "The value must be at least 15 characters long.", "The value must be less
  than or equal to 100.", "The value is not one of the accepted values." for a closed set, and never quotes the text
  typed. A parser written by hand that refuses without a reason is answered "The text could not be parsed as Code.", and
  one that gives an empty message "The field Code is invalid.", as `[ParsesAs]` answers them.

Text typed into a form comes from outside, so it always goes through the rules: there is no `trusted` option. A refusal
is a validation message, never an exception.

## Nullable members and empty text

- **On a nullable value object**, `Iban?`, empty text binds `null`, with no message: `InputBase` does it before any
  parsing.
- **On a value object that cannot be null**, empty text is parsed as any other text. A type that accepts it binds what it
  creates: `[ValueObject<string>(AllowEmpty = true)]` binds the empty instance, not the default.
- **A type that refuses it** is answered with `value_object.required` and the message `[Required]` gives, "The {0} field
  is required.", `{0}` being the input's `DisplayName`, or else the name of the field. A `Guid` value object would
  otherwise say "The text is not a valid System.Guid." of a field the person emptied. The model keeps its value, never
  the default.

## Which text an instance shows

The input writes the text its type reads back:

- **A value object without a formatting hook** shows its own text, written in the round-trip form of its underlying type:
  `2026-10-14` for a `DateOnly`, `10:30:15.2500000` for a `TimeOnly`, `2026-10-14T10:30:15.2500000Z` for a `DateTime`,
  its kind included, `2026-10-14T10:30:15.2500000+02:00` for a `DateTimeOffset`, `1.01:01:01.0010000` for a `TimeSpan`,
  and a number in the culture of the input. A `DateOnly` value object therefore suits `type="date"`, whose value is that
  form; a number suits `type="number"` only with `Culture="CultureInfo.InvariantCulture"`, the form a browser gives it.
- **A value object whose formatting hook writes other text** shows its bare underlying value, in that same form: a
  temperature that prints `21 °C` shows `21`, since its parser refuses its own `ToString()`.
- **An instance equal to the default that its rules refuse**, a field nobody filled in, shows as empty text: an unset
  `Quantity` bounded 1..100 no longer shows `0`. An instance equal to the default that they accept, a count from 0 or
  0 °C, shows as its value, so that a static form posts it back.

## The culture

The text is read and written in the input's `Culture`. When it is not set:

- in **the current culture** where the component is interactive, as `@bind` and `InputSelect` read text, and while an
  interactive component prerenders, so that the prerendered page and the interactive one show `12,5` alike in `fr-FR`;
- in **the invariant culture** under static rendering, which is the culture the framework reads a posted form in: a
  static `fr-FR` page writes `12.5`, and its untouched post reads back.

A date, of a value object over `DateOnly`, `DateTime` or `DateTimeOffset`, is read in the Gregorian calendar its text is
written in, whatever the culture's own. In the cultures whose calendar is another, the Thai Buddhist one of `th-TH`, the
Persian one of `fa-IR` or the Um al-Qura one of `ar-SA`, the input reads with a copy of the culture set to its Gregorian
calendar: the culture's patterns and month names stay, and a year typed is a Gregorian one. `1990-10-14`, the text the
input shows, and `14/10/1990` in `th-TH` read as 14 October 1990; `14/10/2533`, the Buddhist year, reads as the year 2533,
which a type bounded to plausible birth dates refuses. Read in the culture's own calendar, the text the input writes
would come back 543 years earlier in `th-TH`, 621 years later in `fa-IR`, and refused in `ar-SA`.

## The code

An `EditContext` holds a refusal as a message alone. The input keeps the code of the rule beside it, on the form, from the
refusal until the next text it reads, an emptied nullable field included, or until it is disposed:

```csharp skip
if (editContext.TryGetValueObjectErrorCode(FieldIdentifier.Create(() => Order.Quantity), out var code))
{
    // value_object.out_of_range, or the code of the application's own rule
}
```

This is the field's equivalent of `ValueObjectErrors.ErrorCodeKey`, for a test, a telemetry hook or a component that
styles a field by rule rather than by its English message. An input outside an `EditForm` reads and binds its text the
same way, and has no form to keep a code on.

## Messages in the application's language

```csharp skip
builder.Services.AddValueObjectInputs(options => options.UseResource<ValidationMessages>());
```

The message of a refusal is then looked up in the resources of `ValidationMessages` by the code of the rule, under the
keys of [the errors reference](../reference/errors.md#a-message-in-the-applications-language),
`ValueObject.value_object.too_short` first, with the input's `DisplayName`, or else its field's name, as `{0}` and the
arguments the type's rules supply from `{1}`, in the current UI culture, and formatted in the current culture: the culture
a Blazor Server circuit or a Blazor WebAssembly application runs in. "« IBAN » : au moins 15 caractères." and
"« Quantity » doit être compris entre 1 et 100." come from a `fr` resource; a code the resource has no key for keeps the
English message. `AddValueObjectInputs` registers `AddLocalization()`, keeping the localizer factory and the
`ResourcesPath` the application registered first. Options configured by hand, without the localization services, fail
the first refusal with an `InvalidOperationException` that names `builder.Services.AddLocalization()`, thrown from the
change event.

A Blazor WebAssembly application loads the satellite assemblies of the culture it starts in.

## A field nobody touched

The input checks the text it is given; a field nobody touched holds its default instance, which no text refused.
`[NotDefault]` under `DataAnnotationsValidator` is the guard
([DataAnnotations](./data-annotations.md#an-uninitialized-value-object)):

```csharp skip
public sealed class Order
{
    [NotDefault]
    public Iban Account { get; set; }
}
```

An untouched form is then refused on submit with "The Account field is required.". While the field shows refused text
and the model still holds the default, the field carries both messages, the input's and `[NotDefault]`'s:
DataAnnotations names the field by `[Display]`, or else by its member, and the input by its `DisplayName`.

## Static rendering

A page that sets no render mode is rendered statically, and its form is posted back. The framework maps the post into the
`[SupplyParameterFromForm]` model, through `IParsable<T>` and in the invariant culture, before any component runs, and
nothing public lets a package change that mapping. So in a static form:

- **A refused value is answered with the framework's message**, "The value 'XX' is not valid for 'Account'.", which quotes
  the text and carries neither the rule's message nor its code, and the member it refused holds its default.
- **An optional value object left empty is refused**, "The value '' is not valid for 'Optional'.": the framework maps
  empty text to `null` only for primitive types, `DateOnly`, `TimeOnly` and `DateTimeOffset`, on purpose
  (dotnet/aspnetcore#52499 and #60498).
- **Accepted text binds**, normalized, and the invariant text the input writes reads back untouched: a date, a time, a
  duration and a number in a `fr-FR` page alike.

For a static form whose refusals should carry the rule's message, keep the text and hold it to the rules with
[`[ParsesAs]`](./data-annotations.md), which `DataAnnotationsValidator` checks on submit:

```csharp skip
public sealed class OrderForm
{
    [ParsesAs(typeof(Iban))]
    public string? Account { get; set; }
}
```

```razor
<InputText @bind-Value="Model.Account" />
<ValidationMessage For="() => Model.Account" />   @* The value must be at least 15 characters long. *@
```

The model then holds text, which the application converts once the form is valid, and `DataAnnotationsValidator` keeps
the message alone, without the code.

## InputSelect and InputRadioGroup

`InputSelect<CountryCode>` and `InputRadioGroup` bind an option a value object accepts, and throw the
`ValueObjectException` of one it refuses out of the change event, which ends a Blazor Server circuit. Render the options
from the known values the type declares, so that none is refused:

```razor
<InputSelect @bind-Value="Order.Country">
    @foreach (var known in CountryCode.Schema.KnownValues)
    {
        <option value="@known">@known</option>
    }
</InputSelect>
```

## The attributes of the rules

```razor
<InputValueObject @bind-Value="Order.Iban" SchemaAttributes="true" />
```

`SchemaAttributes="true"` writes the `maxlength` and `minlength` of the type's rules, which a browser counts in UTF-16
code units as .NET does, and its pattern, when a browser reads it as .NET does:
`maxlength="34" minlength="15" pattern="^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$"`. An attribute the input is given wins:
`maxlength="40"` beside `SchemaAttributes` keeps 40.

The attributes are off by default, because a browser checks the text as it is typed, before the normalizer runs. A
Maltese IBAN, 31 characters, typed in its printed groups of four is 38, which a browser may stop at `maxlength="34"`;
`fr76 3000 …` does not match the canonical pattern, and a static `EditForm`, rendered without `novalidate`, would be
blocked by the browser's own validation before any .NET code reads it; an interactive form would show the browser's
popups, in its language, beside the input's messages. Reserve them for a type whose normalizer leaves typed text alone.

A browser compiles `pattern` as `^(?:…)$` with the `v` flag, and drops a pattern it cannot compile. The input writes a
pattern only when the browser accepts every text the .NET pattern accepts:

- each alternative is anchored at both ends, `^…$`, as the browser anchors it;
- `\d`, `\w`, `\s` and `\p{…}` are written as the characters of the Basic Multilingual Plane .NET matches them with, so
  that `^[A-Z]{3}-\d{3,10}$` accepts `ABC-١٢٣` in the browser as in .NET, where the raw `\d` refuses it;
- a lazy quantifier is written greedy and a group without capturing, and `-` is escaped in a class, where the raw
  `^[A-Z0-9-]+$` is a syntax error under the `v` flag;
- no pattern is written for `.`, a negated class or escape, a word boundary, a class subtraction, a surrogate, a named
  block or the categories `C` and `Cs`, which the engines read apart, nor for a pattern the shared reader refuses, a
  lookaround among them.

## Generic and hand-written value objects

The input finds the value object in the registry, where a generated one is registered when its assembly loads. A value
object written by hand, and a construction of a generic one, `Reference<PurchaseOrder>`, are described by reflection the
first time an input binds them, where the runtime supports dynamic code. Where it does not, they are registered by hand
before the input renders, or its first render fails with an `InvalidOperationException` naming the registration:

```csharp skip
ValueObjectRegistry.Register<HandWrittenCode, string>(HandWrittenCode.Schema);
ValueObjectRegistry.Register<Reference<PurchaseOrder>, string>(static () => new Reference<PurchaseOrder>.ValueJsonConverter());
```

## Native AOT and trimming

The package is AOT-compatible: each input closes its text adapters over the value object through the descriptor's
visitor, never with `MakeGenericType`. A trimmed Blazor WebAssembly publish, checked when the package was designed
and not in CI, reported warnings from Microsoft.AspNetCore.Components and Microsoft.JSInterop alone, none from these
assemblies. Native AOT is no Blazor host, but CI renders a form of every registered value object through `HtmlRenderer` in a native binary, whose output
matches the JIT's; Microsoft.AspNetCore.Components warns for itself there, `IL2104` and `IL3053`, once a component
renders.

## Testing a form with bUnit

The input reads `RendererInfo`, so bUnit asks for it, or throws `MissingRendererInfoException`. Register the services
first, then set it. An interactive form reports an interactive renderer:

```csharp skip
using var context = new BunitContext();
context.Services.AddValueObjectInputs(options => options.UseResource<ValidationMessages>());
context.SetRendererInfo(new RendererInfo("Server", isInteractive: true));

var form = context.Render<OrderForm>();
form.Find("input").Change("XX");
```

A component an interactive page prerenders reports a static renderer, with its render mode assigned, which is what makes
the input write in the current culture there:

```csharp skip
context.SetRendererInfo(new RendererInfo("Static", isInteractive: false));

var form = context.Render<OrderForm>(parameters => parameters.SetAssignedRenderMode(RenderMode.InteractiveServer));
```

A static renderer with no render mode assigned stands for a statically rendered page, read and written in the invariant
culture.

## .NET 11

The input built against .NET 10 works on the .NET 11 release candidate, whose `InputBase` moved into
Microsoft.AspNetCore.Components.Web and is forwarded. Two differences show: `ValidationMessage` writes the
`data-valmsg-for` and `data-valmsg-replace` attributes of the client validation of static forms, so read messages from
the `EditContext` rather than the markup, and `InputText` writes an `id` of its own, which this input does not: give it
one explicitly. `EditContext.Validate()` is obsolete there, for `ValidateAsync()`, which refuses a form holding refused
text alike.

## Not covered

- **A select of known values.** `InputValueObjectSelect<TValue>`, rendering the options of a closed set from its known
  values, follows on demand.
- **Component libraries.** The inputs of MudBlazor, Radzen and Fluent UI were not checked; those that read text through
  `BindConverter` presumably bind a value object as `@bind` does.
- **A browser.** What a browser makes of `maxlength` and `pattern`, a circuit ended by `InputSelect`, and the culture a
  circuit or a WebAssembly application gives the messages follow the HTML standard and Microsoft's documentation; no
  browser ran them.
