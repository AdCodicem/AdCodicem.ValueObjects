# Interface IValueObjectStringFormatter<TValue\> {#AdCodicem_ValueObjects_IValueObjectStringFormatter_1}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Declares that a value object produces its text directly, when writing into a buffer would be wasteful.

```csharp
public interface IValueObjectStringFormatter<TValue>
```

#### Type Parameters

`TValue` 

Underlying value type.

## Remarks

Prefer <xref href="AdCodicem.ValueObjects.IValueObjectFormatter%601" data-throw-if-not-resolved="false"></xref>, which can format without allocating. This one exists for
rules whose output is naturally a string, and takes precedence over it when both are implemented.

## Methods

### FormatValue\(in TValue, ReadOnlySpan<char\>, IFormatProvider?\) {#AdCodicem_ValueObjects_IValueObjectStringFormatter_1_FormatValue__0__System_ReadOnlySpan_System_Char__System_IFormatProvider_}

Produces the text of a value.

```csharp
public static abstract string FormatValue(in TValue value, ReadOnlySpan<char> format, IFormatProvider? provider)
```

#### Parameters

`value` TValue

Value to format.

`format` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Format specifier, possibly empty.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

Format provider.

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)

The formatted text.

