# Interface IValueObject<TSelf, TValue\> {#AdCodicem_ValueObjects_IValueObject_2}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

The full contract of a single-value value object, self-referencing so that construction, parsing and
comparison are resolved statically without reflection or boxing.

```csharp
public interface IValueObject<TSelf, TValue> : IValueObject<TValue>, IValueObject, IEquatable<TSelf>, IComparable<TSelf>, IComparable, ISpanParsable<TSelf>, IParsable<TSelf>, ISpanFormattable, IFormattable where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Type Parameters

`TSelf` 

The value object type itself.

`TValue` 

Underlying value type.

#### Implements

[IValueObject<TValue\>](AdCodicem.ValueObjects.IValueObject\-1.md), 
[IValueObject](AdCodicem.ValueObjects.IValueObject.md), 
[IEquatable<TSelf\>](https://learn.microsoft.com/dotnet/api/system.iequatable\-1), 
[IComparable<TSelf\>](https://learn.microsoft.com/dotnet/api/system.icomparable\-1), 
[IComparable](https://learn.microsoft.com/dotnet/api/system.icomparable), 
[ISpanParsable<TSelf\>](https://learn.microsoft.com/dotnet/api/system.ispanparsable\-1), 
[IParsable<TSelf\>](https://learn.microsoft.com/dotnet/api/system.iparsable\-1), 
[ISpanFormattable](https://learn.microsoft.com/dotnet/api/system.ispanformattable), 
[IFormattable](https://learn.microsoft.com/dotnet/api/system.iformattable)

## Remarks

<p>
Implementations are expected to be <code>readonly partial struct</code>s produced by the
<code>AdCodicem.ValueObjects.Generators</code> source generator. Writing one by hand is supported but tedious.
</p>
<p>
The construction pipeline is always <code>Normalize</code> then <code>Validate</code> then assign, so a non-default
instance is by construction both normalized and valid.
</p>

## Properties

### IsDefault {#AdCodicem_ValueObjects_IValueObject_2_IsDefault}

Gets a value indicating whether this instance is the uninitialized <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/default">default</a> of its type.

```csharp
bool IsDefault { get; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

#### Remarks

<code>default(TSelf)</code> and <code>new TSelf()</code> bypass validation because the CLR always allows them for a
struct. The analyzers shipped with <code>AdCodicem.ValueObjects</code> report those expressions as errors; this
property is the runtime guard for values that cross a boundary the analyzer cannot see.

## Methods

### Create\(TValue\) {#AdCodicem_ValueObjects_IValueObject_2_Create__1_}

Normalizes, validates, and creates a value object.

```csharp
public static abstract TSelf Create(TValue value)
```

#### Parameters

`value` TValue

Candidate value.

#### Returns

 TSelf

The created value object.

#### Exceptions

 [ValueObjectException](AdCodicem.ValueObjects.ValueObjectException.md)

<code class="paramref">value</code> violates one of the rules.

### CreateUnchecked\(TValue\) {#AdCodicem_ValueObjects_IValueObject_2_CreateUnchecked__1_}

Creates a value object from a value that is already known to be normalized and valid.

```csharp
public static abstract TSelf CreateUnchecked(TValue value)
```

#### Parameters

`value` TValue

Trusted value.

#### Returns

 TSelf

The created value object.

#### Remarks

This is the trusted-source fast path: it performs no work at all. It is used when materializing entities
from a database the application itself wrote to. Feeding it unvalidated input defeats the whole point of
the type.

### Normalize\(TValue\) {#AdCodicem_ValueObjects_IValueObject_2_Normalize__1_}

Normalizes a candidate value into its canonical form.

```csharp
public static abstract TValue Normalize(TValue value)
```

#### Parameters

`value` TValue

Candidate value.

#### Returns

 TValue

The canonical form of <code class="paramref">value</code>.

#### Remarks

Normalization must be idempotent: <code>Normalize(Normalize(x))</code> equals <code>Normalize(x)</code>. It must not
reject values — an unnormalizable value is rejected by <xref href="AdCodicem.ValueObjects.IValueObject%602.Validate(%601%40)" data-throw-if-not-resolved="false"></xref> instead.

### TryCreate\(TValue, out TSelf\) {#AdCodicem_ValueObjects_IValueObject_2_TryCreate__1__0__}

Normalizes, validates, and creates a value object without throwing.

```csharp
public static abstract bool TryCreate(TValue value, out TSelf result)
```

#### Parameters

`value` TValue

Candidate value.

`result` TSelf

The created value object, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/default">default</a> when the value is rejected.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when <code class="paramref">value</code> was accepted.

### TryCreate\(TValue, out TSelf, out ValidationResult\) {#AdCodicem_ValueObjects_IValueObject_2_TryCreate__1__0__AdCodicem_ValueObjects_ValidationResult__}

Normalizes, validates, and creates a value object without throwing, reporting why a value was rejected.

```csharp
public static abstract bool TryCreate(TValue value, out TSelf result, out ValidationResult validation)
```

#### Parameters

`value` TValue

Candidate value.

`result` TSelf

The created value object, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/default">default</a> when the value is rejected.

`validation` [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

The outcome of the validation.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when <code class="paramref">value</code> was accepted.

### TryParse\(ReadOnlySpan<char\>, IFormatProvider?, out TSelf, out ValidationResult\) {#AdCodicem_ValueObjects_IValueObject_2_TryParse_System_ReadOnlySpan_System_Char__System_IFormatProvider__0__AdCodicem_ValueObjects_ValidationResult__}

Parses text without throwing, reporting why the text was rejected.

```csharp
public static abstract bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out TSelf result, out ValidationResult validation)
```

#### Parameters

`text` [ReadOnlySpan](https://learn.microsoft.com/dotnet/api/system.readonlyspan\-1)<[char](https://learn.microsoft.com/dotnet/api/system.char)\>

Text to parse.

`provider` [IFormatProvider](https://learn.microsoft.com/dotnet/api/system.iformatprovider)?

Format provider used to parse the underlying value.

`result` TSelf

The parsed value object, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/default">default</a> when the text is rejected.

`validation` [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

The outcome, distinguishing text that does not even have the shape of the underlying type from text that
parses but breaks one of the type's rules.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> when the text was accepted.

#### Remarks

This is what every boundary wants: model binding, configuration binding and data readers all need to tell
the caller which rule was violated, not merely that something went wrong.

### Validate\(in TValue\) {#AdCodicem_ValueObjects_IValueObject_2_Validate__1__}

Validates an already normalized candidate value.

```csharp
public static abstract ValidationResult Validate(in TValue value)
```

#### Parameters

`value` TValue

Normalized candidate value.

#### Returns

 [ValidationResult](AdCodicem.ValueObjects.ValidationResult.md)

The outcome of the first violated rule, or <xref href="AdCodicem.ValueObjects.ValidationResult.Success" data-throw-if-not-resolved="false"></xref>.

