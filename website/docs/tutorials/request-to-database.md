---
title: From Request to Database
sidebar_label: From request to database
slug: /tutorials/request-to-database
description: Take value objects through an ASP.NET Core API, its OpenAPI document and EF Core, declaring each rule once.
---

# From request to database

This tutorial puts value objects behind a small ASP.NET Core API that stores customers with EF Core. Every rule
is declared once, on the type, and each boundary picks it up from there. The complete application is the
[sample API](https://github.com/AdCodicem/AdCodicem.ValueObjects/tree/main/samples/AdCodicem.ValueObjects.Sample.Api)
in the repository.

## The packages

```bash
dotnet add package AdCodicem.ValueObjects
dotnet add package AdCodicem.ValueObjects.AspNetCore
dotnet add package AdCodicem.ValueObjects.OpenApi
dotnet add package AdCodicem.ValueObjects.EntityFrameworkCore
```

## The domain

Three types, each declaring its own rules:

```csharp
[ValueObject<Guid>]
public readonly partial struct CustomerId : IValueObjectValidator<Guid>
{
    public static CustomerId New() => CreateUnchecked(Guid.CreateVersion7());

    public static ValidationResult ValidateValue(in Guid value)
        => value == Guid.Empty
            ? ValidationResult.Required("A customer identifier must not be empty.")
            : ValidationResult.Success;
}

[ValueObject<string>(MaxLength = 254, SchemaFormat = "email")]
public readonly partial struct EmailAddress : IValueObjectNormalizer<string>, IValueObjectPatternValidator
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    public static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();
}

[ValueObject<string>(ValueSet = ValueSetKind.Closed, MinLength = 2, MaxLength = 2)]
[KnownValue("France", "FR")]
[KnownValue("Belgium", "BE")]
[KnownValue("Luxembourg", "LU")]
public readonly partial struct CountryCode : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}
```

`CustomerId.New()` is a member of your own, next to the generated ones. `CreateUnchecked` is legitimate there
because the application produced the value itself; it never is for input that comes from outside.
`EmailAddress` declares its format through `IValueObjectPatternValidator`: the regex source generator compiles
the `[GeneratedRegex]`, so its file needs `using System.Text.RegularExpressions;`.

The entity and the contracts use the types directly, with no `string` in sight:

```csharp skip
public sealed class Customer
{
    public CustomerId Id { get; set; }
    public EmailAddress Email { get; set; }
    public CountryCode Country { get; set; }
}

public sealed record CreateCustomerRequest(EmailAddress Email, CountryCode Country);
public sealed record CustomerResponse(CustomerId Id, EmailAddress Email, CountryCode Country);
```

## The API

```csharp skip
var builder = WebApplication.CreateBuilder(args);

// Model binding for routes, query strings and headers, and JSON for request and response bodies.
builder.Services.AddControllers().AddValueObjects();

// The error code of the violated rule is added to the automatic 400 response.
builder.Services.Configure<ApiBehaviorOptions>(options => options.AddValueObjectProblemDetails());

// Value objects are documented as their underlying type, with the rules declared on them.
builder.Services.AddOpenApi(options => options.AddValueObjects());

builder.Services.AddDbContext<ShopDbContext>(options => options.UseNpgsql(connectionString));
```

A controller takes the value objects as parameters, from any source:

```csharp skip
[HttpGet("{id}")]
public async Task<ActionResult<CustomerResponse>> GetById(CustomerId id, CancellationToken cancellationToken)

[HttpGet]
public async Task<IReadOnlyList<CustomerResponse>> List([FromQuery] CountryCode? country, CancellationToken cancellationToken)

[HttpPost]
public async Task<ActionResult<CustomerResponse>> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
```

A minimal API needs none of the above to bind: a generated value object implements `IParsable<T>`, which is what
minimal API parameter binding looks for. A rejected value is a bare 400 there, though, with no code: the problem
details carrying it are MVC's. Under native AOT, a value object declared in the project that maps the endpoints also
lists its contract on its declaration, as
[the Request Delegate Generator](../how-to/aspnet-core.md#the-request-delegate-generator) explains; the sample keeps
its value objects in a domain project of their own, which needs nothing.

```csharp skip
app.MapGet("/customers/{id}", async (CustomerId id, ShopDbContext database) => /* … */);
```

## The database

```csharp skip
public sealed class ShopDbContext(DbContextOptions<ShopDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        => configurationBuilder.ConfigureValueObjects(typeof(CustomerId).Assembly);
}
```

That one call maps every value object of the assembly: `CustomerId` to the provider's native GUID column
(`uuid`, `uniqueidentifier`), and `EmailAddress` to text bounded at 254 characters because the type says 254 —
`character varying(254)` on PostgreSQL, `nvarchar(254)` on SQL Server. A LINQ query
compares value objects the way it would compare the underlying values — `Where(c => c.Country == country)`
becomes an ordinary `WHERE` on the column.

## What you get

**Bodies carry bare values.** A request sends `{"email": "  Ada@Example.COM ", "country": "fr"}` and the
response comes back as `{"id": "0193…", "email": "ada@example.com", "country": "FR"}`: normalized on the way
in, and never wrapped in an object.

**A rejected value names its rule.** `GET /customers?country=ZZ` fails model binding, and the problem details
response carries the stable code next to the message:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "country": ["The value is not one of the accepted values."] },
  "errorCodes": { "country": "value_object.not_a_known_value" }
}
```

`errorCodes` covers values bound from the route, the query string, headers and forms, and a value inside a JSON
body, under its JSON path: `{"email": "not-an-email"}` answers with `"errorCodes": { "$.email":
"value_object.invalid_format" }`. A payload that carries raw text rather than value objects is validated with
[FluentValidation](../how-to/fluentvalidation.md), which reports the value object's own codes.

**The OpenAPI document states the rules.** `EmailAddress` is documented as
`{"type": "string", "format": "email", "maxLength": 254, "pattern": "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$"}` and
`CountryCode` carries `"enum": ["FR", "BE", "LU"]`. Nothing was written for it beyond the declaration.

**The column is sized by the type.** Change `MaxLength` and the next migration resizes the column. The rule has
one home.

## Reading rows back

Materializing a row does not validate the value again: it uses `CreateUnchecked`, because it is the hottest path
in most applications and reads values this same application validated when it wrote them. For a table that
another system also writes to, turn validation back on:

```csharp skip
configurationBuilder.ConfigureValueObjects(strict: true, typeof(CustomerId).Assembly);
```

Next: [Public identifiers](./public-identifiers.md), for identifiers that clients see.
