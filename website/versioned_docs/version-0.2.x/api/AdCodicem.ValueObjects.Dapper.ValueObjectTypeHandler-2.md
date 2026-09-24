# Class ValueObjectTypeHandler<TSelf, TValue\> {#AdCodicem_ValueObjects_Dapper_ValueObjectTypeHandler_2}

Namespace: [AdCodicem.ValueObjects.Dapper](AdCodicem.ValueObjects.Dapper.md)  
Assembly: AdCodicem.ValueObjects.Dapper.dll  

Maps a value object to and from its underlying column value for Dapper.

```csharp
public sealed class ValueObjectTypeHandler<TSelf, TValue> : SqlMapper.TypeHandler<TSelf>, SqlMapper.ITypeHandler where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Type Parameters

`TSelf` 

Value object type.

`TValue` 

Underlying value type.

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
SqlMapper.TypeHandler<TSelf\> ← 
[ValueObjectTypeHandler<TSelf, TValue\>](AdCodicem.ValueObjects.Dapper.ValueObjectTypeHandler\-2.md)

#### Implements

SqlMapper.ITypeHandler

#### Inherited Members

SqlMapper.TypeHandler<TSelf\>.SetValue\(IDbDataParameter, TSelf\), 
SqlMapper.TypeHandler<TSelf\>.Parse\(object\), 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

Reading uses the trusted factory, on the same reasoning as the Entity Framework Core converter: the rows come
from a database this application wrote through the validating factory, and read paths are hot.

## Methods

### Parse\(object\) {#AdCodicem_ValueObjects_Dapper_ValueObjectTypeHandler_2_Parse_System_Object_}

Parse a database value back to a typed value

```csharp
public override TSelf Parse(object value)
```

#### Parameters

`value` [object](https://learn.microsoft.com/dotnet/api/system.object)

The value from the database

#### Returns

 TSelf

The typed value

### SetValue\(IDbDataParameter, TSelf\) {#AdCodicem_ValueObjects_Dapper_ValueObjectTypeHandler_2_SetValue_System_Data_IDbDataParameter__0_}

Assign the value of a parameter before a command executes

```csharp
public override void SetValue(IDbDataParameter parameter, TSelf value)
```

#### Parameters

`parameter` [IDbDataParameter](https://learn.microsoft.com/dotnet/api/system.data.idbdataparameter)

The parameter to configure

`value` TSelf

Parameter value

