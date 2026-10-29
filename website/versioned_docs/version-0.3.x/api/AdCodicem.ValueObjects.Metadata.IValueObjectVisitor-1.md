# Interface IValueObjectVisitor<TResult\> {#AdCodicem_ValueObjects_Metadata_IValueObjectVisitor_1}

Namespace: [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Receives the type arguments of a value object that a caller knows only by its descriptor, so that the caller closes
its own generic code over them at compile time.

```csharp
public interface IValueObjectVisitor<out TResult>
```

#### Type Parameters

`TResult` 

What the visitor hands back: the adapter it built, a flag, a schema read off the type.

## Remarks

<p>
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.Accept%60%601(AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref> calls
<xref href="AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%601.Visit%60%602" data-throw-if-not-resolved="false"></xref> with the type arguments the descriptor was built with. An integration that finds
value objects by <xref href="System.Type" data-throw-if-not-resolved="false"></xref> creates its adapter there, <code>new MyFormatter&lt;TSelf, TValue&gt;()</code>, rather
than closing it with <xref href="System.Type.MakeGenericType(System.Type%5b%5d)" data-throw-if-not-resolved="false"></xref> and <xref href="System.Activator" data-throw-if-not-resolved="false"></xref>, which has no native code
to run for a struct under native AOT.
</p>
<p>
Under native AOT, the compiler generates <xref href="AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%601.Visit%60%602" data-throw-if-not-resolved="false"></xref> of each visitor the application creates for
each value object a descriptor is built for in code: every value object the generator registers, and every value
object registered by hand, a value object written by hand or a construction of a generic one. One the registry
describes by reflection, the first time <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.TryResolve(System.Type%2cAdCodicem.ValueObjects.Metadata.ValueObjectDescriptor%40)" data-throw-if-not-resolved="false"></xref> is asked for it because nothing
registered it, has a visitor call too, which only the JIT can run.
</p>
<p>
A visitor needing context, a builder or a flag, holds it in fields. The calls happen at start-up, where creating a
visitor per call costs nothing that matters.
</p>

## Methods

### Visit<TSelf, TValue\>\(\) {#AdCodicem_ValueObjects_Metadata_IValueObjectVisitor_1_Visit__2}

Receives the type arguments of the value object.

```csharp
TResult Visit<TSelf, TValue>() where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Returns

 TResult

What the visitor builds for the value object.

#### Type Parameters

`TSelf` 

The value object type.

`TValue` 

Its underlying type.

