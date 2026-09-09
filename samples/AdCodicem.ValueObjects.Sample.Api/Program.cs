using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.OpenApi;
using AdCodicem.ValueObjects.Sample.Api.Contracts;
using AdCodicem.ValueObjects.Sample.Api.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Model binding for routes, query strings and headers, plus JSON serialization of value objects.
builder.Services.AddControllers().AddValueObjects();

// The stable error code of the violated rule is added to the automatic 400 response.
builder.Services.Configure<ApiBehaviorOptions>(static options => options.AddValueObjectProblemDetails());

// Value objects are documented as their underlying type, carrying the rules declared on the type.
builder.Services.AddOpenApi(static options => options.AddValueObjects());

builder.Services.AddScoped<IValidator<ImportAccountRequest>, ImportAccountRequestValidator>();

builder.Services.AddDbContext<BankingDbContext>((provider, options) =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("Banking");

    switch (configuration["Database:Provider"])
    {
        case "SqlServer":
            options.UseSqlServer(connectionString);
            break;
        default:
            options.UseNpgsql(connectionString);
            break;
    }
});

var app = builder.Build();

app.MapOpenApi();
app.MapControllers();

// A minimal API needs nothing from the ASP.NET Core package: a generated value object implements IParsable<T>,
// which is exactly what minimal API parameter binding looks for.
app.MapGet("/accounts/{iban}", async (Iban iban, BankingDbContext database, CancellationToken cancellationToken) =>
{
    var account = await database.Accounts.FirstOrDefaultAsync(entity => entity.Iban == iban, cancellationToken);

    return account is null
        ? Results.NotFound()
        : Results.Ok(new AccountResponse(account.Iban, account.Balance));
});

// Shows a validator deferring to the value object's own rules for a payload that carries raw text.
app.MapPost("/accounts/import", (ImportAccountRequest request, IValidator<ImportAccountRequest> validator) =>
{
    var result = validator.Validate(request);

    return result.IsValid
        ? Results.Accepted()
        : Results.ValidationProblem(
            result.ToDictionary(),
            extensions: new Dictionary<string, object?>
            {
                [ValueObjectProblemDetails.ExtensionName] = result.Errors
                    .ToDictionary(failure => failure.PropertyName, failure => failure.ErrorCode),
            });
});

await app.RunAsync();

/// <summary>
/// Entry point, made reachable so that integration tests can host the application.
/// </summary>
public partial class Program;
