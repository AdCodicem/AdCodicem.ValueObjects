# Class EntityIdBson {#AdCodicem_ValueObjects_Identifiers_MongoDB_EntityIdBson}

Namespace: [AdCodicem.ValueObjects.Identifiers.MongoDB](AdCodicem.ValueObjects.Identifiers.MongoDB.md)  
Assembly: AdCodicem.ValueObjects.Identifiers.MongoDB.dll  

Registers the id generators of entity identifiers with MongoDB.Driver, so that a document inserted without its
identifier is given a new one.

```csharp
public static class EntityIdBson
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[EntityIdBson](AdCodicem.ValueObjects.Identifiers.MongoDB.EntityIdBson.md)

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
Call it at start-up, beside <code>ValueObjectBson.Register</code>, which the identifier's serializer still comes from,
and before any class map of a document holding an identifier is built: the driver gives a class map's id member the
generator registered for its type when it builds the class map, and never looks again.
</p>

<pre><code class="lang-csharp">BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
ValueObjectBson.Register(typeof(AccountId).Assembly);
EntityIdBson.Register(typeof(AccountId).Assembly);

await accounts.InsertOneAsync(new Account { Name = "Ada" }); // account.Id is now acc_…</code></pre>

## Methods

### Register\(params Assembly\[\]\) {#AdCodicem_ValueObjects_Identifiers_MongoDB_EntityIdBson_Register_System_Reflection_Assembly___}

Registers the generator of every entity identifier registered, those the assemblies declare included, unless
the driver holds one for it already.

```csharp
[RequiresUnreferencedCode("Locates the generated registration of each assembly given by its metadata.")]
public static void Register(params Assembly[] assemblies)
```

#### Parameters

`assemblies` [Assembly](https://learn.microsoft.com/dotnet/api/system.reflection.assembly)\[\]

Assemblies declaring entity identifiers, whose generated registration runs first, so that their identifiers are
registered.

#### Remarks

Each generator is closed over its identifier at compile time, through the type the descriptor hands back to a
visitor (<xref href="AdCodicem.ValueObjects.Identifiers.EntityIdDescriptor.Accept%60%601(AdCodicem.ValueObjects.Identifiers.IEntityIdVisitor%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>). A generator of the
application's own, registered first, is kept; the checker the driver gives a type that has none, under
<xref href="MongoDB.Bson.Serialization.BsonSerializer.UseZeroIdChecker" data-throw-if-not-resolved="false"></xref> or <xref href="MongoDB.Bson.Serialization.BsonSerializer.UseNullIdChecker" data-throw-if-not-resolved="false"></xref>, is not one.
Locating the generated registration of an assembly given by name reads its metadata, which trimming may remove.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">assemblies</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

### Register<TId\>\(\) {#AdCodicem_ValueObjects_Identifiers_MongoDB_EntityIdBson_Register__1}

Registers the generator of one entity identifier, unless the driver holds one for it already.

```csharp
public static void Register<TId>() where TId : struct, IEntityId<TId>
```

#### Type Parameters

`TId` 

The identifier type.

#### Remarks

A second call does nothing, and a generator of the application's own, registered first, is kept. The checker the
driver gives a type that has no generator, under <xref href="MongoDB.Bson.Serialization.BsonSerializer.UseZeroIdChecker" data-throw-if-not-resolved="false"></xref> or
<xref href="MongoDB.Bson.Serialization.BsonSerializer.UseNullIdChecker" data-throw-if-not-resolved="false"></xref>, is not one of the application's: it would refuse, or let through,
the default instance rather than mint an identifier, so it is replaced. Closed at compile time, the registration
needs no reflection.

