# AdCodicem.ValueObjects

An answer to primitive obsession for .NET 10 and later: single-value DDD value objects, generated at compile time, with
no reflection and no allocation on the paths that matter. This is the package to install: it brings the source
generator, its analyzers and code fixes, and the abstractions the generated code implements.

```sh
dotnet add package AdCodicem.ValueObjects
```

A `readonly partial struct` marked `[ValueObject<T>]` gets its whole implementation: construction that normalizes, then
validates, then assigns, so that an instance is valid by construction; equality, comparison, parsing and formatting;
and a JSON converter and a type converter, through which it crosses every boundary as its underlying type.

```csharp
using AdCodicem.ValueObjects;
using AdCodicem.ValueObjects.Annotations;

namespace Shop;

[ValueObject<string>(MinLength = 3, MaxLength = 12)]
public readonly partial struct Sku;

public static class Catalogue
{
    public static Sku Parse(string text) => Sku.Create(text);

    public static bool TryRead(string text, out Sku sku) => Sku.TryCreate(text, out sku);
}
```

Its rules are declared once, and reach JSON, EF Core, model binding, the OpenAPI document and every other integration
through the packages of the same name.

- Documentation: <https://adcodicem.github.io/AdCodicem.ValueObjects/>
- Every released package, and what each integrates: <https://adcodicem.github.io/AdCodicem.ValueObjects/docs/packages>
- Every package of the next version, previews included:
  <https://adcodicem.github.io/AdCodicem.ValueObjects/docs/preview/packages>
- Changes: <https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/CHANGELOG.md>
- Source and issues: <https://github.com/AdCodicem/AdCodicem.ValueObjects>
