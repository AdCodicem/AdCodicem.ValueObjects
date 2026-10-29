# Class ValueObjectArbitrary {#AdCodicem_ValueObjects_FsCheck_ValueObjectArbitrary}

Namespace: [AdCodicem.ValueObjects.FsCheck](AdCodicem.ValueObjects.FsCheck.md)  
Assembly: AdCodicem.ValueObjects.FsCheck.dll  

The FsCheck arbitrary of a value object: values its rules accept, drawn from the schema it declares, biased towards the
edges it declares, and shrinking to values it accepts.

```csharp
public static class ValueObjectArbitrary
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectArbitrary](AdCodicem.ValueObjects.FsCheck.ValueObjectArbitrary.md)

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
FsCheck draws the seed of each value, which seeds the <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSampler" data-throw-if-not-resolved="false"></xref> that draws it, so that a failing
run replays from the seed FsCheck reports. One value in four is one of the type's boundaries
(<xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSampler.Boundaries%60%602" data-throw-if-not-resolved="false"></xref>), its bounds, its lengths and its known values, since a
property over a wide type lands on its edges by chance once in a blue moon; the size FsCheck passes is not read.
</p>
<p>
A counterexample shrinks through <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSampler.Shrink%60%602(%60%600%2cSystem.Func%7b%60%601%2cSystem.Collections.Generic.IEnumerable%7b%60%601%7d%7d)" data-throw-if-not-resolved="false"></xref>: towards the values the type
declares, its minimum, its first known value and its example, then through FsCheck's own shrinker of the underlying
value, keeping only what the type accepts as it is, its normalizer leaving it unchanged. FsCheck has a shrinker for the
integers up to 64 bits, <xref href="System.Single" data-throw-if-not-resolved="false"></xref>, <xref href="System.Double" data-throw-if-not-resolved="false"></xref>, <xref href="System.Decimal" data-throw-if-not-resolved="false"></xref>, <xref href="System.Char" data-throw-if-not-resolved="false"></xref>,
<xref href="System.String" data-throw-if-not-resolved="false"></xref>, <xref href="System.DateTime" data-throw-if-not-resolved="false"></xref>, <xref href="System.DateTimeOffset" data-throw-if-not-resolved="false"></xref> and <xref href="System.TimeSpan" data-throw-if-not-resolved="false"></xref>; a value object over
another type, <xref href="System.Boolean" data-throw-if-not-resolved="false"></xref>, <xref href="System.Guid" data-throw-if-not-resolved="false"></xref>, <xref href="System.DateOnly" data-throw-if-not-resolved="false"></xref> or <xref href="System.Int128" data-throw-if-not-resolved="false"></xref> for one, shrinks to
its declared values alone. From a declared value, a shrink goes on only to the declared values ranked before it.
</p>
<p>
Nothing of the value object is read before the first draw: neither its schema nor its type initializer runs when the
arbitrary is built or merged.
</p>

## Methods

### For<TSelf, TValue\>\(ValueObjectSamplerOptions?\) {#AdCodicem_ValueObjects_FsCheck_ValueObjectArbitrary_For__2_AdCodicem_ValueObjects_Testing_Data_ValueObjectSamplerOptions_}

Gets the arbitrary of a value object: values its rules accept, one in four at its boundaries, shrunk to values it
accepts.

```csharp
public static Arbitrary<TSelf> For<TSelf, TValue>(ValueObjectSamplerOptions? options = null) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`options` [ValueObjectSamplerOptions](AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplerOptions.md)?

How each value is drawn, the generators of the rules no schema carries included, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for the defaults.

#### Returns

 Arbitrary<TSelf\>

The arbitrary, to merge into a map with <code>MergeArb</code> or to hand to <code>Prop.ForAll</code>.

#### Type Parameters

`TSelf` 

The value object.

`TValue` 

Its underlying type.

#### Remarks

A draw that finds no value the type accepts throws a <xref href="AdCodicem.ValueObjects.Testing.Data.ValueObjectSamplingException" data-throw-if-not-resolved="false"></xref>, out of the
property's check.

