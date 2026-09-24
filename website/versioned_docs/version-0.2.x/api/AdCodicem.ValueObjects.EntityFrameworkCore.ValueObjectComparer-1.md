# Class ValueObjectComparer<TSelf\> {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectComparer_1}

Namespace: [AdCodicem.ValueObjects.EntityFrameworkCore](AdCodicem.ValueObjects.EntityFrameworkCore.md)  
Assembly: AdCodicem.ValueObjects.EntityFrameworkCore.dll  

Compares value objects using their own equality rather than the underlying value's.

```csharp
public sealed class ValueObjectComparer<TSelf> : ValueComparer<TSelf>, IEqualityComparer, IEqualityComparer<object>, IEqualityComparer<TSelf> where TSelf : struct, IEquatable<TSelf>
```

#### Type Parameters

`TSelf` 

Value object type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueComparer](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer) ← 
[ValueComparer<TSelf\>](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer\-1) ← 
[ValueObjectComparer<TSelf\>](AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectComparer\-1.md)

#### Implements

[IEqualityComparer](https://learn.microsoft.com/dotnet/api/system.collections.iequalitycomparer), 
[IEqualityComparer<object\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.iequalitycomparer\-1), 
[IEqualityComparer<TSelf\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.iequalitycomparer\-1)

#### Inherited Members

[ValueComparer<TSelf\>.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer\-1.equals\#microsoft\-entityframeworkcore\-changetracking\-valuecomparer\-1\-equals\(system\-object\-system\-object\)), 
[ValueComparer<TSelf\>.GetHashCode\(object?\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer\-1.gethashcode\#microsoft\-entityframeworkcore\-changetracking\-valuecomparer\-1\-gethashcode\(system\-object\)), 
[ValueComparer<TSelf\>.Equals\(TSelf, TSelf\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer\-1.equals\#microsoft\-entityframeworkcore\-changetracking\-valuecomparer\-1\-equals\(\-0\-0\)), 
[ValueComparer<TSelf\>.GetHashCode\(TSelf\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer\-1.gethashcode\#microsoft\-entityframeworkcore\-changetracking\-valuecomparer\-1\-gethashcode\(\-0\)), 
[ValueComparer<TSelf\>.Snapshot\(object?\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer\-1.snapshot\#microsoft\-entityframeworkcore\-changetracking\-valuecomparer\-1\-snapshot\(system\-object\)), 
[ValueComparer<TSelf\>.Snapshot\(TSelf\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer\-1.snapshot\#microsoft\-entityframeworkcore\-changetracking\-valuecomparer\-1\-snapshot\(\-0\)), 
[ValueComparer<TSelf\>.ObjectEqualsExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer\-1.objectequalsexpression), 
[ValueComparer<TSelf\>.Type](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer\-1.type), 
[ValueComparer<TSelf\>.EqualsExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer\-1.equalsexpression), 
[ValueComparer<TSelf\>.HashCodeExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer\-1.hashcodeexpression), 
[ValueComparer<TSelf\>.SnapshotExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer\-1.snapshotexpression), 
[ValueComparer.GetGenericSnapshotMethod\(Type\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.getgenericsnapshotmethod), 
[ValueComparer.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.equals), 
[ValueComparer.GetHashCode\(object?\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.gethashcode), 
[ValueComparer.Snapshot\(object?\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.snapshot), 
[ValueComparer.ExtractEqualsBody\(Expression, Expression\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.extractequalsbody), 
[ValueComparer.ExtractHashCodeBody\(Expression\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.extracthashcodebody), 
[ValueComparer.ExtractSnapshotBody\(Expression\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.extractsnapshotbody), 
[ValueComparer.Add\(HashCode, int\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.add), 
[ValueComparer.CreateDefault\(Type, bool\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.createdefault\#microsoft\-entityframeworkcore\-changetracking\-valuecomparer\-createdefault\(system\-type\-system\-boolean\)), 
[ValueComparer.CreateDefault<T\>\(bool\)](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.createdefault\#microsoft\-entityframeworkcore\-changetracking\-valuecomparer\-createdefault\-1\(system\-boolean\)), 
[ValueComparer.Type](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.type), 
[ValueComparer.EqualsExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.equalsexpression), 
[ValueComparer.ObjectEqualsExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.objectequalsexpression), 
[ValueComparer.HashCodeExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.hashcodeexpression), 
[ValueComparer.SnapshotExpression](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.changetracking.valuecomparer.snapshotexpression), 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

This matters as soon as a value object declares a comparison other than ordinal: without it, change tracking
would consider <code>ORD-42</code> and <code>ord-42</code> different for a case-insensitive reference and issue an
UPDATE that changes nothing. A value object is immutable, so the snapshot is the value itself.

## Constructors

### ValueObjectComparer\(\) {#AdCodicem_ValueObjects_EntityFrameworkCore_ValueObjectComparer_1__ctor}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.EntityFrameworkCore.ValueObjectComparer%601" data-throw-if-not-resolved="false"></xref> class.

```csharp
public ValueObjectComparer()
```

