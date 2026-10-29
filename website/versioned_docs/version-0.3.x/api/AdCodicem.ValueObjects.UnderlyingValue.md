# Class UnderlyingValue {#AdCodicem_ValueObjects_UnderlyingValue}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Thin generic bridges to the underlying type's own parsing and formatting, used by generated code.

```csharp
public static class UnderlyingValue
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[UnderlyingValue](AdCodicem.ValueObjects.UnderlyingValue.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

Going through a constrained generic call rather than naming <code>Guid.TryParse</code>, <code>Decimal.TryParse</code> and
the rest one by one keeps the generator honest: any type reachable here is guaranteed by the compiler to
expose the API, and the JIT devirtualizes the call for the concrete type argument, so nothing is paid at
run time for the indirection.

## Methods

### Abs<TValue\>\(TValue\) {#AdCodicem_ValueObjects_UnderlyingValue_Abs__1___0_}

Returns the absolute value of a numeric underlying value.

```csharp
public static TValue Abs<TValue>(TValue value) where TValue : INumberBase<TValue>
```

#### Parameters

`value` TValue

Value to take the absolute value of.

#### Returns

 TValue

The absolute value.

#### Type Parameters

`TValue` 

Underlying value type.

#### Remarks

The numeric constants of the BCL primitives are static abstract members of <code>INumberBase</code>, reachable
only through a type parameter. These bridges are how generated code names them.

### IsZero<TValue\>\(TValue\) {#AdCodicem_ValueObjects_UnderlyingValue_IsZero__1___0_}

Determines whether a numeric underlying value is zero.

```csharp
public static bool IsZero<TValue>(TValue value) where TValue : INumberBase<TValue>
```

#### Parameters

`value` TValue

Value to test.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the value is zero.

#### Type Parameters

`TValue` 

Underlying value type.

#### Remarks

The numeric constants of the BCL primitives are static abstract members of <code>INumberBase</code>, reachable
only through a type parameter. These bridges are how generated code names them.

### One<TValue\>\(\) {#AdCodicem_ValueObjects_UnderlyingValue_One__1}

Gets the multiplicative identity of a numeric underlying type.

```csharp
public static TValue One<TValue>() where TValue : INumberBase<TValue>
```

#### Returns

 TValue

One.

#### Type Parameters

`TValue` 

Underlying value type.

#### Remarks

The numeric constants of the BCL primitives are static abstract members of <code>INumberBase</code>, reachable
only through a type parameter. These bridges are how generated code names them.

### TryConvertToInteger<TNumber, TValue\>\(TNumber, out TValue\) {#AdCodicem_ValueObjects_UnderlyingValue_TryConvertToInteger__2___0___1__}

Converts a number to an integer type with a checked conversion that never truncates.

```csharp
public static bool TryConvertToInteger<TNumber, TValue>(TNumber number, out TValue value) where TNumber : INumberBase<TNumber> where TValue : IBinaryInteger<TValue>
```

#### Parameters

`number` TNumber

Number to convert.

`value` TValue

The same number, as a <code class="typeparamref">TValue</code>.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when <code class="typeparamref">TValue</code> holds the number whole; <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a> when the
number is out of its range, has a fraction, or is not a number at all.

#### Type Parameters

`TNumber` 

Type of the number to convert.

`TValue` 

Integer type to convert it to.

#### Remarks

The type converter of a numeric value object goes through it for a number of another numeric type than its own,
such as the <xref href="System.Int64" data-throw-if-not-resolved="false"></xref> Newtonsoft.Json reads every JSON integer as, or the <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref> a numeric
control holds, and to hand its value to such a type. A checked cast alone would drop a fraction, <code>7.5</code>
becoming 7, and that is refused too.

### TryConvertToReal<TNumber, TValue\>\(TNumber, out TValue\) {#AdCodicem_ValueObjects_UnderlyingValue_TryConvertToReal__2___0___1__}

Converts a number to a real type with a checked conversion.

```csharp
public static bool TryConvertToReal<TNumber, TValue>(TNumber number, out TValue value) where TNumber : INumberBase<TNumber> where TValue : IFloatingPoint<TValue>
```

#### Parameters

`number` TNumber

Number to convert.

`value` TValue

The nearest <code class="typeparamref">TValue</code> to the number.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the number is within the range of <code class="typeparamref">TValue</code>;
<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a> when it is beyond it, or is <code>NaN</code> or an infinity, which a <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref> does
not hold.

#### Type Parameters

`TNumber` 

Type of the number to convert.

`TValue` 

Real type to convert it to: <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, <xref href="System.Double" data-throw-if-not-resolved="false"></xref> or <xref href="System.Single" data-throw-if-not-resolved="false"></xref>.

#### Remarks

The type converter of a numeric value object goes through it as it goes through
<xref href="AdCodicem.ValueObjects.UnderlyingValue.TryConvertToInteger%60%602(%60%600%2c%60%601%40)" data-throw-if-not-resolved="false"></xref>. A real holds the number to its own
precision, as a cast does: <code>0.1</code> converted to a <xref href="System.Single" data-throw-if-not-resolved="false"></xref> is the nearest <xref href="System.Single" data-throw-if-not-resolved="false"></xref>.
A <xref href="System.Double" data-throw-if-not-resolved="false"></xref> or a <xref href="System.Single" data-throw-if-not-resolved="false"></xref> converted to a <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref> is the one exception: a cast
keeps fifteen significant digits of a <xref href="System.Double" data-throw-if-not-resolved="false"></xref> and seven of a <xref href="System.Single" data-throw-if-not-resolved="false"></xref>, fewer than either
carries, so that <code>16777216f</code> would become 16777220. It is read from its shortest round-trip text instead,
which keeps every digit it holds, as the Newtonsoft.Json converter reads a JSON number Newtonsoft.Json turned into
a <xref href="System.Double" data-throw-if-not-resolved="false"></xref>. What the bridge refuses is a number beyond the range of the real: a
<xref href="System.Decimal" data-throw-if-not-resolved="false"></xref> has no infinity to hold one, and a <xref href="System.Double" data-throw-if-not-resolved="false"></xref> beyond the range of a
<xref href="System.Single" data-throw-if-not-resolved="false"></xref> would otherwise become an infinity, which is not the number either. <code>NaN</code> and an
infinity carry over to a <xref href="System.Double" data-throw-if-not-resolved="false"></xref> or a <xref href="System.Single" data-throw-if-not-resolved="false"></xref> as they are, and a <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>
refuses them.

### TryFormat<TValue\>\(in TValue, Span<char\>, out int, ReadOnlySpan<char\>, IFormatProvider?\) {#AdCodicem_ValueObjects_UnderlyingValue_TryFormat__1___0__System_Span_System_Char__System_Int32__System_ReadOnlySpan_System_Char__System_IFormatProvider_}

Formats a value into a destination span using the underlying type's own formatter.

```csharp
public static bool TryFormat<TValue>(in TValue value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) where TValue : ISpanFormattable
```

#### Parameters

`value` TValue

Value to format.

`destination` [Span](https://learn.microsoft.com/dotnet/api/system.span\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Destination buffer.

`charsWritten` [int](https://learn.microsoft.com/dotnet/api/system.int32)

Number of characters written.

`format` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Format specifier.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

Format provider.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the value fitted in <code class="paramref">destination</code>.

#### Type Parameters

`TValue` 

Underlying value type.

#### Remarks

Several BCL types, <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> among them, implement the four-argument
<xref href="System.ISpanFormattable.TryFormat(System.Span%7bSystem.Char%7d%2cSystem.Int32%40%2cSystem.ReadOnlySpan%7bSystem.Char%7d%2cSystem.IFormatProvider)" data-throw-if-not-resolved="false"></xref> explicitly and only expose a shorter public overload. Routing
through a constrained type parameter reaches the interface method uniformly, and the call is
devirtualized for the concrete type argument.

### TryParse<TValue\>\(ReadOnlySpan<char\>, IFormatProvider?, out TValue\) {#AdCodicem_ValueObjects_UnderlyingValue_TryParse__1_System_ReadOnlySpan_System_Char__System_IFormatProvider___0__}

Parses a span using the underlying type's own parser.

```csharp
public static bool TryParse<TValue>(ReadOnlySpan<char> text, IFormatProvider? provider, out TValue value) where TValue : ISpanParsable<TValue>
```

#### Parameters

`text` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Text to parse.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

Format provider.

`value` TValue

The parsed value.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the text was parsed.

#### Type Parameters

`TValue` 

Underlying value type.

### TryParse<TValue\>\(string?, IFormatProvider?, out TValue\) {#AdCodicem_ValueObjects_UnderlyingValue_TryParse__1_System_String_System_IFormatProvider___0__}

Parses a string using the underlying type's own parser.

```csharp
public static bool TryParse<TValue>(string? text, IFormatProvider? provider, out TValue value) where TValue : IParsable<TValue>
```

#### Parameters

`text` [string](https://learn.microsoft.com/dotnet/api/system.string)?

Text to parse.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

Format provider.

`value` TValue

The parsed value.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the text was parsed.

#### Type Parameters

`TValue` 

Underlying value type.

### TryParse<TValue\>\(ReadOnlySpan<char\>, NumberStyles, IFormatProvider?, out TValue\) {#AdCodicem_ValueObjects_UnderlyingValue_TryParse__1_System_ReadOnlySpan_System_Char__System_Globalization_NumberStyles_System_IFormatProvider___0__}

Parses a span using the underlying number type's own parser, with the given styles in the invariant culture.

```csharp
public static bool TryParse<TValue>(ReadOnlySpan<char> text, NumberStyles invariantStyle, IFormatProvider? provider, out TValue value) where TValue : INumberBase<TValue>
```

#### Parameters

`text` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Text to parse.

`invariantStyle` [NumberStyles](https://learn.microsoft.com/dotnet/api/system.globalization.numberstyles)

The styles the text is read with when <code class="paramref">provider</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> or the invariant
culture.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

Format provider. <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> stands for <xref href="System.Globalization.CultureInfo.InvariantCulture" data-throw-if-not-resolved="false"></xref>; any culture other
than the invariant one reads the text with the type's own styles.

`value` TValue

The parsed value.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the text was parsed.

#### Type Parameters

`TValue` 

Underlying value type.

#### Remarks

The own styles of <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, <xref href="System.Double" data-throw-if-not-resolved="false"></xref> and <xref href="System.Single" data-throw-if-not-resolved="false"></xref> accept the group separator,
a comma in the invariant culture, so text written with a decimal comma, <code>12,5</code>, would read as 125. Generated
code hands those styles over without <xref href="System.Globalization.NumberStyles.AllowThousands" data-throw-if-not-resolved="false"></xref>. A culture that is not the
invariant one says what a comma is, and keeps the type's own styles: <code>fr-FR</code> reads <code>12,5</code> as 12.5.
The invariant culture is recognized by equality, so a <xref href="System.Globalization.CultureInfo" data-throw-if-not-resolved="false"></xref> created for the empty name is
read as <xref href="System.Globalization.CultureInfo.InvariantCulture" data-throw-if-not-resolved="false"></xref> is, and so is its number format,
<xref href="System.Globalization.NumberFormatInfo.InvariantInfo" data-throw-if-not-resolved="false"></xref>, which callers of <code>decimal.Parse</code> pass as often.

### Zero<TValue\>\(\) {#AdCodicem_ValueObjects_UnderlyingValue_Zero__1}

Gets the additive identity of a numeric underlying type.

```csharp
public static TValue Zero<TValue>() where TValue : INumberBase<TValue>
```

#### Returns

 TValue

Zero.

#### Type Parameters

`TValue` 

Underlying value type.

#### Remarks

The numeric constants of the BCL primitives are static abstract members of <code>INumberBase</code>, reachable
only through a type parameter. These bridges are how generated code names them.

