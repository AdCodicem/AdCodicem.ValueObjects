# Interface IValueObjectExample<TSelf\> {#AdCodicem_ValueObjects_IValueObjectExample_1}

Namespace: [AdCodicem.ValueObjects](AdCodicem.ValueObjects.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Declares the example of a value object, which its OpenAPI and JSON schemas publish.

```csharp
public interface IValueObjectExample<TSelf>
```

#### Type Parameters

`TSelf` 

The value object itself.

## Remarks

<p>
The example is an instance of the value object, so it went through the type's own rules, and it can be one of its
known values: <code>public static CountryCode Example =&gt; France;</code>. An explicit implementation,
<code>static CountryCode IValueObjectExample&lt;CountryCode&gt;.Example =&gt; France;</code>, keeps it off the public
surface of the type. The schema holds its underlying value, which the schemas write as the type writes it in JSON.
</p>
<p>
The schema reads it once, when the value object's type initializes, which the generated registration does as the
declaring assembly loads: an example must neither throw nor depend on the state of the application, and one the
type's rules refuse stops the application before it starts. The generator refuses it at compile time when the getter
returns <code>Create</code> over a constant it can evaluate, and the contract kit of <code>AdCodicem.ValueObjects.Testing</code>
checks it at run time.
</p>
<p>
An entity identifier that does not implement it publishes an identifier of the right shape, built from its profile.
Implementing it over another type than the value object is a build error.
</p>

## Properties

### Example {#AdCodicem_ValueObjects_IValueObjectExample_1_Example}

Gets the example the schemas publish.

```csharp
public static abstract TSelf Example { get; }
```

#### Property Value

 TSelf

