# Interface IValueObjectSpanNormalizer {#AdCodicem_ValueObjects_IValueObjectSpanNormalizer}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Declares that a string value object can normalize straight from text, without materializing it first.

```csharp
public interface IValueObjectSpanNormalizer
```

## Remarks

<p>
Implement this alongside <xref href="AdCodicem.ValueObjects.IValueObjectNormalizer%601" data-throw-if-not-resolved="false"></xref> on a value object whose underlying
type is <xref href="System.String" data-throw-if-not-resolved="false"></xref>. Parsing and JSON reading then route through the span overload, so ingesting a
value allocates the normalized string and nothing else; without it, the raw text is materialized first and
immediately thrown away.
</p>
<p>
The two overloads must agree. Write the value-typed one as a one-line delegation:
<code>public static string NormalizeValue(string value) =&gt; NormalizeValue(value.AsSpan());</code>
</p>

## Methods

### NormalizeValue\(ReadOnlySpan<char\>\) {#AdCodicem_ValueObjects_IValueObjectSpanNormalizer_NormalizeValue_System_ReadOnlySpan_System_Char__}

Puts text into its canonical form.

```csharp
public static abstract string NormalizeValue(ReadOnlySpan<char> value)
```

#### Parameters

`value` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Text to normalize.

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)

The canonical form of the text.

