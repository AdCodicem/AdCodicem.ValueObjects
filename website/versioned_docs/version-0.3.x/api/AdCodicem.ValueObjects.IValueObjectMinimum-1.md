# Interface IValueObjectMinimum<TValue\> {#AdCodicem_ValueObjects_IValueObjectMinimum_1}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Declares the inclusive lower bound of a value object, as a value of its underlying type.

```csharp
public interface IValueObjectMinimum<TValue>
```

#### Type Parameters

`TValue` 

Underlying value type: a number, a <xref href="System.Char" data-throw-if-not-resolved="false"></xref>, a date, a time or a duration.

## Remarks

<p>
The compiler checks the type of the bound, and the bound can be any expression of that type:
<code>public static DateOnly Minimum =&gt; new(1900, 1, 1);</code>. The generated <code>Validate</code> refuses a smaller value
with <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.OutOfRange" data-throw-if-not-resolved="false"></xref>, and the OpenAPI schema publishes the bound, in the form the
JSON converter writes it.
</p>
<p>
The bound is a constant. The check reads it each time it runs, which costs nothing for a constant, folded into the
check by the JIT, while the schema reads it once, when the value object's type initializes, which the generated
registration does as the declaring assembly loads. A bound that changed would therefore be checked against a value
the schema does not publish, and a bound must neither throw nor depend on the state of the application. A bound
relative to the clock, "not in the future" or "within 90 days", is a rule rather than a bound: implement it in
<xref href="AdCodicem.ValueObjects.IValueObjectValidator%601" data-throw-if-not-resolved="false"></xref>, reading the time from a <xref href="System.TimeProvider" data-throw-if-not-resolved="false"></xref> a test can fix.
</p>
<p>
Write it as an expression-bodied property, as above. An initialized property, <code>{ get; } = ...</code>, is assigned
with the type's other static fields, in the order they are declared: an instance created from a static field
declared before it, <code>public static readonly Percentage Full = Create(100);</code>, would be checked against the
default of the type.
</p>

## Properties

### Minimum {#AdCodicem_ValueObjects_IValueObjectMinimum_1_Minimum}

Gets the smallest value the value object accepts.

```csharp
public static abstract TValue Minimum { get; }
```

#### Property Value

 TValue

