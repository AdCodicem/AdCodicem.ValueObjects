using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Schema;

namespace AdCodicem.ValueObjects.Metadata;

/// <summary>
/// Reads, writes and describes a value object in XML, for the <c>IXmlSerializable</c> implementation the generator emits
/// on the value objects of an assembly marked <c>[assembly: ValueObjectXmlSerialization]</c>.
/// </summary>
/// <remarks>
/// <para>
/// A value object is written as the content of its element, in the form <c>XmlSerializer</c> writes its underlying type
/// in, and read in the form <c>XmlSerializer</c> reads that type in, then through its rules: it reads every document
/// written when the member was the primitive it replaces. A 128-bit integer, which <c>XmlSerializer</c> writes as an
/// empty element, is written as its digits.
/// </para>
/// <para>
/// A refusal is an <see cref="XmlException"/> naming the type and the rule, never the text, which carries the code of the
/// rule under <see cref="ValueObjectErrors.ErrorCodeKey"/>: <see cref="ValueObjectErrorCodes.Required"/> for an element
/// marked <c>xsi:nil</c>, <see cref="ValueObjectErrorCodes.NotParsable"/> for text that is not of the underlying type, or
/// an element holding elements, the code of the rule otherwise.
/// </para>
/// <para>
/// A value object written by hand implements <c>IXmlSerializable</c> through these methods too, as long as its
/// underlying type is one the generator supports.
/// </para>
/// </remarks>
public static class ValueObjectXml
{
    /// <summary>The namespace <c>DataContractSerializer</c> puts a CLR namespace under.</summary>
    private static readonly Uri ContractNamespace = new("http://schemas.datacontract.org/2004/07/");

    /// <summary>
    /// The value object each simple type this class adds to a schema set describes, and its rules, which tell a second
    /// value object of the same name and namespace sharing that type from one it would misdescribe.
    /// </summary>
    private static readonly ConditionalWeakTable<XmlSchemaSimpleType, Described> Owners = [];

    /// <summary>The lexical form <c>DataContractSerializer</c> holds a <see cref="Guid"/> to.</summary>
    private const string GuidPattern = @"[\da-fA-F]{8}-[\da-fA-F]{4}-[\da-fA-F]{4}-[\da-fA-F]{4}-[\da-fA-F]{12}";

    /// <summary>
    /// Reads the element the reader is positioned on as a value object: its text, in the lexical form of the underlying
    /// type, normalized and validated.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="reader">The reader, on the start tag of the element; past its end tag on return.</param>
    /// <returns>The value object.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    /// <exception cref="XmlException">
    /// The element is marked <c>xsi:nil</c>, its text is not of the underlying type, or the type refuses it.
    /// </exception>
    /// <exception cref="NotSupportedException"><typeparamref name="TValue"/> is not an underlying type the generator supports.</exception>
    public static TSelf Read<TSelf, TValue>(XmlReader reader)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(reader);

        var (line, position) = reader is IXmlLineInfo info && info.HasLineInfo() ? (info.LineNumber, info.LinePosition) : (0, 0);
        bool nil;
        string text;
        try
        {
            nil = IsNil(reader.GetAttribute("nil", XmlSchema.InstanceNamespace));
            text = reader.ReadElementContentAsString();
        }
        catch (XmlException exception)
        {
            // An element holding other elements, as DataContractSerializer writes a bare DateTimeOffset, holds no value.
            throw Refusal(
                ValueObjectErrorCodes.NotParsable,
                $"The value is not a valid {NameOf(typeof(TSelf))}: the element holds no text.",
                line,
                position,
                exception);
        }

        // XmlSerializer hands the type an element marked nil when its member cannot be null: a null, as anywhere else.
        if (nil)
        {
            throw Refusal(
                ValueObjectErrorCodes.Required,
                $"The value is not a valid {NameOf(typeof(TSelf))}: the element is nil.",
                line,
                position);
        }

