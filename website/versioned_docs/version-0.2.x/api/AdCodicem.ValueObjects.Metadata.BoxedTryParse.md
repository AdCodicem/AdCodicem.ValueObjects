# Delegate BoxedTryParse {#AdCodicem_ValueObjects_Metadata_BoxedTryParse}

Namespace: [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Attempts to build a value object from its text representation.

```csharp
public delegate bool BoxedTryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out object? result, out ValidationResult validation)
```

#### Parameters

`text` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Text to parse.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

Format provider used to parse the underlying value.

`result` [object](https://learn.microsoft.com/dotnet/api/system.object)?

The boxed value object, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> when the text is rejected.

`validation` [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

The outcome of the parse and validation.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the text was accepted.

