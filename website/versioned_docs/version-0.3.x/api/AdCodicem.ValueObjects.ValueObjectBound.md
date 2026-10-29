# Class ValueObjectBound {#AdCodicem_ValueObjects_ValueObjectBound}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

A generic bridge to the bounds a value object declares through <xref href="AdCodicem.ValueObjects.IValueObjectMinimum%601" data-throw-if-not-resolved="false"></xref> and
<xref href="AdCodicem.ValueObjects.IValueObjectMaximum%601" data-throw-if-not-resolved="false"></xref>, used by generated code.

```csharp
public static class ValueObjectBound
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectBound](AdCodicem.ValueObjects.ValueObjectBound.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
A static abstract member is reached through a type parameter, which finds it however the value object implements
it. Named on the type, <code>BirthDate.Minimum</code> would miss an explicit implementation and fail to compile in a file
the consumer cannot edit.
</p>
<p>
The bound is read from the value object each time it is asked for, and nothing keeps it. A bound is a constant, which
the JIT folds into the check that reads it, so a copy would save nothing; and a copy taken while the value object's
own static fields are still being initialized, by a well-known instance created before the bound is assigned, would
keep the default of the type for the life of the process.
</p>

## Methods

### Maximum<TSelf, TValue\>\(\) {#AdCodicem_ValueObjects_ValueObjectBound_Maximum__2}

Gets the inclusive upper bound a value object declares.

```csharp
public static TValue Maximum<TSelf, TValue>() where TSelf : IValueObjectMaximum<TValue>
```

#### Returns

 TValue

The bound.

#### Type Parameters

`TSelf` 

The value object.

`TValue` 

Its underlying type.

### Minimum<TSelf, TValue\>\(\) {#AdCodicem_ValueObjects_ValueObjectBound_Minimum__2}

Gets the inclusive lower bound a value object declares.

```csharp
public static TValue Minimum<TSelf, TValue>() where TSelf : IValueObjectMinimum<TValue>
```

#### Returns

 TValue

The bound.

#### Type Parameters

`TSelf` 

The value object.

`TValue` 

Its underlying type.

### Text<TValue\>\(TValue\) {#AdCodicem_ValueObjects_ValueObjectBound_Text__1___0_}

Writes a bound as text, in the one invariant form a bound of its type is written in.

```csharp
public static string Text<TValue>(TValue bound)
```

#### Parameters

`bound` TValue

The bound.

#### Returns

 [string](https://learn.microsoft.com/dotnet/api/system.string)

The text: digits for an integer or a decimal, the round-trip form of a real, the ISO 8601 form of a date or
    a time, the constant form of a duration, and the character itself for a <xref href="System.Char" data-throw-if-not-resolved="false"></xref>.

#### Type Parameters

`TValue` 

The type of the bound.

#### Remarks

<p>
It is the text the schema publishes, which the OpenAPI transformer and the type's own parser read back, and the
text the message of a rejected value quotes.
</p>
<p>
A <xref href="System.DateTime" data-throw-if-not-resolved="false"></xref> is written without its <xref href="System.DateTime.Kind" data-throw-if-not-resolved="false"></xref>, neither a <code>Z</code> nor an offset:
the check compares clock readings, whatever their kind, and a <xref href="System.DateTimeKind.Local" data-throw-if-not-resolved="false"></xref> bound would
otherwise publish the offset of whichever machine serves the document.
</p>