        if (!XmlLexical.TryParse(text, out TValue? raw))
        {
            throw Refusal(
                ValueObjectErrorCodes.NotParsable,
                $"The value is not a valid {NameOf(typeof(TSelf))}: the text is not a valid {XmlLexical.Lexical<TValue>()}.",
                line,
                position);
        }

        if (!TSelf.TryCreate(raw!, out var result, out var validation))
        {
            throw Refusal(
                validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable,
                $"The value is not a valid {NameOf(typeof(TSelf))}: {validation.ErrorMessage}",
                line,
                position);
        }

        return result;
    }

    /// <summary>
    /// Writes a value object as the content of the current element, in the lexical form of its underlying type.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="writer">The writer, inside the element.</param>
    /// <param name="value">The value object.</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    /// <exception cref="XmlException">The instance is the default, and its type refuses the default's value.</exception>
    /// <exception cref="NotSupportedException"><typeparamref name="TValue"/> is not an underlying type the generator supports.</exception>
    public static void Write<TSelf, TValue>(XmlWriter writer, TSelf value)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(writer);

        var current = value.Value;
        if (value.IsDefault)
        {
            var validation = TSelf.Validate(in current);
            if (!validation.IsValid)
            {
                throw Refusal(
                    validation.ErrorCode,
                    $"The value to write is not a valid {NameOf(typeof(TSelf))}: {validation.ErrorMessage}",
                    0,
                    0);
            }
        }

        writer.WriteString(XmlLexical.Format(current));
    }

    /// <summary>
    /// Adds the <c>xs:simpleType</c> describing a value object to a schema set, once, and names it.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="schemas">The schema set an exporter or a serializer hands the schema provider.</param>
    /// <param name="xmlNamespace">
    /// The namespace of the type, or <see langword="null"/> for the one <c>DataContractSerializer</c> derives from its CLR
    /// namespace.
    /// </param>
    /// <returns>The qualified name of the type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="schemas"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The schema set holds a type of that name and namespace describing another value object, with other rules.
    /// </exception>
    /// <exception cref="NotSupportedException"><typeparamref name="TValue"/> is not an underlying type the generator supports.</exception>
    /// <remarks>
    /// <para>
    /// The type restricts the XSD type of the underlying value with the rules of <c>TSelf.Schema</c>: the lengths as
    /// <c>minLength</c> and <c>maxLength</c>, the pattern as <c>pattern</c> when XSD can say what it means, the bounds as
    /// <c>minInclusive</c> and <c>maxInclusive</c>, the known values of a closed set as <c>enumeration</c> and the
    /// description as documentation. A facet System.Xml cannot read, such as a bound of a 128-bit integer beyond what a
    /// <see cref="decimal"/> holds, is left out, since <c>XmlSerializer</c> would refuse the whole type for it.
    /// </para>
    /// <para>
    /// The type is named after the value object, after the types it is nested in and joined to them with a dot, and,
    /// for a construction of a generic value object, followed by <c>Of</c> and the names of its type arguments. Its name
    /// leaves out the CLR namespace, which the default XML namespace carries: two value objects of one name put under
    /// one XML namespace share one type when their rules agree, and are refused when they differ, since the type would
    /// describe the second with the rules of the first.
    /// </para>
    /// </remarks>
    public static XmlQualifiedName ProvideSchema<TSelf, TValue>(XmlSchemaSet schemas, string? xmlNamespace)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(schemas);

        // As DataContractSerializer derives it: the CLR namespace resolved against its base, so escaped as a URI is.
        var name = new XmlQualifiedName(
            XmlConvert.EncodeLocalName(LocalName(typeof(TSelf))),
            xmlNamespace ?? new Uri(ContractNamespace, typeof(TSelf).Namespace).AbsoluteUri);

        var declared = schemas.Schemas(name.Namespace).Cast<XmlSchema>()
            .SelectMany(static schema => schema.Items.OfType<XmlSchemaSimpleType>())
            .FirstOrDefault(type => type.Name == name.Name);
        if (declared is not null)
        {
            // A type this class did not add stands as it is. One it added for another value object describes this one
            // too only when their rules agree: one name and namespace with two meanings would misdescribe the second.
            return !Owners.TryGetValue(declared, out var owner) || owner.Type == typeof(TSelf)
                || owner.Rules == Rules(Describe<TSelf, TValue>(name.Name), TSelf.Schema.Description)
                ? name
                : throw new InvalidOperationException(
                    $"The XML schema type '{name.Name}' of namespace '{name.Namespace}' describes '{owner.Type}', whose rules "
                    + $"differ from those of '{typeof(TSelf)}': give the value objects distinct names, or leave the Namespace "
                    + "of [assembly: ValueObjectXmlSerialization] unset, so that each CLR namespace keeps an XML namespace of its own.");
        }

        var described = Describe<TSelf, TValue>(name.Name);
        schemas.Add(new XmlSchema
        {
            TargetNamespace = name.Namespace,
            ElementFormDefault = XmlSchemaForm.Qualified,
            Items = { described },
        });
        Owners.Add(described, new Described(typeof(TSelf), Rules(described, TSelf.Schema.Description)));

        return name;
    }

    /// <summary>
    /// Describes a value object by the rules of its <c>Schema</c>, or by its description alone when they do not compile
    /// together: a schema written by hand may hold facets that each read, yet contradict each other, a <c>MinLength</c>
    /// above its <c>MaxLength</c>, for which System.Xml would refuse the whole type, and every serializer over a type
    /// holding it.
    /// </summary>
    private static XmlSchemaSimpleType Describe<TSelf, TValue>(string name)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => Compiles(Describe<TValue>(name, TSelf.Schema))
            ? Describe<TValue>(name, TSelf.Schema)
            : Describe<TValue>(name, ValueObjectSchema.Unconstrained with { Description = TSelf.Schema.Description });

    /// <summary>Gets what a simple type this class wrote says: its base type, its facets and its description.</summary>
    private static string Rules(XmlSchemaSimpleType type, string? description)
    {
        var restriction = (XmlSchemaSimpleTypeRestriction)type.Content!;
        return string.Join(
            '\n',
            [
                restriction.BaseTypeName.Name,
                .. restriction.Facets.Cast<XmlSchemaFacet>().Select(static facet => $"{facet.GetType().Name}={facet.Value}"),
                description,
            ]);
    }

    private static XmlSchemaSimpleType Describe<TValue>(string name, ValueObjectSchema declared)
    {
        var baseName = new XmlQualifiedName(XmlLexical.BaseType<TValue>(), XmlSchema.Namespace);
        var datatype = XmlSchemaType.GetBuiltInSimpleType(baseName)!.Datatype!;
        var restriction = new XmlSchemaSimpleTypeRestriction { BaseTypeName = baseName };

        if (typeof(TValue) == typeof(string))
        {
            if (declared.MinLength is { } minimum)
            {
                restriction.Facets.Add(new XmlSchemaMinLengthFacet { Value = XmlConvert.ToString(minimum) });
            }

            if (declared.MaxLength is { } maximum)
            {
                restriction.Facets.Add(new XmlSchemaMaxLengthFacet { Value = XmlConvert.ToString(maximum) });
            }

            if (declared.Pattern is { } pattern && XsdPattern.TryWrite(pattern, out var xsd))
            {
                restriction.Facets.Add(new XmlSchemaPatternFacet { Value = xsd });
            }
        }
        else if (typeof(TValue) == typeof(Guid))
        {
            restriction.Facets.Add(new XmlSchemaPatternFacet { Value = GuidPattern });
        }

        // xs:integer bounds nothing, but an unsigned 128-bit integer starts at zero.
        var low = Bound<TValue>(declared.Minimum) ?? (typeof(TValue) == typeof(UInt128) ? "0" : null);
        if (low is not null && Accepts(datatype, low))
        {
            restriction.Facets.Add(new XmlSchemaMinInclusiveFacet { Value = low });
        }

        if (Bound<TValue>(declared.Maximum) is { } high && Accepts(datatype, high))
        {
            restriction.Facets.Add(new XmlSchemaMaxInclusiveFacet { Value = high });
        }

        if (declared.IsClosedValueSet && !declared.KnownValues.IsDefaultOrEmpty
            && declared.KnownValues.All(known => known is TValue typed && Accepts(datatype, XmlLexical.Format(typed))))
        {
            foreach (var known in declared.KnownValues)
            {
                restriction.Facets.Add(new XmlSchemaEnumerationFacet { Value = XmlLexical.Format((TValue)known) });
            }
        }

        // On the restriction: DataContractSerializer's exporter replaces the annotation of the type with one of its own.
        if (!string.IsNullOrWhiteSpace(declared.Description))
        {
            restriction.Annotation = new XmlSchemaAnnotation
            {
                Items = { new XmlSchemaDocumentation { Markup = [new XmlDocument().CreateTextNode(declared.Description)] } },
            };
        }

        return new XmlSchemaSimpleType { Name = name, Content = restriction };
    }

    /// <summary>
    /// Writes a bound, held in the invariant form <see cref="ValueObjectBound.Text{TValue}"/> writes it, in the lexical form
    /// of the underlying type, or answers <see langword="null"/> for one it cannot read.
    /// </summary>
    private static string? Bound<TValue>(string? text)
        => text is not null && XmlLexical.TryParseInvariant(text, out TValue? bound) ? XmlLexical.Format(bound!) : null;

    /// <summary>
    /// Tells whether System.Xml reads a value as one of the XSD type: it holds an <c>xs:integer</c> in a
    /// <see cref="decimal"/>, and refuses the whole schema, and every serializer built over it, for a facet it cannot read.
    /// </summary>
    private static bool Accepts(XmlSchemaDatatype datatype, string value)
    {
        try
        {
            datatype.ParseValue(value, null, null);
            return true;
        }
        catch (XmlSchemaException)
        {
            return false;
        }
    }

    /// <summary>Tells whether System.Xml compiles a simple type, which it refuses for facets that contradict each other.</summary>
    private static bool Compiles(XmlSchemaSimpleType type)
    {
        var schema = new XmlSchema();
        schema.Items.Add(type);
        var set = new XmlSchemaSet();
        var valid = true;
        set.ValidationEventHandler += (_, _) => valid = false;
        set.Add(schema);
        set.Compile();
        return valid;
    }

    /// <summary>
    /// Tells whether an <c>xsi:nil</c> attribute marks its element nil: <c>true</c> or <c>1</c>, as an <c>xs:boolean</c>
    /// reads, and not text of another kind, which leaves the element to be read as it is.
    /// </summary>
    private static bool IsNil(string? flag) => flag is not null && XmlLexical.TryParse(flag, out bool nil) && nil;

    private static XmlException Refusal(string code, string message, int line, int position, Exception? inner = null)
    {
        var exception = line > 0 ? new XmlException(message, inner, line, position) : new XmlException(message, inner);
        exception.Data[ValueObjectErrors.ErrorCodeKey] = code;
        return exception;
    }

    /// <summary>
    /// Gets the name a type is described by: its name, after the types it is nested in, without the arity, and followed
    /// by <c>Of</c> and the names of its type arguments for a construction, as <c>DataContractSerializer</c> names one.
    /// </summary>
    private static string LocalName(Type type)
    {
        var name = NameOf(type);
        for (var outer = type.DeclaringType; outer is not null; outer = outer.DeclaringType)
        {
            name = NameOf(outer) + "." + name;
        }

        return type.IsConstructedGenericType
            ? name + "Of" + string.Concat(type.GetGenericArguments().Select(LocalName))
            : name;
    }

    private static string NameOf(Type type)
    {
        var tick = type.Name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? type.Name : type.Name[..tick];
    }

    /// <summary>The value object a simple type was added for, and what it says.</summary>
    private sealed record Described(Type Type, string Rules);
}
