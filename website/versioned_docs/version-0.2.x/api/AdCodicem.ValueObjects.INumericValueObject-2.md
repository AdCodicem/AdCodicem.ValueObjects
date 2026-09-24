# Interface INumericValueObject<TSelf, TValue\> {#AdCodicem_ValueObjects_INumericValueObject_2}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

A value object over a numeric underlying type, exposing arithmetic through the generic math interfaces.

```csharp
public interface INumericValueObject<TSelf, TValue> : IValueObject<TSelf, TValue>, IValueObject<TValue>, IValueObject, IEquatable<TSelf>, IComparable<TSelf>, IComparable, ISpanParsable<TSelf>, IParsable<TSelf>, ISpanFormattable, IFormattable, IAdditionOperators<TSelf, TSelf, TSelf>, ISubtractionOperators<TSelf, TSelf, TSelf>, IComparisonOperators<TSelf, TSelf, bool>, IEqualityOperators<TSelf, TSelf, bool>, IMultiplyOperators<TSelf, TValue, TSelf>, IDivisionOperators<TSelf, TValue, TSelf> where TSelf : struct, INumericValueObject<TSelf, TValue> where TValue : struct, INumber<TValue>
```

#### Type Parameters

`TSelf` 

The value object type itself.

`TValue` 

Underlying numeric type.

#### Implements

[IValueObject<TSelf, TValue\>](AdCodicem.ValueObjects.IValueObject\-2.md), 
[IValueObject<TValue\>](AdCodicem.ValueObjects.IValueObject\-1.md), 
[IValueObject](AdCodicem.ValueObjects.IValueObject.md), 
[IEquatable<TSelf\>](https://learn.microsoft.com/dotnet/api/system.iequatable\-1), 
[IComparable<TSelf\>](https://learn.microsoft.com/dotnet/api/system.icomparable\-1), 
[IComparable](https://learn.microsoft.com/dotnet/api/system.icomparable), 
[ISpanParsable<TSelf\>](https://learn.microsoft.com/dotnet/api/system.ispanparsable\-1), 
[IParsable<TSelf\>](https://learn.microsoft.com/dotnet/api/system.iparsable\-1), 
[ISpanFormattable](https://learn.microsoft.com/dotnet/api/system.ispanformattable), 
[IFormattable](https://learn.microsoft.com/dotnet/api/system.iformattable), 
[IAdditionOperators<TSelf, TSelf, TSelf\>](https://learn.microsoft.com/dotnet/api/system.numerics.iadditionoperators\-3), 
[ISubtractionOperators<TSelf, TSelf, TSelf\>](https://learn.microsoft.com/dotnet/api/system.numerics.isubtractionoperators\-3), 
[IComparisonOperators<TSelf, TSelf, bool\>](https://learn.microsoft.com/dotnet/api/system.numerics.icomparisonoperators\-3), 
[IEqualityOperators<TSelf, TSelf, bool\>](https://learn.microsoft.com/dotnet/api/system.numerics.iequalityoperators\-3), 
[IMultiplyOperators<TSelf, TValue, TSelf\>](https://learn.microsoft.com/dotnet/api/system.numerics.imultiplyoperators\-3), 
[IDivisionOperators<TSelf, TValue, TSelf\>](https://learn.microsoft.com/dotnet/api/system.numerics.idivisionoperators\-3)

## Remarks

<p>
Every arithmetic operator routes its result back through <xref href="AdCodicem.ValueObjects.IValueObject%602.Create(%601)" data-throw-if-not-resolved="false"></xref>,
so an operation that would produce an invalid value throws instead of silently escaping the type's rules —
subtracting 30 from a <code>Percentage</code> of 20 is a bug, not a negative percentage.
</p>
<p>
Unary negation is emitted by the generator only for signed underlying types and is intentionally absent from
this contract, so that value objects over unsigned integers can still take part in generic arithmetic. The
same goes for dividing two value objects into a bare ratio: the operator is emitted on the concrete type,
but declaring it here alongside division by a scalar would make the two instantiations of
<xref href="System.Numerics.IDivisionOperators%603" data-throw-if-not-resolved="false"></xref> unifiable.
</p>

## Properties

### IsZero {#AdCodicem_ValueObjects_INumericValueObject_2_IsZero}

Gets a value indicating whether the carried value is zero.

```csharp
bool IsZero { get; }
```

#### Property Value

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### One {#AdCodicem_ValueObjects_INumericValueObject_2_One}

Gets the value object representing one.

```csharp
public static TSelf One { get; }
```

#### Property Value

 TSelf

#### Exceptions

 [ValueObjectException](AdCodicem.ValueObjects.ValueObjectException.md)

One is not a valid value for <code class="typeparamref">TSelf</code>.

### Zero {#AdCodicem_ValueObjects_INumericValueObject_2_Zero}

Gets the value object representing zero.

```csharp
public static TSelf Zero { get; }
```

#### Property Value

 TSelf

#### Exceptions

 [ValueObjectException](AdCodicem.ValueObjects.ValueObjectException.md)

Zero is not a valid value for <code class="typeparamref">TSelf</code>.

## Methods

### Abs\(TSelf\) {#AdCodicem_ValueObjects_INumericValueObject_2_Abs__0_}

Returns the absolute value of <code class="paramref">value</code>.

```csharp
public static TSelf Abs(TSelf value)
```

#### Parameters

`value` TSelf

Value to take the absolute value of.

#### Returns

 TSelf

The absolute value.

#### Exceptions

 [ValueObjectException](AdCodicem.ValueObjects.ValueObjectException.md)

The absolute value is not valid for <code class="typeparamref">TSelf</code>.

### Max\(TSelf, TSelf\) {#AdCodicem_ValueObjects_INumericValueObject_2_Max__0__0_}

Returns the larger of two values.

```csharp
public static TSelf Max(TSelf left, TSelf right)
```

#### Parameters

`left` TSelf

Left operand.

`right` TSelf

Right operand.

#### Returns

 TSelf

The larger of the two operands.

### Min\(TSelf, TSelf\) {#AdCodicem_ValueObjects_INumericValueObject_2_Min__0__0_}

Returns the smaller of two values.

```csharp
public static TSelf Min(TSelf left, TSelf right)
```

#### Parameters

`left` TSelf

Left operand.

`right` TSelf

Right operand.

#### Returns

 TSelf

The smaller of the two operands.

### Sum\(IEnumerable<TSelf\>\) {#AdCodicem_ValueObjects_INumericValueObject_2_Sum_System_Collections_Generic_IEnumerable__0__}

Computes the sum of a sequence of values.

```csharp
public static TSelf Sum(IEnumerable<TSelf> values)
```

#### Parameters

`values` [IEnumerable](https://learn.microsoft.com/dotnet/api/system.collections.generic.ienumerable\-1)<TSelf\>

Values to add up.

#### Returns

 TSelf

The sum, or the value object built from <code>TValue.Zero</code> for an empty sequence.

#### Remarks

Accumulation happens on the underlying type and the result is validated once, so an intermediate total
that momentarily leaves the valid range does not throw.

