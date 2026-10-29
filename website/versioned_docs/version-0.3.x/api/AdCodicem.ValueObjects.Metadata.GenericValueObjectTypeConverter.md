# Class GenericValueObjectTypeConverter {#AdCodicem_ValueObjects_Metadata_GenericValueObjectTypeConverter}

Namespace: [AdCodicem.ValueObjects.Metadata](AdCodicem.ValueObjects.Metadata.md)  
Assembly: AdCodicem.ValueObjects.Abstractions.dll  

Converts each construction of a generic value object to and from text and its underlying value.

```csharp
public sealed class GenericValueObjectTypeConverter : TypeConverter
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ← 
[TypeConverter](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter) ← 
[GenericValueObjectTypeConverter](AdCodicem.ValueObjects.Metadata.GenericValueObjectTypeConverter.md)

#### Inherited Members

[TypeConverter.CanConvertFrom\(ITypeDescriptorContext?, Type\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.canconvertfrom\#system\-componentmodel\-typeconverter\-canconvertfrom\(system\-componentmodel\-itypedescriptorcontext\-system\-type\)), 
[TypeConverter.CanConvertFrom\(Type\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.canconvertfrom\#system\-componentmodel\-typeconverter\-canconvertfrom\(system\-type\)), 
[TypeConverter.CanConvertTo\(ITypeDescriptorContext?, Type?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.canconvertto\#system\-componentmodel\-typeconverter\-canconvertto\(system\-componentmodel\-itypedescriptorcontext\-system\-type\)), 
[TypeConverter.CanConvertTo\(Type?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.canconvertto\#system\-componentmodel\-typeconverter\-canconvertto\(system\-type\)), 
[TypeConverter.ConvertFrom\(ITypeDescriptorContext?, CultureInfo?, object\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.convertfrom\#system\-componentmodel\-typeconverter\-convertfrom\(system\-componentmodel\-itypedescriptorcontext\-system\-globalization\-cultureinfo\-system\-object\)), 
[TypeConverter.ConvertFrom\(object\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.convertfrom\#system\-componentmodel\-typeconverter\-convertfrom\(system\-object\)), 
[TypeConverter.ConvertFromInvariantString\(ITypeDescriptorContext?, string\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.convertfrominvariantstring\#system\-componentmodel\-typeconverter\-convertfrominvariantstring\(system\-componentmodel\-itypedescriptorcontext\-system\-string\)), 
[TypeConverter.ConvertFromInvariantString\(string\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.convertfrominvariantstring\#system\-componentmodel\-typeconverter\-convertfrominvariantstring\(system\-string\)), 
[TypeConverter.ConvertFromString\(ITypeDescriptorContext?, CultureInfo?, string\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.convertfromstring\#system\-componentmodel\-typeconverter\-convertfromstring\(system\-componentmodel\-itypedescriptorcontext\-system\-globalization\-cultureinfo\-system\-string\)), 
[TypeConverter.ConvertFromString\(ITypeDescriptorContext?, string\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.convertfromstring\#system\-componentmodel\-typeconverter\-convertfromstring\(system\-componentmodel\-itypedescriptorcontext\-system\-string\)), 
[TypeConverter.ConvertFromString\(string\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.convertfromstring\#system\-componentmodel\-typeconverter\-convertfromstring\(system\-string\)), 
[TypeConverter.ConvertTo\(ITypeDescriptorContext?, CultureInfo?, object?, Type\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.convertto\#system\-componentmodel\-typeconverter\-convertto\(system\-componentmodel\-itypedescriptorcontext\-system\-globalization\-cultureinfo\-system\-object\-system\-type\)), 
[TypeConverter.ConvertTo\(object?, Type\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.convertto\#system\-componentmodel\-typeconverter\-convertto\(system\-object\-system\-type\)), 
[TypeConverter.ConvertToInvariantString\(ITypeDescriptorContext?, object?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.converttoinvariantstring\#system\-componentmodel\-typeconverter\-converttoinvariantstring\(system\-componentmodel\-itypedescriptorcontext\-system\-object\)), 
[TypeConverter.ConvertToInvariantString\(object?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.converttoinvariantstring\#system\-componentmodel\-typeconverter\-converttoinvariantstring\(system\-object\)), 
[TypeConverter.ConvertToString\(ITypeDescriptorContext?, CultureInfo?, object?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.converttostring\#system\-componentmodel\-typeconverter\-converttostring\(system\-componentmodel\-itypedescriptorcontext\-system\-globalization\-cultureinfo\-system\-object\)), 
[TypeConverter.ConvertToString\(ITypeDescriptorContext?, object?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.converttostring\#system\-componentmodel\-typeconverter\-converttostring\(system\-componentmodel\-itypedescriptorcontext\-system\-object\)), 
[TypeConverter.ConvertToString\(object?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.converttostring\#system\-componentmodel\-typeconverter\-converttostring\(system\-object\)), 
[TypeConverter.CreateInstance\(IDictionary\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.createinstance\#system\-componentmodel\-typeconverter\-createinstance\(system\-collections\-idictionary\)), 
[TypeConverter.CreateInstance\(ITypeDescriptorContext?, IDictionary\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.createinstance\#system\-componentmodel\-typeconverter\-createinstance\(system\-componentmodel\-itypedescriptorcontext\-system\-collections\-idictionary\)), 
[TypeConverter.GetCreateInstanceSupported\(\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.getcreateinstancesupported\#system\-componentmodel\-typeconverter\-getcreateinstancesupported), 
[TypeConverter.GetCreateInstanceSupported\(ITypeDescriptorContext?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.getcreateinstancesupported\#system\-componentmodel\-typeconverter\-getcreateinstancesupported\(system\-componentmodel\-itypedescriptorcontext\)), 
[TypeConverter.GetProperties\(ITypeDescriptorContext?, object\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.getproperties\#system\-componentmodel\-typeconverter\-getproperties\(system\-componentmodel\-itypedescriptorcontext\-system\-object\)), 
[TypeConverter.GetProperties\(ITypeDescriptorContext?, object, Attribute\[\]?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.getproperties\#system\-componentmodel\-typeconverter\-getproperties\(system\-componentmodel\-itypedescriptorcontext\-system\-object\-system\-attribute\(\)\)), 
[TypeConverter.GetProperties\(object\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.getproperties\#system\-componentmodel\-typeconverter\-getproperties\(system\-object\)), 
[TypeConverter.GetPropertiesSupported\(\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.getpropertiessupported\#system\-componentmodel\-typeconverter\-getpropertiessupported), 
[TypeConverter.GetPropertiesSupported\(ITypeDescriptorContext?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.getpropertiessupported\#system\-componentmodel\-typeconverter\-getpropertiessupported\(system\-componentmodel\-itypedescriptorcontext\)), 
[TypeConverter.GetStandardValues\(\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.getstandardvalues\#system\-componentmodel\-typeconverter\-getstandardvalues), 
[TypeConverter.GetStandardValues\(ITypeDescriptorContext?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.getstandardvalues\#system\-componentmodel\-typeconverter\-getstandardvalues\(system\-componentmodel\-itypedescriptorcontext\)), 
[TypeConverter.GetStandardValuesExclusive\(\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.getstandardvaluesexclusive\#system\-componentmodel\-typeconverter\-getstandardvaluesexclusive), 
[TypeConverter.GetStandardValuesExclusive\(ITypeDescriptorContext?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.getstandardvaluesexclusive\#system\-componentmodel\-typeconverter\-getstandardvaluesexclusive\(system\-componentmodel\-itypedescriptorcontext\)), 
[TypeConverter.GetStandardValuesSupported\(\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.getstandardvaluessupported\#system\-componentmodel\-typeconverter\-getstandardvaluessupported), 
[TypeConverter.GetStandardValuesSupported\(ITypeDescriptorContext?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.getstandardvaluessupported\#system\-componentmodel\-typeconverter\-getstandardvaluessupported\(system\-componentmodel\-itypedescriptorcontext\)), 
[TypeConverter.IsValid\(ITypeDescriptorContext?, object?\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.isvalid\#system\-componentmodel\-typeconverter\-isvalid\(system\-componentmodel\-itypedescriptorcontext\-system\-object\)), 
[TypeConverter.IsValid\(object\)](https://learn.microsoft.com/dotnet/api/system.componentmodel.typeconverter.isvalid\#system\-componentmodel\-typeconverter\-isvalid\(system\-object\)), 
[object.Equals\(object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\)), 
[object.Equals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.equals\#system\-object\-equals\(system\-object\-system\-object\)), 
[object.GetHashCode\(\)](https://learn.microsoft.com/dotnet/api/system.object.gethashcode), 
[object.GetType\(\)](https://learn.microsoft.com/dotnet/api/system.object.gettype), 
[object.ReferenceEquals\(object?, object?\)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals), 
[object.ToString\(\)](https://learn.microsoft.com/dotnet/api/system.object.tostring)

## Remarks

<p>
The generator puts this converter in the <code>[TypeConverter]</code> attribute of a generic value object, where a value
object that is not generic names its own converter: an attribute cannot name a type through a type parameter.
<xref href="System.ComponentModel.TypeDescriptor" data-throw-if-not-resolved="false"></xref> hands it the construction it converts, and it converts through the construction's
descriptor, exactly as the converter the generator writes on a value object that is not generic does.
</p>
<p>
It closes nothing at run time. A construction registered with
<xref href="AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.Register%60%602(AdCodicem.ValueObjects.Metadata.ValueObjectSchema%2cSystem.Text.Json.Serialization.JsonConverter%7b%60%600%7d)" data-throw-if-not-resolved="false"></xref>,
as native AOT requires, is found without reflection; any other is described by the registry the first time it is
asked for it.
</p>
<p>
Over a number, it converts from and to every numeric type a value object may wrap, with the checked conversion the
converter the generator writes performs: a number the underlying type cannot hold whole is refused as
<xref href="AdCodicem.ValueObjects.ValueObjectErrorCodes.NotParsable" data-throw-if-not-resolved="false"></xref>, and a numeric type that cannot hold the value whole is refused with
<xref href="System.NotSupportedException" data-throw-if-not-resolved="false"></xref>. Nothing is ever truncated.
</p>
<p>
Text or a number it refuses is never quoted in the message of the <xref href="AdCodicem.ValueObjects.ValueObjectException" data-throw-if-not-resolved="false"></xref> it throws, and
is left out of its <xref href="AdCodicem.ValueObjects.ValueObjectException.AttemptedValue" data-throw-if-not-resolved="false"></xref> too when the generic definition carries an
attribute derived from <code>Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute</code>, other than
<code>NoDataClassificationAttribute</code>, as the <code>Parse</code> the generator writes does.
</p>

## Constructors

### GenericValueObjectTypeConverter\(Type\) {#AdCodicem_ValueObjects_Metadata_GenericValueObjectTypeConverter__ctor_System_Type_}

Initializes a new instance of the <xref href="AdCodicem.ValueObjects.Metadata.GenericValueObjectTypeConverter" data-throw-if-not-resolved="false"></xref> class for a construction.

```csharp
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "A construction registered by hand, as native AOT requires, is found without reflection. Any other is described by the registry by reflection, which TypeDescriptor, itself reflection-based, already depends on.")]
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "A construction registered by hand, as native AOT requires, is found without reflection. Any other is described by the registry by reflection, which TypeDescriptor, itself reflection-based, already depends on.")]
public GenericValueObjectTypeConverter(Type type)
```

#### Parameters

`type` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The construction, <code>Code&lt;Order&gt;</code>, as <xref href="System.ComponentModel.TypeDescriptor" data-throw-if-not-resolved="false"></xref> hands it over.

#### Exceptions

 [ArgumentException](https://learn.microsoft.com/dotnet/api/system.argumentexception)

<code class="paramref">type</code> is not a value object.

## Methods

### CanConvertFrom\(ITypeDescriptorContext?, Type\) {#AdCodicem_ValueObjects_Metadata_GenericValueObjectTypeConverter_CanConvertFrom_System_ComponentModel_ITypeDescriptorContext_System_Type_}

Returns whether this converter can convert an object of the given type to the type of this converter, using the specified context.

```csharp
public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
```

#### Parameters

`context` [ITypeDescriptorContext](https://learn.microsoft.com/dotnet/api/system.componentmodel.itypedescriptorcontext)?

An <xref href="System.ComponentModel.ITypeDescriptorContext" data-throw-if-not-resolved="false"></xref> that provides a format context.

`sourceType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

