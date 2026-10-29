# Class ValueObjectRequestExecutorBuilderExtensions {#AdCodicem_ValueObjects_HotChocolate_ValueObjectRequestExecutorBuilderExtensions}

Namespace: [AdCodicem.ValueObjects.HotChocolate](AdCodicem.ValueObjects.HotChocolate.md)  
Assembly: AdCodicem.ValueObjects.HotChocolate.dll  

Maps value objects to GraphQL scalars of their own in a HotChocolate schema, read through their rules.

```csharp
public static class ValueObjectRequestExecutorBuilderExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[ValueObjectRequestExecutorBuilderExtensions](AdCodicem.ValueObjects.HotChocolate.ValueObjectRequestExecutorBuilderExtensions.md)

#### Inherited Members

[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.MemberwiseClone\(\)](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Methods

### AddValueObjectScalar<TSelf, TValue\>\(IRequestExecutorBuilder, string?\) {#AdCodicem_ValueObjects_HotChocolate_ValueObjectRequestExecutorBuilderExtensions_AddValueObjectScalar__2_HotChocolate_Execution_Configuration_IRequestExecutorBuilder_System_String_}

Maps one value object to its scalar, closed at compile time: a construction of a generic value object, or one
written by hand, under native AOT.

```csharp
public static IRequestExecutorBuilder AddValueObjectScalar<TSelf, TValue>(this IRequestExecutorBuilder builder, string? name = null) where TSelf : struct, IValueObject<TSelf, TValue>
```

#### Parameters

`builder` IRequestExecutorBuilder

The schema's builder.

`name` [string](https://learn.microsoft.com/dotnet/api/system.string)?

The name of the scalar, a GraphQL name, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> to take the one
<xref href="AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions.Name%60%601(System.String)" data-throw-if-not-resolved="false"></xref> gives, or else the schema's naming conventions.

#### Returns

 IRequestExecutorBuilder

<code class="paramref">builder</code>, so calls can be chained.

#### Type Parameters

`TSelf` 

The value object type.

`TValue` 

Its underlying type.

#### Remarks

The scalar binds implicitly to <code class="typeparamref">TSelf</code>, so that the schema uses it wherever the type appears,
beside <xref href="AdCodicem.ValueObjects.HotChocolate.ValueObjectRequestExecutorBuilderExtensions.AddValueObjects(HotChocolate.Execution.Configuration.IRequestExecutorBuilder%2cSystem.Action%7bAdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions%7d)" data-throw-if-not-resolved="false"></xref> or without it. It needs no registration of the value object: its rules are
read through <code class="typeparamref">TSelf</code>, never through the registry.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">builder</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">name</code> is empty or white space.

### AddValueObjects\(IRequestExecutorBuilder, Action<ValueObjectScalarOptions\>?\) {#AdCodicem_ValueObjects_HotChocolate_ValueObjectRequestExecutorBuilderExtensions_AddValueObjects_HotChocolate_Execution_Configuration_IRequestExecutorBuilder_System_Action_AdCodicem_ValueObjects_HotChocolate_ValueObjectScalarOptions__}

Maps every value object the schema meets to a scalar of its own, which reads a value through the type's rules.

```csharp
public static IRequestExecutorBuilder AddValueObjects(this IRequestExecutorBuilder builder, Action<ValueObjectScalarOptions>? configure = null)
```

#### Parameters

`builder` IRequestExecutorBuilder

The schema's builder, from <code>AddGraphQL()</code> or <code>AddGraphQLServer()</code>.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action\-1)<[ValueObjectScalarOptions](AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions.md)\>?

Names, specifications and messages of the scalars, or <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> for none.

#### Returns

 IRequestExecutorBuilder

<code class="paramref">builder</code>, so calls can be chained.

#### Remarks

<p>
A type discovery handler, asked before HotChocolate's own, answers each value object the schema references, as an
argument, an input field, a field or the element of a list, with a <xref href="AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarType%602" data-throw-if-not-resolved="false"></xref>
closed over it through the descriptor's visitor. A <code>TSelf?</code> is the nullable scalar. A value object the registry
holds is closed at compile time; one it does not hold yet, a construction of a generic value object or a value
object written by hand, is described by <code>ValueObjectRegistry.TryResolve</code> where the runtime supports dynamic
code, and refused with a <xref href="System.NotSupportedException" data-throw-if-not-resolved="false"></xref> naming <code>ValueObjectRegistry.Register</code> and
<xref href="AdCodicem.ValueObjects.HotChocolate.ValueObjectRequestExecutorBuilderExtensions.AddValueObjectScalar%60%602(HotChocolate.Execution.Configuration.IRequestExecutorBuilder%2cSystem.String)" data-throw-if-not-resolved="false"></xref> where it does not, as under native AOT.
</p>
<p>
Every call on one schema configures the same <xref href="AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions" data-throw-if-not-resolved="false"></xref>, in the order of the calls, and
the schema gets one handler. A scalar <xref href="AdCodicem.ValueObjects.HotChocolate.ValueObjectRequestExecutorBuilderExtensions.AddValueObjectScalar%60%602(HotChocolate.Execution.Configuration.IRequestExecutorBuilder%2cSystem.String)" data-throw-if-not-resolved="false"></xref> adds wins for its type.
</p>
<p>
The first call also adds an error filter, which localizes a refusal under
<xref href="AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions.UseResource%60%601" data-throw-if-not-resolved="false"></xref>, quoting the scalar's name for a value refused in an
argument or an input field <xref href="AdCodicem.ValueObjects.HotChocolate.ValueObjectScalarOptions.QuoteScalarNameIn(System.Func%7bHotChocolate.Types.IInputValueDefinition%2cSystem.Boolean%7d)" data-throw-if-not-resolved="false"></xref> selects.
</p>

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

<code class="paramref">builder</code> is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

