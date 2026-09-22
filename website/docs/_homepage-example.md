<!--
  The example on the homepage. It lives here rather than in src/pages/index.tsx so that it is frozen with the
  rest of the documentation at each stable release, which lets the homepage show the latest stable version's
  code, and so that DocumentationSnippetTests checks it like every other published snippet.
  docusaurus.config.ts resolves it by this file name: keep the two in step.
-->

```csharp
[ValueObject<string>(
    MinLength = 15,
    MaxLength = 34,
    Pattern = "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
    SchemaFormat = "iban")]
public readonly partial struct Iban : IValueObjectNormalizer<string>, IValueObjectValidator<string>
{
    public static string NormalizeValue(string value) => /* strip separators, upper-case */;

    public static ValidationResult ValidateValue(in string value)
        => HasValidCheckDigits(value)
            ? ValidationResult.Success
            : ValidationResult.InvalidFormat("The IBAN check digits are incorrect.");
}
```

```csharp skip
var iban = Iban.Create("fr76 3000 6000 0112 3456 7890 189");
iban.Value                              // "FR7630006000011234567890189"
JsonSerializer.Serialize(new { iban })  // {"iban":"FR7630006000011234567890189"}
```
