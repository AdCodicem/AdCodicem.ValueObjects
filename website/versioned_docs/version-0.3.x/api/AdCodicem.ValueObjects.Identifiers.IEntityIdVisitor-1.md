# Interface IEntityIdVisitor<TResult\> {#AdCodicem_ValueObjects_Identifiers_IEntityIdVisitor_1}

Namespace: [AdCodicem.ValueObjects.Identifiers](AdCodicem.ValueObjects.Identifiers.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.dll  

Receives the type of an entity identifier that a caller knows only by its descriptor, so that the caller closes its
own generic code over it at compile time.

```csharp
public interface IEntityIdVisitor<out TResult>
```

#### Type Parameters

`TResult` 

What the visitor hands back: the adapter it built, a flag.

## Remarks

<xref href="AdCodicem.ValueObjects.Identifiers.EntityIdDescriptor.Accept%60%601(AdCodicem.ValueObjects.Identifiers.IEntityIdVisitor%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref> calls <xref href="AdCodicem.ValueObjects.Identifiers.IEntityIdVisitor%601.Visit%60%601" data-throw-if-not-resolved="false"></xref> with the
type the descriptor was built for, as <xref href="AdCodicem.ValueObjects.Metadata.ValueObjectDescriptor.Accept%60%601(AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>
hands a value object's type arguments to an <xref href="AdCodicem.ValueObjects.Metadata.IValueObjectVisitor%601" data-throw-if-not-resolved="false"></xref>. An integration that
finds identifiers in <xref href="AdCodicem.ValueObjects.Identifiers.EntityIdRegistry" data-throw-if-not-resolved="false"></xref> creates its adapter there, <code>new MyGenerator&lt;TId&gt;()</code>,
where <xref href="System.Type.MakeGenericType(System.Type%5b%5d)" data-throw-if-not-resolved="false"></xref> would close it at run time, which native AOT cannot do for a struct;
and it reaches the members only an identifier has, <xref href="AdCodicem.ValueObjects.Identifiers.IEntityId%601.New" data-throw-if-not-resolved="false"></xref> among them, through the
constraint.

## Methods

### Visit<TId\>\(\) {#AdCodicem_ValueObjects_Identifiers_IEntityIdVisitor_1_Visit__1}

Receives the type of the identifier.

```csharp
TResult Visit<TId>() where TId : struct, IEntityId<TId>
```

#### Returns

 TResult

What the visitor builds for the identifier.

#### Type Parameters

`TId` 

The identifier type.