A <xref href="System.Type" data-throw-if-not-resolved="false"></xref> that represents the type you want to convert from.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> if this converter can perform the conversion; otherwise, <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>.

### CanConvertTo\(ITypeDescriptorContext?, Type?\) {#AdCodicem_ValueObjects_Metadata_GenericValueObjectTypeConverter_CanConvertTo_System_ComponentModel_ITypeDescriptorContext_System_Type_}

Returns whether this converter can convert the object to the specified type, using the specified context.

```csharp
public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
```

#### Parameters

`context` [ITypeDescriptorContext](https://learn.microsoft.com/dotnet/api/system.componentmodel.itypedescriptorcontext)?

An <xref href="System.ComponentModel.ITypeDescriptorContext" data-throw-if-not-resolved="false"></xref> that provides a format context.

`destinationType` [Type](https://learn.microsoft.com/dotnet/api/system.type)?

A <xref href="System.Type" data-throw-if-not-resolved="false"></xref> that represents the type you want to convert to.

#### Returns

 [bool](https://learn.microsoft.com/dotnet/api/system.boolean)

<a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">true</a> if this converter can perform the conversion; otherwise, <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool">false</a>.

### ConvertFrom\(ITypeDescriptorContext?, CultureInfo?, object\) {#AdCodicem_ValueObjects_Metadata_GenericValueObjectTypeConverter_ConvertFrom_System_ComponentModel_ITypeDescriptorContext_System_Globalization_CultureInfo_System_Object_}

Converts the given object to the type of this converter, using the specified context and culture information.

```csharp
public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
```

#### Parameters

`context` [ITypeDescriptorContext](https://learn.microsoft.com/dotnet/api/system.componentmodel.itypedescriptorcontext)?

An <xref href="System.ComponentModel.ITypeDescriptorContext" data-throw-if-not-resolved="false"></xref> that provides a format context.

`culture` [CultureInfo](https://learn.microsoft.com/dotnet/api/system.globalization.cultureinfo)?

The <xref href="System.Globalization.CultureInfo" data-throw-if-not-resolved="false"></xref> to use as the current culture.

`value` [object](https://learn.microsoft.com/dotnet/api/system.object)

The <xref href="System.Object" data-throw-if-not-resolved="false"></xref> to convert.

#### Returns

 [object](https://learn.microsoft.com/dotnet/api/system.object)?

An <xref href="System.Object" data-throw-if-not-resolved="false"></xref> that represents the converted value.

#### Exceptions

 [NotSupportedException](https://learn.microsoft.com/dotnet/api/system.notsupportedexception)

The conversion cannot be performed.

 [ValueObjectException](AdCodicem.ValueObjects.ValueObjectException.md)

The text or the value is not one the value object accepts.

### ConvertTo\(ITypeDescriptorContext?, CultureInfo?, object?, Type\) {#AdCodicem_ValueObjects_Metadata_GenericValueObjectTypeConverter_ConvertTo_System_ComponentModel_ITypeDescriptorContext_System_Globalization_CultureInfo_System_Object_System_Type_}

Converts the given value object to the specified type, using the specified context and culture information.

```csharp
public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
```

#### Parameters

`context` [ITypeDescriptorContext](https://learn.microsoft.com/dotnet/api/system.componentmodel.itypedescriptorcontext)?

An <xref href="System.ComponentModel.ITypeDescriptorContext" data-throw-if-not-resolved="false"></xref> that provides a format context.

`culture` [CultureInfo](https://learn.microsoft.com/dotnet/api/system.globalization.cultureinfo)?

A <xref href="System.Globalization.CultureInfo" data-throw-if-not-resolved="false"></xref>. If <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a> is passed, the current culture is assumed.

`value` [object](https://learn.microsoft.com/dotnet/api/system.object)?

The <xref href="System.Object" data-throw-if-not-resolved="false"></xref> to convert.

`destinationType` [Type](https://learn.microsoft.com/dotnet/api/system.type)

The <xref href="System.Type" data-throw-if-not-resolved="false"></xref> to convert the <code class="paramref">value</code> parameter to.

#### Returns

 [object](https://learn.microsoft.com/dotnet/api/system.object)?

An <xref href="System.Object" data-throw-if-not-resolved="false"></xref> that represents the converted value.

#### Exceptions

 [ArgumentNullException](https://learn.microsoft.com/dotnet/api/system.argumentnullexception)

The <code class="paramref">destinationType</code> parameter is <a href="https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null">null</a>.

 [NotSupportedException](https://learn.microsoft.com/dotnet/api/system.notsupportedexception)

The conversion cannot be performed.

 [NotSupportedException](https://learn.microsoft.com/dotnet/api/system.notsupportedexception)

The destination is not a type the converter writes, or is a numeric type that cannot hold the value whole.

