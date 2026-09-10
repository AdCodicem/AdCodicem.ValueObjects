---
title: Getting Started
sidebar_label: Getting Started
slug: /getting-started
---

# Getting started

```bash
dotnet add package AdCodicem.ValueObjects
```

Then wire up whichever boundaries you have:

```csharp
builder.Services.AddControllers().AddValueObjects();
builder.Services.Configure<ApiBehaviorOptions>(o => o.AddValueObjectProblemDetails());
builder.Services.AddOpenApi(o => o.AddValueObjects());

protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    => builder.ConfigureValueObjects(typeof(Iban).Assembly);
```

Minimal APIs need nothing extra: a generated value object implements `IParsable<T>`, which is exactly what
minimal API parameter binding looks for.

## Testing your own value objects

`AdCodicem.ValueObjects.Testing` ships a contract kit that derives a dozen checks from a short declaration:

```csharp
public sealed class IbanContract : ValueObjectContract<Iban, string>
{
    protected override IEnumerable<string> AcceptedValues => ["FR7630006000011234567890189"];
    protected override IEnumerable<string> RejectedValues => ["", "not-an-iban"];
}
```

Normalization settles, equality and ordering agree, text and JSON round-trip, rejected values are rejected the
same way by every entry point.

Next: [Packages](./packages.md), for which package covers which boundary.
