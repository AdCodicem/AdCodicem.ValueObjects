# Delegate BoxedTryCreate {#AdCodicem_ValueObjects_Metadata_BoxedTryCreate}

Namespace: [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Attempts to build a value object from a boxed underlying value.

```csharp
public delegate bool BoxedTryCreate(object? value, out object? result, out ValidationResult validation)
```

#### Parameters

`value` [object](https://learn.microsoft.com/dotnet/api/system.object)?

Boxed candidate value.

`result` [object](https://learn.microsoft.com/dotnet/api/system.object)?

The boxed value object, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> when the value is rejected.

`validation` [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

The outcome of the validation.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the value was accepted.

