# Interface IValueObjectMaximum<TValue\> {#AdCodicem_ValueObjects_IValueObjectMaximum_1}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Declares the inclusive upper bound of a value object, as a value of its underlying type.

```csharp
public interface IValueObjectMaximum<TValue>
```

#### Type Parameters

`TValue` 

Underlying value type: a number, a <xref href="System.Char" data-throw-if-not-resolved="false"></xref>, a date, a time or a duration.

## Remarks

The bound is checked, published and read as <xref href="AdCodicem.ValueObjects.IValueObjectMinimum%601" data-throw-if-not-resolved="false"></xref> describes.

## Properties

### Maximum {#AdCodicem_ValueObjects_IValueObjectMaximum_1_Maximum}

Gets the largest value the value object accepts.

```csharp
public static abstract TValue Maximum { get; }
```

#### Property Value

 TValue

