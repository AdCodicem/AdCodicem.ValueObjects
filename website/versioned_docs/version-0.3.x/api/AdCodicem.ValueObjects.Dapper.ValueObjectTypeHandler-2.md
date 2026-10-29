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

<p>
Reading uses the trusted factory, on the same reasoning as the Entity Framework Core converter: the rows come
from a database this application wrote through the validating factory, and read paths are hot. A column holding
text where the underlying type is not text, or the reverse, is the exception: the value object did not write it.
Text read into a value object whose underlying type is not <xref href="System.String" data-throw-if-not-resolved="false"></xref> is parsed, and so validated, the
way the value object parses text. Anything else read into a value object over <xref href="System.String" data-throw-if-not-resolved="false"></xref> - a number
from a numeric column, a <xref href="System.Guid" data-throw-if-not-resolved="false"></xref> from a <code>uuid</code> or <code>uniqueidentifier</code> one - is converted to
text, then validated through <code>TryCreate</code>.
</p>
<p>
A parameter declares the column a value object over <xref href="System.String" data-throw-if-not-resolved="false"></xref> is mapped to when the value object says
more than its underlying type, as the Entity Framework Core conventions map it: an entity identifier as fixed-length,
non-Unicode text of its exact length, a value object declaring <code>Unicode = false</code> as non-Unicode text of its
maximum length or, declaring none, of the value's own, and one declaring a maximum length alone as Unicode text of
that length. SQL Server then compares the parameter with the column as it is, with no implicit conversion of the
column to cost an index seek, and an Always Encrypted <code>varchar</code> column takes it. The size is never smaller than
the value, which both SqlClient and Npgsql would otherwise truncate.
</p>
<p>
A SQL <code>NULL</code> reads as <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> into an optional value object, <code>TSelf?</code>, and is refused
with a <xref href="System.Data.DataException" data-throw-if-not-resolved="false"></xref> for a required one in a single-column query, as Dapper refuses it for an
<xref href="System.Int32" data-throw-if-not-resolved="false"></xref>. The handler implements <xref href="Dapper.SqlMapper.ITypeHandler" data-throw-if-not-resolved="false"></xref> itself for that, since
<xref href="Dapper.SqlMapper.TypeHandler%601.Parse(System.Object)" data-throw-if-not-resolved="false"></xref> cannot return <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for a struct.
</p>
<p>
For a member of a mapped type, or a parameter of the constructor it is mapped through, Dapper checks for a
<code>NULL</code> before it calls the handler, and never calls it: a required value object is left uninitialized,
<code>default(TSelf)</code>, and nothing throws. A column that can be <code>NULL</code> belongs in a <code>TSelf?</code> member.
</p>
<p>
Every <xref href="System.Data.DataException" data-throw-if-not-resolved="false"></xref> the handler throws for a value it refuses carries the code of the rule in
<xref href="System.Exception.Data" data-throw-if-not-resolved="false"></xref> under <xref href="AdCodicem.ValueObjects.ValueObjectErrors.ErrorCodeKey" data-throw-if-not-resolved="false"></xref>, where
<xref href="AdCodicem.ValueObjects.ValueObjectErrors.TryGetCode(System.Exception%2cSystem.String%40)" data-throw-if-not-resolved="false"></xref> reads it: the rule's own code,
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable" data-throw-if-not-resolved="false"></xref> for a value that cannot be converted to
<code class="typeparamref">TValue</code>, and <xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.Required" data-throw-if-not-resolved="false"></xref> for a <code>NULL</code>.
</p>

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

#### Exceptions

 [DataException](https://learn.microsoft.com/dotnet/api/system.data.dataexception)

<code class="paramref">value</code> is a SQL <code>NULL</code>, which a value object cannot hold, a value that cannot be
converted to <code class="typeparamref">TValue</code>, or text, or a value converted to text, that the value object
refuses.

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

#### Remarks

The handler refuses to write a value the value object rejects, which a read would then trust: only an instance
equal to the default can hold one, since any other went through <code>Create</code>, and over a value type, where a
constructed zero equals the default too, validation tells a valid zero from a refused one. Dapper calls the
handler with the value object alone, whether the parameter is a <code class="typeparamref">TSelf</code> or a
<code>TSelf?</code> holding one, so it cannot tell a column that takes a <code>NULL</code> from one that does not, and
refuses in both cases: a <code>NULL</code> is written from a <code>TSelf?</code> that holds nothing.

#### Exceptions

 [DataException](https://learn.microsoft.com/dotnet/api/system.data.dataexception)

<code class="paramref">value</code> equals <code>default(TSelf)</code> and holds a value the value object rejects.

