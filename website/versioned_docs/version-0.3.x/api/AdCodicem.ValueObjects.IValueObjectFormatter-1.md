# Interface IValueObjectFormatter<TValue\> {#AdCodicem_ValueObjects_IValueObjectFormatter_1}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Declares that a value object formats itself, rather than deferring to its underlying value.

```csharp
public interface IValueObjectFormatter<TValue>
```

#### Type Parameters

`TValue` 

Underlying value type.

## Remarks

<p>
Implementing this takes over formatting entirely, including the default format, so it must handle an empty
or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> format specifier. It is what gives a value object named formats: an IBAN printed
in groups of four, or masked down to its last four characters. A type that also implements
<xref href="AdCodicem.ValueObjects.IValueObjectStringFormatter%601" data-throw-if-not-resolved="false"></xref> never calls this one: the string formatter answers everywhere.
</p>
<p>
The generated <code>ToString(format, provider)</code> calls it with a stack buffer, then with a pooled buffer twice as
large each time it returns <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>, up to 1,048,576 characters, past which it throws a
<xref href="System.FormatException" data-throw-if-not-resolved="false"></xref>. A format the rule does not support is therefore refused by throwing that
exception, not by returning <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>. On a <xref href="System.String" data-throw-if-not-resolved="false"></xref> value object, text equal to the
value returns the string the value object holds, so formatting a value unchanged allocates nothing.
</p>

## Methods

### TryFormatValue\(in TValue, Span<char\>, out int, ReadOnlySpan<char\>, IFormatProvider?\) {#AdCodicem_ValueObjects_IValueObjectFormatter_1_TryFormatValue__0__System_Span_System_Char__System_Int32__System_ReadOnlySpan_System_Char__System_IFormatProvider_}

Writes the formatted value into a destination buffer.

```csharp
public static abstract bool TryFormatValue(in TValue value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
```

#### Parameters

`value` TValue

Value to format.

`destination` [Span](https://learn.microsoft.com/dotnet/api/system.span\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Buffer to write into.

`charsWritten` [int](https://learn.microsoft.com/dotnet/api/system.int32)

Characters written, when the buffer was large enough.

`format` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Format specifier, possibly empty.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

Format provider.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a> when the destination was too small, as the framework expects.

