# {{PackageId}}

{{Description}}

It is a package of AdCodicem.ValueObjects, single-value DDD value objects for .NET 10 and later: a `readonly partial
struct` marked `[ValueObject<T>]` gets its whole implementation from a source generator, and crosses every boundary as
its underlying type. It works with the main package, which holds the generator:

```sh
dotnet add package AdCodicem.ValueObjects
dotnet add package {{PackageId}}
```

- Documentation: <https://adcodicem.github.io/AdCodicem.ValueObjects/>
- Every released package, and what each integrates: <https://adcodicem.github.io/AdCodicem.ValueObjects/docs/packages>
- Every package of the next version, previews included:
  <https://adcodicem.github.io/AdCodicem.ValueObjects/docs/preview/packages>
- Changes: <https://github.com/AdCodicem/AdCodicem.ValueObjects/blob/main/CHANGELOG.md>
- Source and issues: <https://github.com/AdCodicem/AdCodicem.ValueObjects>
