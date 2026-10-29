# Class ValueObjectBsonSchema {#AdCodicem_ValueObjects_MongoDB_ValueObjectBsonSchema}

Namespace: [AdCodicem.ValueObjects.MongoDB](AdCodicem.ValueObjects.MongoDB.md)  
Assembly: AdCodicem.ValueObjects.MongoDB.dll  

Builds a <code>$jsonSchema</code> collection validator from the rules of the value objects a document holds, so that the
server refuses a document another writer stores with a value the type rejects.

```csharp
public static class ValueObjectBsonSchema
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectBsonSchema](AdCodicem.ValueObjects.MongoDB.ValueObjectBsonSchema.md)

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
A collection has no schema, and a strict read refuses, one document at a time, what another service, a script or
Compass stored. A validator built here makes the server refuse it on the write instead, with code 121, "Document
failed validation", naming the field and the rule:
</p>

<pre><code class="lang-csharp">await database.CreateCollectionAsync("orders", new CreateCollectionOptions&lt;BsonDocument&gt;
{
    Validator = new BsonDocumentFilterDefinition&lt;BsonDocument&gt;(ValueObjectBsonSchema.For&lt;Order&gt;()),
});</code></pre>

<p>
Each rule is carried only where the server refuses no value the type accepts, since a validator stricter than the
type would refuse the application's own writes. <code>MaxLength</code> becomes <code>maxLength</code>; <code>MinLength</code> becomes
<code>minLength</code>, halved unless the pattern keeps every value in the Basic Multilingual Plane, since the server counts
a character outside it as one where .NET counts two; the pattern behind <xref href="AdCodicem.ValueObjects.IValueObjectPatternValidator" data-throw-if-not-resolved="false"></xref>
becomes <code>pattern</code>, written in the PCRE2 dialect the server reads, a class escape listed as the characters .NET's
own Unicode tables give it, when it stays within what both engines read alike and sets no option such as
<code>IgnoreCase</code>; the bounds become <code>minimum</code> and <code>maximum</code>, written as the serializer writes a number; a
closed set becomes <code>enum</code>, unless the type stores another spelling of a known value, as a set looked up ignoring
case does; and the description becomes <code>description</code>. Rules only code checks, a checksum in
<xref href="AdCodicem.ValueObjects.IValueObjectValidator%601" data-throw-if-not-resolved="false"></xref>, stay on the read path.
</p>
<p>
The document is described as its class map writes it, element names and serialization options included: a value
object's <code>bsonType</code> is the BSON type the serializer of its underlying value writes, <code>binData</code> for a
<xref href="System.Guid" data-throw-if-not-resolved="false"></xref> under the standard representation, <code>string</code> for one given
<code>[BsonRepresentation(BsonType.String)]</code>. A value object member is <code>required</code>, unless the class map leaves it
out when it is the default or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>; a <code>TSelf?</code> is not, and adds <code>null</code> to its
<code>bsonType</code> and to its <code>enum</code>. A document nested in a member, and the elements of an array, are described
the same way, where they hold a value object. A dictionary, a member with a serializer of the application's own, and
whatever holds no value object, are left free.
</p>
<p>
The server checks an update as it checks an insert, so a validator also catches what no serializer sees, such as an
<code>Update.Inc(x =&gt; x.Quantity.Value, 1)</code> past the maximum. <code>validationLevel</code> and
<code>validationAction</code> are the application's to set, beside the validator, on <code>createCollection</code> or
<code>collMod</code>.
</p>

## Methods

### For<TDocument\>\(\) {#AdCodicem_ValueObjects_MongoDB_ValueObjectBsonSchema_For__1}

Builds <code>{ "$jsonSchema": { … } }</code> for a document type, from its class map and the rules of each value object
it holds.

```csharp
public static BsonDocument For<TDocument>()
```

#### Returns

 BsonDocument

The validator, to pass to <code>createCollection</code> or <code>collMod</code>.

#### Type Parameters

`TDocument` 

The type of the documents the collection holds.

#### Remarks

It reads the serializer the driver gives each member, and builds the class maps of the document and of the
classes nested in it, which the driver keeps for good. So call it at start-up, after
<xref href="AdCodicem.ValueObjects.MongoDB.ValueObjectBson.Register(System.Reflection.Assembly%5b%5d)" data-throw-if-not-resolved="false"></xref>, without which a value object would be given
a class map; after <code>EntityIdBson.Register</code>, whose id generators a class map takes when it is built; and after
any <code>BsonClassMap.RegisterClassMap</code> of those types, which fails once one is built.

#### Exceptions

 [InvalidOperationException](https://learn.microsoft.com/dotnet/api/system.invalidoperationexception)

<xref href="AdCodicem.ValueObjects.MongoDB.ValueObjectBson" data-throw-if-not-resolved="false"></xref> has not registered its serializers yet, or the driver serializes
<code class="typeparamref">TDocument</code> through a serializer of its own rather than a class map.

