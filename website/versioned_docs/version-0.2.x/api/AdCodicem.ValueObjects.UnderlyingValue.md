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

