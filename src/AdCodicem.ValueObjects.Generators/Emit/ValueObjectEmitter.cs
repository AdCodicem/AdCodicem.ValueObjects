using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdCodicem.ValueObjects.Generators.Internal;
using AdCodicem.ValueObjects.Generators.Model;

namespace AdCodicem.ValueObjects.Generators.Emit;

/// <summary>
/// Emits the implementation of one value object.
/// </summary>
internal static class ValueObjectEmitter
{
    private const string Abstractions = "global::AdCodicem.ValueObjects";
    private const string Identifiers = Abstractions + ".Identifiers";
    private const string IdFormat = Identifiers + ".EntityIdFormat";
    private const string ValidationResult = Abstractions + ".ValidationResult";
    private const string ErrorCodes = Abstractions + ".ValueObjectErrorCodes";
    private const string UncheckedTag = Abstractions + ".UncheckedTag";
    private const string Inline = "[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";
    private const string Invariant = "global::System.Globalization.CultureInfo.InvariantCulture";

    /// <summary>
    /// The most characters a formatting hook is offered before <c>ToString</c> gives up on it: far more than the text
    /// of any value object, and few enough that a hook which never succeeds costs a few megabytes, once.
    /// </summary>
    private const int MaxFormattedLength = 1 << 20;

    /// <summary>
    /// The member a span formatting hook's <c>ToString</c> hands its retry loop to, once the stack buffer is too small.
    /// </summary>
    private const string PooledFormatMethod = "FormatWithPooledBuffer";

    /// <summary>The members written on every value object, by name.</summary>
    private static readonly string[] CommonMembers =
    [
        "_value", "Value", "KnownValues", "Schema", "Normalize", "Validate", "Create", "TryCreate",
        "CreateUnchecked", "Equals", "GetHashCode", "CompareTo", "ToString", "TryFormat", "Parse", "TryParse",
        "ValueJsonConverter", "ValueTypeConverter",
    ];

    /// <summary>The members arithmetic adds, by name.</summary>
    private static readonly string[] ArithmeticMembers = ["Zero", "One", "IsZero", "Abs", "Min", "Max", "Sum"];

    /// <summary>
    /// The getters of the properties written on every value object. The compiler names a getter <c>get_</c>
    /// followed by its property's name and reserves that name in the type, as it does a member's.
    /// </summary>
    private static readonly string[] CommonGetters = ["get_Value", "get_KnownValues", "get_Schema"];

    /// <summary>The getters of the properties arithmetic adds.</summary>
    private static readonly string[] ArithmeticGetters = ["get_Zero", "get_One", "get_IsZero"];

    /// <summary>
    /// The metadata names of the operators written on every value object. The compiler reserves an operator's
    /// metadata name in the type as it does any member's, so a property of that name would not compile.
    /// </summary>
    private static readonly string[] ComparisonOperators =
    [
        "op_Equality", "op_Inequality", "op_LessThan", "op_GreaterThan", "op_LessThanOrEqual", "op_GreaterThanOrEqual",
    ];

    /// <summary>The metadata names of the binary operators arithmetic adds.</summary>
    private static readonly string[] ArithmeticOperators = ["op_Addition", "op_Subtraction", "op_Multiply", "op_Division"];

    /// <summary>The members an entity identifier adds, by name, its getters included.</summary>
    private static readonly string[] EntityIdMembers =
        ["Prefix", "Granularity", "Length", "New", "get_Prefix", "get_Granularity", "get_Length"];

    /// <summary>
    /// Gets the names the members written on a value object take in its scope.
    /// </summary>
    /// <remarks>
    /// The members written here, the metadata names of the operators and of the property getters written here: the
    /// compiler reserves each of them in the type. The names follow the options because the members do:
    /// <c>Zero</c> is only taken on a value object with arithmetic. They are listed beside the emitters so that a
    /// member added here is added to them in the same change; <c>KnownValueNameTests</c> and <c>TypeNameTests</c>
    /// read the generated code to check it.
    /// </remarks>
    /// <param name="underlying">The underlying type, whose sign decides whether a negation is written.</param>
    /// <param name="arithmetic">Whether the arithmetic members are written.</param>
    /// <param name="implicitConversion">Whether the implicit conversion to the underlying value is written.</param>
    /// <param name="explicitConversion">Whether the explicit conversion from the underlying value is written.</param>
    /// <param name="closedValueSet">Whether the membership lookup of a closed value set is written.</param>
    /// <param name="pattern">Whether the compiled pattern is written.</param>
    /// <param name="normalizesFromSpan">Whether the factory normalizing from a span is written.</param>
    /// <param name="entityId">Whether the members of an entity identifier are written.</param>
    /// <param name="formatsThroughSpanHook">
    /// Whether <c>ToString</c> goes through a span formatting hook, which writes its retry loop as a member of its own.
    /// </param>
    /// <returns>The names, compared ordinally.</returns>
    public static HashSet<string> MemberNames(
        UnderlyingType underlying,
        bool arithmetic,
        bool implicitConversion,
        bool explicitConversion,
        bool closedValueSet,
        bool pattern,
        bool normalizesFromSpan,
        bool entityId,
        bool formatsThroughSpanHook)
    {
        var names = new HashSet<string>(CommonMembers, StringComparer.Ordinal);
        names.UnionWith(CommonGetters);
        names.UnionWith(ComparisonOperators);

        if (arithmetic)
        {
            names.UnionWith(ArithmeticMembers);
            names.UnionWith(ArithmeticGetters);
            names.UnionWith(ArithmeticOperators);

            if (underlying.IsSigned)
            {
                names.Add("op_UnaryNegation");
            }
        }

        if (implicitConversion)
        {
            names.Add("op_Implicit");
        }

        if (explicitConversion)
        {
            names.Add("op_Explicit");
        }

        if (closedValueSet)
        {
            names.Add("KnownUnderlyingValues");
        }

        if (pattern)
        {
            names.Add("DeclaredPattern");
        }

        if (normalizesFromSpan)
        {
            names.Add("TryCreateFrom");
        }

        if (entityId)
        {
            names.UnionWith(EntityIdMembers);
        }

        if (formatsThroughSpanHook)
        {
            names.Add(PooledFormatMethod);
        }

        return names;
    }

    /// <summary>
    /// Gets the names a known value cannot take on a value object because the generated code uses them.
    /// </summary>
    /// <remarks>
    /// A known value becomes a static property of the value object, so a name already taken there would not
    /// compile, in a file the author cannot edit: the names of the members written here, and the name of the type,
    /// which its constructor takes. The statements written here discard nothing, so <c>_</c> is free.
    /// </remarks>
    /// <param name="typeName">Name of the value object, which its constructor takes.</param>
    /// <param name="underlying">The underlying type, whose sign decides whether a negation is written.</param>
    /// <param name="arithmetic">Whether the arithmetic members are written.</param>
    /// <param name="implicitConversion">Whether the implicit conversion to the underlying value is written.</param>
    /// <param name="explicitConversion">Whether the explicit conversion from the underlying value is written.</param>
    /// <param name="closedValueSet">Whether the membership lookup of a closed value set is written.</param>
    /// <param name="pattern">Whether the compiled pattern is written.</param>
    /// <param name="normalizesFromSpan">Whether the factory normalizing from a span is written.</param>
    /// <param name="formatsThroughSpanHook">Whether <c>ToString</c> goes through a span formatting hook.</param>
    /// <returns>The names taken, compared ordinally.</returns>
    public static HashSet<string> TakenNames(
        string typeName,
        UnderlyingType underlying,
        bool arithmetic,
        bool implicitConversion,
        bool explicitConversion,
        bool closedValueSet,
        bool pattern,
        bool normalizesFromSpan,
        bool formatsThroughSpanHook)
    {
        var names = MemberNames(
            underlying,
            arithmetic,
            implicitConversion,
            explicitConversion,
            closedValueSet,
            pattern,
            normalizesFromSpan,
            entityId: false,
            formatsThroughSpanHook);
        names.Add(typeName);

        return names;
    }

    public static string Emit(ValueObjectModel model)
    {
        var writer = new CodeWriter();
        var underlying = model.Underlying;
        var value = underlying.FullName;
        var self = model.QualifiedName;

        writer.Line("// <auto-generated/>");
        writer.Line("#nullable enable");
        writer.Line("#pragma warning disable CS1591 // members are documented through <inheritdoc/>");
        writer.Line("#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604 // the emitter proves the nullability itself");
        writer.Line("#pragma warning disable CS8981 // a lower-case name is the author's, reported where they declared it");
        writer.Line();

        var closeCount = 0;
        if (model.Namespace.Length > 0)
        {
            writer.Open($"namespace {model.Namespace}");
            closeCount++;
        }

        var depth = 0;
        foreach (var containing in model.ContainingTypes)
        {
            writer.Open(containing);
            closeCount++;

            var step = depth - model.RegistrationRouteStart;
            if (step >= 0 && step < model.RegistrationRoute.Length)
            {
                RegistrationEmitter.EmitStep(writer, model, step);
            }

            depth++;
        }

        EmitTypeAttributes(writer, model);

        var contract = model switch
        {
            { IsEntityId: true } => $"{Identifiers}.IEntityId<{self}>",
            { Arithmetic: true } => $"{Abstractions}.INumericValueObject<{self}, {value}>",
            _ => $"{Abstractions}.IValueObject<{self}, {value}>",
        };

        writer.Open($"partial struct {model.Identifier}{model.TypeParameters} : {contract}");

        EmitState(writer, model, value, self);
        EmitEntityIdMembers(writer, model, self);

        // The pattern comes before the named constants, which go through it while they are created, and the named
        // constants come before the schema so that the schema can publish their normalized values.
        EmitDeclaredPattern(writer, model);
        EmitKnownValues(writer, model, value, self);
        EmitSchema(writer, model, underlying);
        EmitNormalize(writer, model, value);
        EmitValidate(writer, model, underlying, value);
        EmitFactories(writer, model, value, self);
        EmitEquality(writer, model, value, self);
        EmitComparison(writer, model, self);
        EmitFormatting(writer, model, underlying, value);
        EmitParsing(writer, model, underlying, value, self);
        EmitConversions(writer, model, value, self);
        EmitArithmetic(writer, model, underlying, value, self);

        JsonConverterEmitter.Emit(writer, model, underlying, value, self);
        TypeConverterEmitter.Emit(writer, model, underlying, value, self);

        writer.Close();

        for (var i = 0; i < closeCount; i++)
        {
            writer.Close();
        }

        return writer.ToString();
    }

    private static void EmitTypeAttributes(CodeWriter writer, ValueObjectModel model)
    {
        // An attribute cannot name a type through a type parameter, and neither System.Text.Json nor TypeDescriptor
        // closes an open generic converter: a generic value object names converters that close its own over the
        // construction they are asked for.
        if (model.IsGeneric)
        {
            writer.Line($"[global::System.Text.Json.Serialization.JsonConverter(typeof({Abstractions}.Metadata.GenericValueObjectJsonConverterFactory))]");
            writer.Line($"[global::System.ComponentModel.TypeConverter(typeof({Abstractions}.Metadata.GenericValueObjectTypeConverter))]");
        }
        else
        {
            writer.Line($"[global::System.Text.Json.Serialization.JsonConverter(typeof({model.QualifiedName}.ValueJsonConverter))]");
            writer.Line($"[global::System.ComponentModel.TypeConverter(typeof({model.QualifiedName}.ValueTypeConverter))]");
        }

        writer.Line("[global::System.Diagnostics.DebuggerDisplay(\"{ToString(),nq}\")]");
    }

    private static void EmitState(CodeWriter writer, ValueObjectModel model, string value, string self)
    {
        var underlying = model.Underlying;

        writer.Line("/// <summary>Backing storage. The struct is exactly the size of its underlying value.</summary>");
        writer.Line($"private readonly {value}{(underlying.IsReferenceType ? "?" : string.Empty)} _value;");
        writer.Line();

        // The tag is required and of a type only generated code has a reason to supply: a reflection mapper calls a
        // non-public constructor that takes the source value alone, and would wrap a value no rule has checked.
        writer.Line("/// <summary>Wraps an already normalized and validated value.</summary>");
        writer.Line(Inline);
        writer.Open($"private {model.Identifier}({value} value, {UncheckedTag} tag)");
        writer.Line("_value = value;");
        writer.Close();
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Line(underlying.IsReferenceType
            ? $"public {value} Value => _value ?? {value}.Empty;"
            : $"public {value} Value => _value;");
        writer.Line();

        // Explicit, so that a tool reading the public instance properties of the type (a logger destructuring it, a
        // schema generator, an exporter) finds the value alone, not a guard meant for code.
        writer.Line("/// <inheritdoc />");
        writer.Line(underlying.IsReferenceType
            ? $"bool {Abstractions}.IValueObject<{self}, {value}>.IsDefault => _value is null;"
            : $"bool {Abstractions}.IValueObject<{self}, {value}>.IsDefault => _value.Equals(default({value}));");
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Line($"object? {Abstractions}.IValueObject.GetBoxedValue() => Value;");
        writer.Line();
    }

    /// <summary>
    /// Emits the members an entity identifier adds over an ordinary string value object.
    /// </summary>
    /// <remarks>
    /// The profile is emitted as expression-bodied properties rather than initialized fields, so that the
    /// schema initializer below can read them regardless of the order the members appear in.
    /// </remarks>
    /// <param name="writer">Sink.</param>
    /// <param name="model">Value object being emitted.</param>
    /// <param name="self">Fully qualified name of the value object.</param>
    private static void EmitEntityIdMembers(CodeWriter writer, ValueObjectModel model, string self)
    {
        if (model.Id is not { } profile)
        {
            return;
        }

        var granularity = $"{Identifiers}.IdGranularity.{profile.GranularityName}";

        writer.Line("/// <inheritdoc />");
        writer.Line($"public static string Prefix => {LiteralFactory.Quote(profile.Prefix)};");
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Line($"public static {Identifiers}.IdGranularity Granularity => {granularity};");
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Line($"public static int Length => {profile.TotalLength};");
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Line(Inline);
        writer.Line($"public static {self} New() => New({Identifiers}.ValueObjectIds.TimeProvider, {Identifiers}.ValueObjectIds.Entropy);");
        writer.Line();

        // Straight to the constructor: what Create produces is canonical and valid by construction, so
        // re-normalizing and re-validating it would only cost a scan to reach the same value.
        writer.Line("/// <inheritdoc />");
        writer.Line($"public static {self} New(global::System.TimeProvider timeProvider, {Identifiers}.IdEntropySource entropy)");
        writer.Line($"    => new({IdFormat}.Create(Prefix, Granularity, timeProvider, entropy), default({UncheckedTag}));");
        writer.Line();
    }

    private static void EmitSchema(CodeWriter writer, ValueObjectModel model, UnderlyingType underlying)
    {
        var properties = new List<string>();

        if (model.Pattern is not null)
        {
            properties.Add($"Pattern = {LiteralFactory.Quote(model.Pattern)},");
        }
        else if (model.HasPatternHook)
        {
            // The text the [GeneratedRegex] attribute holds, so that describing the type builds no regular expression.
            // Without the attribute, the regular expression is asked for it, which builds it as the type initializes.
            properties.Add(model.PatternHookText is not null
                ? $"Pattern = {LiteralFactory.Quote(model.PatternHookText)},"
                : $"Pattern = {Abstractions}.ValueObjectPattern.Of<{model.QualifiedName}>().ToString(),");
        }
        else if (model.IsEntityId)
        {
            // Published for the clients generated from the document, and never compiled here: at fixed length
            // over a fixed alphabet the running check is a span scan.
            properties.Add($"Pattern = {IdFormat}.SchemaPattern(Prefix, Granularity),");
        }

        if (model.MinLength >= 0)
        {
            properties.Add($"MinLength = {model.MinLength},");
        }

        if (model.MaxLength >= 0)
        {
            properties.Add($"MaxLength = {model.MaxLength},");
        }

        // A hook is read when the type initializes, through the bridge, and written in the one form a bound of its type
        // takes, which the OpenAPI transformer reads back as it reads a text bound.
        if (model.HasMinimumHook)
        {
            properties.Add($"Minimum = {Abstractions}.ValueObjectBound.Text({BoundOf(model, "Minimum")}),");
        }
        else if (model.MinimumText is not null)
        {
            properties.Add($"Minimum = {LiteralFactory.Quote(model.MinimumText)},");
        }

        if (model.HasMaximumHook)
        {
            properties.Add($"Maximum = {Abstractions}.ValueObjectBound.Text({BoundOf(model, "Maximum")}),");
        }
        else if (model.MaximumText is not null)
        {
            properties.Add($"Maximum = {LiteralFactory.Quote(model.MaximumText)},");
        }

        var format = model.SchemaFormat ?? underlying.SchemaFormat;
        if (format is not null)
        {
            properties.Add($"Format = {LiteralFactory.Quote(format)},");
        }

        if (model.Description is not null)
        {
            properties.Add($"Description = {LiteralFactory.Quote(model.Description)},");
        }

        if (model.Example is not null)
        {
            properties.Add($"Example = {LiteralFactory.Quote(model.Example)},");
        }
        else if (model.IsEntityId)
        {
            // Derived rather than random, so that regenerating the document produces the same bytes and a
            // committed specification does not churn on every build.
            properties.Add($"Example = {IdFormat}.Example(Prefix, Granularity),");
        }

        if (model.IsClosedValueSet)
        {
            properties.Add("IsClosedValueSet = true,");
        }

        if (!model.KnownValues.IsEmpty)
        {
            var boxed = string.Join(", ", model.KnownValues.Select(known => $"{known.Name}.Value"));
            properties.Add($"KnownValues = global::System.Collections.Immutable.ImmutableArray.Create<object>({boxed}),");
        }

        writer.Line("/// <summary>The declarative constraints of this value object, shared by validation, OpenAPI and persistence.</summary>");
        writer.Line($"public static global::AdCodicem.ValueObjects.Metadata.ValueObjectSchema Schema {{ get; }} = new()");
        writer.Line("{");
        writer.Indent();
        foreach (var property in properties)
        {
            writer.Line(property);
        }

        writer.Unindent();
        writer.Line("};");
        writer.Line();
    }

    private static void EmitKnownValues(CodeWriter writer, ValueObjectModel model, string value, string self)
    {
        if (model.KnownValues.IsEmpty)
        {
            return;
        }

        // Order matters: static initializers run in declaration order, and the membership lookup has to exist
        // before the named constants below go through Create, which consults it.
        if (model.IsClosedValueSet)
        {
            var normalized = string.Join(", ", model.KnownValues.Select(known => $"Normalize({known.Literal})"));
            var comparer = model.Underlying.IsString
                ? $", global::System.StringComparer.{model.ComparisonName}"
                : string.Empty;

            writer.Line("/// <summary>Frozen membership lookup backing the closed value set.</summary>");
            writer.Line($"private static readonly global::System.Collections.Frozen.FrozenSet<{value}> KnownUnderlyingValues =");
            writer.Line($"    global::System.Collections.Frozen.FrozenSet.ToFrozenSet(new {value}[] {{ {normalized} }}{comparer});");
            writer.Line();
        }

        foreach (var known in model.KnownValues)
        {
            // Create, not the raw constructor: a declared value that violates the type's own rules must fail
            // loudly on first use rather than exist as an unreachable constant.
            writer.Line($"/// <summary>{Xml(known.Description ?? known.Name)}</summary>");
            writer.Line($"public static {self} {known.Name} {{ get; }} = Create({known.Literal});");
            writer.Line();
        }

        var names = string.Join(", ", model.KnownValues.Select(known => known.Name));
        writer.Line("/// <summary>Every value declared through <c>[KnownValue]</c>, in declaration order.</summary>");
        writer.Line($"public static global::System.Collections.Immutable.ImmutableArray<{self}> KnownValues {{ get; }} =");
        writer.Line($"    global::System.Collections.Immutable.ImmutableArray.Create({names});");
        writer.Line();
    }

    private static void EmitNormalize(CodeWriter writer, ValueObjectModel model, string value)
    {
        writer.Line("/// <inheritdoc />");
        writer.Line(Inline);

        if (model.IsEntityId)
        {
            // The format owns its own canonical spelling — lower case, Crockford aliases — so an identifier
            // type never writes a normalizer, and VO0017 reports one that tried.
            writer.Line(
                $"public static {value} Normalize({value} value) => value is null "
                + $"? value : {IdFormat}.Normalize(global::System.MemoryExtensions.AsSpan(value), Prefix);");
        }
        else if (!model.HasNormalizeHook)
        {
            writer.Line($"public static {value} Normalize({value} value) => value;");
        }
        else if (model.Underlying.IsReferenceType)
        {
            // The hook never sees null: a missing value is rejected by Validate, not normalized into something.
            writer.Line($"public static {value} Normalize({value} value) => value is null ? value : NormalizeValue(value);");
        }
        else
        {
            writer.Line($"public static {value} Normalize({value} value) => NormalizeValue(value);");
        }

        writer.Line();
    }

    /// <summary>
    /// Emits the compiled form of the <c>Pattern</c> option.
    /// </summary>
    /// <remarks>
    /// Written before the named constants: static initializers run in declaration order, and each constant goes
    /// through <c>Create</c>, and so through this field. Written after them, it was still null while they were
    /// created, and the type initializer threw inside the module initializer, before any code of the assembly ran.
    /// </remarks>
    /// <param name="writer">Sink.</param>
    /// <param name="model">Value object being emitted.</param>
    private static void EmitDeclaredPattern(CodeWriter writer, ValueObjectModel model)
    {
        if (model.Pattern is null)
        {
            return;
        }

        // A generator cannot feed [GeneratedRegex], which only sees hand-written code, so the pattern is compiled
        // once into a static field instead. A timeout keeps a pathological pattern from hanging a request thread.
        writer.Line("/// <summary>The declared pattern, compiled once for the lifetime of the process.</summary>");
        writer.Line("private static readonly global::System.Text.RegularExpressions.Regex DeclaredPattern = new(");
        writer.Line($"    {LiteralFactory.Quote(model.Pattern)},");
        writer.Line("    global::System.Text.RegularExpressions.RegexOptions.Compiled | global::System.Text.RegularExpressions.RegexOptions.CultureInvariant,");
        writer.Line("    global::System.TimeSpan.FromSeconds(1));");
        writer.Line();
    }

    private static void EmitValidate(CodeWriter writer, ValueObjectModel model, UnderlyingType underlying, string value)
    {
        writer.Line("/// <inheritdoc />");
        writer.Open($"public static {ValidationResult} Validate(in {value} value)");

        if (model.IsEntityId)
        {
            // An absent value reads as absent, not as a length violation, the same way it does for every other
            // string value object. A caller distinguishing "the field was not filled in" from "the field holds
            // something malformed" needs the two apart.
            writer.Open("if (value is null || value.Length == 0)");
            writer.Line($"return {ValidationResult}.Required(\"A value is required.\");");
            writer.Close();
            writer.Line();

            // The format reports which rule broke — length, prefix, alphabet or check character — rather than
            // flattening every rejection into one code a caller cannot act on.
            writer.Line($"{ValidationResult} shape = {IdFormat}.Validate(global::System.MemoryExtensions.AsSpan(value), Prefix, Granularity);");
            writer.Open("if (!shape.IsValid)");
            writer.Line("return shape;");
            writer.Close();
            writer.Line();

            writer.Line(model.HasValidateHook
                ? "return ValidateValue(in value);"
                : $"return {ValidationResult}.Success;");

            writer.Close();
            writer.Line();
            return;
        }

        if (underlying.IsString)
        {
            writer.Open("if (value is null)");
            writer.Line($"return {ValidationResult}.Required(\"A value is required.\");");
            writer.Close();
            writer.Line();

            if (!model.AllowEmpty)
            {
                writer.Open("if (value.Length == 0)");
                writer.Line($"return {ValidationResult}.Required(\"The value must not be empty.\");");
                writer.Close();
                writer.Line();
            }

            if (model.MinLength >= 0)
            {
                writer.Open($"if (value.Length < {model.MinLength})");
                writer.Line($"return {ValidationResult}.Failure({ErrorCodes}.TooShort, \"The value must be at least {model.MinLength} characters long.\");");
                writer.Close();
                writer.Line();
            }

            if (model.MaxLength >= 0)
            {
                writer.Open($"if (value.Length > {model.MaxLength})");
                writer.Line($"return {ValidationResult}.Failure({ErrorCodes}.TooLong, \"The value must be at most {model.MaxLength} characters long.\");");
                writer.Close();
                writer.Line();
            }
        }

        // The hook takes the option's place, with the same code and message, so moving from one to the other changes
        // nothing a caller can observe.
        if (model.Pattern is not null || model.HasPatternHook)
        {
            // Through a type parameter, which reaches the pattern however the type implements it, explicitly included.
            writer.Open(model.HasPatternHook
                ? $"if (!{Abstractions}.ValueObjectPattern.Of<{model.QualifiedName}>().IsMatch(value))"
                : "if (!DeclaredPattern.IsMatch(value))");
            writer.Line($"return {ValidationResult}.InvalidFormat(\"The value does not match the expected format.\");");
            writer.Close();
            writer.Line();
        }

        // NaN compares false with everything: written as `value < minimum`, a bound would let it through, so a
        // floating-point bound asks whether the value is inside it instead.
        var floating = underlying.Kind is UnderlyingKind.Double or UnderlyingKind.Single;

        if (underlying.SupportsBounds && model.HasMinimumHook)
        {
            // Read through the bridge, which reaches an explicit implementation too and which the JIT folds into a
            // constant; the message quotes the bound only on the rejection path.
            var minimum = BoundOf(model, "Minimum");
            writer.Open(floating ? $"if (!(value >= {minimum}))" : $"if (value < {minimum})");
            writer.Line($"return {ValidationResult}.OutOfRange(\"The value must be greater than or equal to \" + {Abstractions}.ValueObjectBound.Text({minimum}) + \".\");");
            writer.Close();
            writer.Line();
        }
        else if (underlying.SupportsBounds && model.MinimumLiteral is not null)
        {
            writer.Open(floating ? $"if (!(value >= {model.MinimumLiteral}))" : $"if (value < {model.MinimumLiteral})");
            writer.Line($"return {ValidationResult}.OutOfRange({LiteralFactory.Quote($"The value must be greater than or equal to {model.MinimumText}.")});");
            writer.Close();
            writer.Line();
        }

        if (underlying.SupportsBounds && model.HasMaximumHook)
        {
            var maximum = BoundOf(model, "Maximum");
            writer.Open(floating ? $"if (!(value <= {maximum}))" : $"if (value > {maximum})");
            writer.Line($"return {ValidationResult}.OutOfRange(\"The value must be less than or equal to \" + {Abstractions}.ValueObjectBound.Text({maximum}) + \".\");");
            writer.Close();
            writer.Line();
        }
        else if (underlying.SupportsBounds && model.MaximumLiteral is not null)
        {
            writer.Open(floating ? $"if (!(value <= {model.MaximumLiteral}))" : $"if (value > {model.MaximumLiteral})");
            writer.Line($"return {ValidationResult}.OutOfRange({LiteralFactory.Quote($"The value must be less than or equal to {model.MaximumText}.")});");
            writer.Close();
            writer.Line();
        }

        if (model.IsClosedValueSet)
        {
            writer.Open("if (!KnownUnderlyingValues.Contains(value))");
            writer.Line($"return {ValidationResult}.Failure({ErrorCodes}.NotAKnownValue, \"The value is not one of the accepted values.\");");
            writer.Close();
            writer.Line();
        }

        writer.Line(model.HasValidateHook
            ? "return ValidateValue(in value);"
            : $"return {ValidationResult}.Success;");

        writer.Close();
        writer.Line();
    }

    private static void EmitFactories(CodeWriter writer, ValueObjectModel model, string value, string self)
    {
        writer.Line("/// <inheritdoc />");
        writer.Open($"public static {self} Create({value} value)");
        writer.Line($"{value} normalized = Normalize(value);");
        writer.Line($"{ValidationResult} validation = Validate(in normalized);");
        writer.Open("if (!validation.IsValid)");
        writer.Line($"validation.ThrowIfInvalid(typeof({self}), {AttemptedValue(model, "value")});");
        writer.Close();
        writer.Line();
        writer.Line($"return new {self}(normalized, default({UncheckedTag}));");
        writer.Close();
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Line(Inline);
        writer.Line($"public static bool TryCreate({value} value, out {self} result) => TryCreate(value, out result, out {ValidationResult} ignored);");
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Open($"public static bool TryCreate({value} value, out {self} result, out {ValidationResult} validation)");
        writer.Line($"{value} normalized = Normalize(value);");
        writer.Line("validation = Validate(in normalized);");
        writer.Open("if (validation.IsValid)");
        writer.Line($"result = new {self}(normalized, default({UncheckedTag}));");
        writer.Line("return true;");
        writer.Close();
        writer.Line();
        writer.Line("result = default;");
        writer.Line("return false;");
        writer.Close();
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Line(Inline);
        writer.Line($"public static {self} CreateUnchecked({value} value) => new(value, default({UncheckedTag}));");
        writer.Line();

        if (model.NormalizesFromSpan)
        {
            // Normalizing straight from the span means the normalized string is the only one allocated, where
            // going through TryCreate(string) would materialize the raw text first and then throw it away.
            writer.Line("/// <summary>Creates from text without materializing it before normalization.</summary>");
            writer.Open($"private static bool TryCreateFrom(global::System.ReadOnlySpan<char> value, out {self} result, out {ValidationResult} validation)");
            writer.Line(model.IsEntityId
                ? $"{value} normalized = {IdFormat}.Normalize(value, Prefix);"
                : $"{value} normalized = NormalizeValue(value);");
            writer.Line("validation = Validate(in normalized);");
            writer.Open("if (validation.IsValid)");
            writer.Line($"result = new {self}(normalized, default({UncheckedTag}));");
            writer.Line("return true;");
            writer.Close();
            writer.Line();
            writer.Line("result = default;");
            writer.Line("return false;");
            writer.Close();
            writer.Line();
        }
    }

    /// <summary>
    /// Writes what the exception of <c>Create</c> or <c>Parse</c> carries as its <c>AttemptedValue</c>: the rejected
    /// value, or <see langword="null"/> on a type its author classifies as sensitive data, whose value a logger reading
    /// the public properties of an exception would otherwise record in clear.
    /// </summary>
    /// <param name="model">The value object.</param>
    /// <param name="rejected">The expression of the rejected value.</param>
    /// <returns>The expression to pass.</returns>
    private static string AttemptedValue(ValueObjectModel model, string rejected) => model.IsClassified ? "null" : rejected;

    private static void EmitEquality(CodeWriter writer, ValueObjectModel model, string value, string self)
    {
        var underlying = model.Underlying;
        var comparison = $"global::System.StringComparison.{model.ComparisonName}";

        writer.Line("/// <inheritdoc />");
        writer.Line(Inline);
        writer.Line(underlying.IsString
            ? $"public bool Equals({self} other) => string.Equals(_value, other._value, {comparison});"
            : $"public bool Equals({self} other) => _value.Equals(other._value);");
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Line($"public override bool Equals(object? obj) => obj is {self} other && Equals(other);");
        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Line(underlying.IsString
            ? $"public override int GetHashCode() => _value is null ? 0 : _value.GetHashCode({comparison});"
            : "public override int GetHashCode() => _value.GetHashCode();");
        writer.Line();

        writer.Line("/// <summary>Determines whether two values are equal.</summary>");
        writer.Line(Inline);
        writer.Line($"public static bool operator ==({self} left, {self} right) => left.Equals(right);");
        writer.Line();

        writer.Line("/// <summary>Determines whether two values differ.</summary>");
        writer.Line(Inline);
        writer.Line($"public static bool operator !=({self} left, {self} right) => !left.Equals(right);");
        writer.Line();

        _ = value;
    }

    private static void EmitComparison(CodeWriter writer, ValueObjectModel model, string self)
    {
        var comparison = $"global::System.StringComparison.{model.ComparisonName}";

        writer.Line("/// <inheritdoc />");
        writer.Line(model.Underlying.IsString
            ? $"public int CompareTo({self} other) => string.Compare(_value, other._value, {comparison});"
            : $"public int CompareTo({self} other) => _value.CompareTo(other._value);");
        writer.Line();

        foreach (var (op, test) in new[] { ("<", "< 0"), (">", "> 0"), ("<=", "<= 0"), (">=", ">= 0") })
        {
            writer.Line($"/// <summary>Compares two values.</summary>");
            writer.Line(Inline);
            writer.Line($"public static bool operator {op}({self} left, {self} right) => left.CompareTo(right) {test};");
            writer.Line();
        }
    }

    private static void EmitFormatting(CodeWriter writer, ValueObjectModel model, UnderlyingType underlying, string value)
    {
        // The text of the underlying value, in the form Parse reads back.
        var roundTrip = underlying.RoundTripFormat is null ? "null" : LiteralFactory.Quote(underlying.RoundTripFormat);
        var plainText = underlying.IsString
            ? "Value"
            : underlying.IsSpanFormattable
                ? $"Value.ToString({roundTrip}, {Invariant})"
                : $"Value.ToString({Invariant})";

        // A hook takes over formatting entirely, the default format included, so ToString() writes what
        // interpolation and ToString(null, null) write.
        writer.Line("/// <inheritdoc />");
        writer.Line(model.HasFormatHook || model.HasTryFormatHook
            ? "public override string ToString() => ToString(null, null);"
            : $"public override string ToString() => {plainText};");
        writer.Line();

        writer.Line("/// <inheritdoc />");
        if (model.HasFormatHook)
        {
            writer.Open("public string ToString(string? format, global::System.IFormatProvider? formatProvider)");
            writer.Line($"{value} current = Value;");
            writer.Line($"return FormatValue(in current, global::System.MemoryExtensions.AsSpan(format), formatProvider ?? {Invariant});");
            writer.Close();
        }
        else if (model.HasTryFormatHook)
        {
            EmitHookToString(writer, model, underlying);
        }
        else if (underlying.IsString)
        {
            writer.Line("public string ToString(string? format, global::System.IFormatProvider? formatProvider) => Value;");
        }
        else if (underlying.IsSpanFormattable)
        {
            // The default format is the one ToString() uses, so that interpolation, a TypeConverter and a binder
            // write what Parse reads back; left to the underlying type, a TimeOnly would drop its seconds.
            var format = underlying.RoundTripFormat is null
                ? "format"
                : $"string.IsNullOrEmpty(format) ? {LiteralFactory.Quote(underlying.RoundTripFormat)} : format";
            writer.Line("public string ToString(string? format, global::System.IFormatProvider? formatProvider)");
            writer.Line($"    => Value.ToString({format}, formatProvider ?? {Invariant});");
        }
        else
        {
            writer.Line("public string ToString(string? format, global::System.IFormatProvider? formatProvider)");
            writer.Line($"    => Value.ToString(formatProvider ?? {Invariant});");
        }

        writer.Line();

        writer.Line("/// <inheritdoc />");
        writer.Open("public bool TryFormat(global::System.Span<char> destination, out int charsWritten, global::System.ReadOnlySpan<char> format, global::System.IFormatProvider? provider)");

        // The string formatter takes precedence when both hooks are declared, here as in ToString above, or
        // interpolation and ToString(format, provider) would write two different texts.
        if (model.HasTryFormatHook && !model.HasFormatHook)
        {
            writer.Line($"{value} current = Value;");
            writer.Line($"return TryFormatValue(in current, destination, out charsWritten, format, provider ?? {Invariant});");
        }
        else if (underlying.IsSpanFormattable && !model.HasFormatHook)
        {
            var format = underlying.RoundTripFormat is null
                ? "format"
                : $"format.IsEmpty ? global::System.MemoryExtensions.AsSpan({LiteralFactory.Quote(underlying.RoundTripFormat)}) : format";
            writer.Line($"{value} current = Value;");
            writer.Line($"return {Abstractions}.UnderlyingValue.TryFormat(in current, destination, out charsWritten, {format}, provider ?? {Invariant});");
        }
        else
        {
            if (model.HasFormatHook)
            {
                // Interpolation and every span-based writer come through here: the hook decides for them too.
                writer.Line($"{value} current = Value;");
                writer.Line($"string text = FormatValue(in current, format, provider ?? {Invariant});");
            }
            else
            {
                writer.Line(underlying.IsString ? "string text = Value;" : $"string text = ToString(null, provider ?? {Invariant});");
            }

            writer.Open("if (global::System.MemoryExtensions.AsSpan(text).TryCopyTo(destination))");
            writer.Line("charsWritten = text.Length;");
            writer.Line("return true;");
            writer.Close();
            writer.Line();
            writer.Line("charsWritten = 0;");
            writer.Line("return false;");
        }

        writer.Close();
        writer.Line();
    }

    /// <summary>
    /// Emits <c>ToString(format, provider)</c> through a span formatting hook.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hook writes into a stack buffer first, and into a pooled one twice as large each time it answers that the
    /// destination is too small, as the framework contract has it. The growth stops at
    /// <see cref="MaxFormattedLength"/> characters, which no text of a value object reaches: a hook still refusing
    /// then is one that never succeeds, and an exception says so rather than looping or writing another text.
    /// </para>
    /// <para>
    /// A string value object whose hook writes its value unchanged, as an IBAN's default format does, gets back the
    /// string it already holds rather than a copy of it, so <c>ToString()</c> allocates nothing.
    /// </para>
    /// <para>
    /// The retry loop is a member of its own that the JIT never inlines, <see cref="PooledFormatMethod"/>. Written
    /// into <c>ToString</c>, it spent the JIT's inlining budget on a path that almost never runs. A small hook was
    /// inlined in full anyway, but a larger one kept calls to what it calls in turn, which cost up to about 3 ns on
    /// every <c>ToString</c>.
    /// </para>
    /// </remarks>
    /// <param name="writer">Sink.</param>
    /// <param name="model">Value object being emitted.</param>
    /// <param name="underlying">Its underlying type.</param>
    private static void EmitHookToString(CodeWriter writer, ValueObjectModel model, UnderlyingType underlying)
    {
        void Return(string span)
        {
            if (!underlying.IsString)
            {
                writer.Line($"return new string({span});");
                return;
            }

            writer.Line($"global::System.ReadOnlySpan<char> text = {span};");
            writer.Line("return global::System.MemoryExtensions.SequenceEqual(text, global::System.MemoryExtensions.AsSpan(current))");
            writer.Line("    ? current");
            writer.Line("    : new string(text);");
        }

        writer.Open("public string ToString(string? format, global::System.IFormatProvider? formatProvider)");
        writer.Line($"{underlying.FullName} current = Value;");
        writer.Line($"global::System.IFormatProvider provider = formatProvider ?? {Invariant};");
        writer.Line($"global::System.Span<char> buffer = stackalloc char[{Math.Max(underlying.FormatBufferSize, 64)}];");
        writer.Open("if (TryFormatValue(in current, buffer, out int written, global::System.MemoryExtensions.AsSpan(format), provider))");
        Return("buffer[..written]");
        writer.Close();
        writer.Line();
        writer.Line($"return {PooledFormatMethod}(current, format, provider, buffer.Length * 2);");
        writer.Close();
        writer.Line();

        // Kept out of ToString, so that the JIT spends its inlining budget on the hook above, not on the retry.
        writer.Line("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        writer.Open($"private static string {PooledFormatMethod}({underlying.FullName} current, string? format, global::System.IFormatProvider provider, int length)");
        writer.Line("// The hook needs more room than the stack gives it: hand it a pooled buffer twice as large each time.");
        writer.Open($"for (; length <= {MaxFormattedLength}; length *= 2)");
        writer.Line("char[] rented = global::System.Buffers.ArrayPool<char>.Shared.Rent(length);");
        writer.Open("try");
        writer.Open("if (TryFormatValue(in current, rented, out int written, global::System.MemoryExtensions.AsSpan(format), provider))");
        Return("new global::System.ReadOnlySpan<char>(rented, 0, written)");
        writer.Close();
        writer.Close();
        writer.Open("finally");
        writer.Line("global::System.Buffers.ArrayPool<char>.Shared.Return(rented);");
        writer.Close();
        writer.Close();
        writer.Line();
        writer.Line("throw new global::System.FormatException(");
        writer.Line($"    $\"The formatting hook of {model.TypeName} wrote no text for the format '{{format}}' in {MaxFormattedLength} characters. \"");
        writer.Line("    + \"TryFormatValue returns false only when the destination is too small.\");");
        writer.Close();
    }

    private static void EmitParsing(CodeWriter writer, ValueObjectModel model, UnderlyingType underlying, string value, string self)
    {
        // The exception carries the rule that rejected the text, as TryParse reports it: a closed set refusing a
        // value says not_a_known_value, and not_parsable is left to text that is not of the underlying type at all.
        // Its message names the type and the rule, never the text, as Create's does: a message is what every log
        // records.
        EmitParsingDocumentation(writer, underlying);
        writer.Open($"public static {self} Parse(global::System.ReadOnlySpan<char> s, global::System.IFormatProvider? provider)");
        writer.Open($"if (TryParse(s, provider, out {self} result, out {ValidationResult} validation))");
        writer.Line("return result;");
        writer.Close();
        writer.Line();
        writer.Line($"throw new {Abstractions}.ValueObjectException(");
        writer.Line($"    \"'{model.TypeName}' rejected the supplied text: \" + validation.ErrorMessage,");
        writer.Line($"    typeof({self}),");
        writer.Line("    validation.ErrorCode,");
        writer.Line($"    {AttemptedValue(model, "s.ToString()")});");
        writer.Close();
        writer.Line();

        EmitParsingDocumentation(writer, underlying);
        writer.Line(Inline);
        writer.Line($"public static {self} Parse(string s, global::System.IFormatProvider? provider) => Parse(global::System.MemoryExtensions.AsSpan(s), provider);");
        writer.Line();

        writer.Line("/// <inheritdoc cref=\"Parse(string, global::System.IFormatProvider?)\" />");
        writer.Line(Inline);
        writer.Line($"public static {self} Parse(string s) => Parse(global::System.MemoryExtensions.AsSpan(s), null);");
        writer.Line();

        EmitParsingDocumentation(writer, underlying);
        writer.Open($"public static bool TryParse(global::System.ReadOnlySpan<char> s, global::System.IFormatProvider? provider, out {self} result)");

        if (underlying.IsString)
        {
            writer.Line(model.NormalizesFromSpan
                ? $"return TryCreateFrom(s, out result, out {ValidationResult} ignored);"
                : "return TryCreate(s.ToString(), out result);");
        }
        else if (underlying.Kind == UnderlyingKind.Char)
        {
            writer.Open("if (s.Length == 1)");
            writer.Line("return TryCreate(s[0], out result);");
            writer.Close();
            writer.Line();
            writer.Line("result = default;");
            writer.Line("return false;");
        }
        else if (underlying.Kind == UnderlyingKind.Boolean)
        {
            writer.Open("if (bool.TryParse(s, out bool raw))");
            writer.Line("return TryCreate(raw, out result);");
            writer.Close();
            writer.Line();
            writer.Line("result = default;");
            writer.Line("return false;");
        }
        else
        {
            writer.Open($"if ({ParseUnderlying(underlying, value, $"out {value} raw")})");
            writer.Line("return TryCreate(raw, out result);");
            writer.Close();
            writer.Line();
            writer.Line("result = default;");
            writer.Line("return false;");
        }

        writer.Close();
        writer.Line();

        EmitParsingDocumentation(writer, underlying);
        writer.Open($"public static bool TryParse(global::System.ReadOnlySpan<char> s, global::System.IFormatProvider? provider, out {self} result, out {ValidationResult} validation)");

        if (underlying.IsString)
        {
            writer.Line(model.NormalizesFromSpan
                ? "return TryCreateFrom(s, out result, out validation);"
                : "return TryCreate(s.ToString(), out result, out validation);");
        }
        else
        {
            var parse = underlying.Kind switch
            {
                UnderlyingKind.Char => "s.Length == 1",
                UnderlyingKind.Boolean => "bool.TryParse(s, out raw)",
                _ => ParseUnderlying(underlying, value, "out raw"),
            };

            writer.Line($"{value} raw = default;");
            writer.LineIf(underlying.Kind == UnderlyingKind.Char, "raw = s.Length == 1 ? s[0] : default;");
            writer.Open($"if (!({parse}))");
            writer.Line("result = default;");
            writer.Line($"validation = {ValidationResult}.Failure(");
            writer.Line($"    {ErrorCodes}.NotParsable,");
            writer.Line($"    \"The text is not a valid {underlying.Keyword}.\");");
            writer.Line("return false;");
            writer.Close();
            writer.Line();
            writer.Line("return TryCreate(raw, out result, out validation);");
        }

        writer.Close();
        writer.Line();

        EmitParsingDocumentation(writer, underlying);
        writer.Open($"public static bool TryParse(string? s, global::System.IFormatProvider? provider, out {self} result)");
        if (underlying.IsString)
        {
            writer.Line("return TryCreate(s!, out result);");
        }
        else
        {
            writer.Open("if (s is null)");
            writer.Line("result = default;");
            writer.Line("return false;");
            writer.Close();
            writer.Line();
            writer.Line("return TryParse(global::System.MemoryExtensions.AsSpan(s), provider, out result);");
        }

        writer.Close();
        writer.Line();

        writer.Line("/// <inheritdoc cref=\"TryParse(string, global::System.IFormatProvider?, out " + model.CrefName + ")\" />");
        writer.Line(Inline);
        writer.Line($"public static bool TryParse(string? s, out {self} result) => TryParse(s, null, out result);");
        writer.Line();

        writer.Line("/// <inheritdoc cref=\"TryParse(string, global::System.IFormatProvider?, out " + model.CrefName + ")\" />");
        writer.Line(Inline);
        writer.Line($"public static bool TryParse(global::System.ReadOnlySpan<char> s, out {self} result) => TryParse(s, null, out result);");
        writer.Line();
    }

    /// <summary>
    /// Writes the expression reading a bound a value object declares through a hook.
    /// </summary>
    /// <param name="model">The value object.</param>
    /// <param name="bound"><c>Minimum</c> or <c>Maximum</c>.</param>
    /// <returns>The expression.</returns>
    private static string BoundOf(ValueObjectModel model, string bound)
        => $"{Abstractions}.ValueObjectBound.{bound}<{model.QualifiedName}, {model.UnderlyingFullName}>()";

    /// <summary>
    /// Gets the call parsing the text <c>s</c> into the underlying value, in the form ToString writes it.
    /// </summary>
    /// <remarks>
    /// A <see cref="DateTime"/> is written in its round-trip form, which names its kind with a <c>Z</c> or an offset.
    /// Read without <c>RoundtripKind</c>, that suffix turns the value into local time: a UTC value would come back
    /// with another kind, and on a machine outside UTC with other ticks. A <c>decimal</c>, a <c>double</c> or a
    /// <c>float</c> is read in the invariant culture, which a null provider stands for, without the group separator
    /// its own styles accept, a comma there: <c>12,5</c> would otherwise read as 125. Every other type reads its own
    /// form as is.
    /// </remarks>
    /// <param name="underlying">The underlying type, neither a string, a bool nor a char.</param>
    /// <param name="value">Its qualified name.</param>
    /// <param name="output">The out argument receiving the value.</param>
    /// <returns>A boolean expression.</returns>
    private static string ParseUnderlying(UnderlyingType underlying, string value, string output)
        => underlying switch
        {
            { Kind: UnderlyingKind.DateTime } =>
                $"global::System.DateTime.TryParse(s, provider ?? {Invariant}, global::System.Globalization.DateTimeStyles.RoundtripKind, {output})",
            { InvariantNumberStyles: { } styles } =>
                $"{Abstractions}.UnderlyingValue.TryParse<{value}>(s, {styles}, provider, {output})",
            _ => $"{Abstractions}.UnderlyingValue.TryParse<{value}>(s, provider ?? {Invariant}, {output})",
        };

    /// <summary>
    /// Writes the documentation of a member parsing text with a provider: the interface's, and what a null provider
    /// stands for.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="underlying">The underlying type.</param>
    private static void EmitParsingDocumentation(CodeWriter writer, UnderlyingType underlying)
    {
        writer.Line("/// <inheritdoc />");

        // A string, a bool or a char reads no culture, and the provider plays no part.
        if (!underlying.IsSpanFormattable)
        {
            return;
        }

        writer.Line("/// <remarks>");
        writer.Line("/// A <see langword=\"null\"/> provider stands for <see cref=\"global::System.Globalization.CultureInfo.InvariantCulture\"/>,");
        writer.Line("/// not for the current culture, here as in <c>ToString</c> and <c>TryFormat</c>.");
        if (underlying.InvariantNumberStyles is not null)
        {
            writer.Line("/// With a <see langword=\"null\"/> provider or the invariant culture, the text takes no group separator:");
            writer.Line("/// <c>12,5</c> and <c>1,234.5</c> are refused as <c>value_object.not_parsable</c>. Any other culture reads the");
            writer.Line($"/// text with the number styles of <c>{underlying.Keyword}</c>, its group separator included.");
        }

        writer.Line("/// </remarks>");
    }

    private static void EmitConversions(CodeWriter writer, ValueObjectModel model, string value, string self)
    {
        if (model.ImplicitConversionToValue)
        {
            writer.Line("/// <summary>Reads the underlying value.</summary>");
            writer.Line(Inline);
            writer.Line($"public static implicit operator {value}({self} valueObject) => valueObject.Value;");
            writer.Line();
        }

        if (!model.ExplicitConversionFromValue)
        {
            return;
        }

        writer.Line("/// <summary>Creates a value object, validating the value.</summary>");
        writer.Line(Inline);
        writer.Line($"public static explicit operator {self}({value} value) => Create(value);");
        writer.Line();
    }

    private static void EmitArithmetic(CodeWriter writer, ValueObjectModel model, UnderlyingType underlying, string value, string self)
    {
        if (!model.Arithmetic)
        {
            return;
        }

        // The narrow integer types promote to int under arithmetic, so the result needs a cast back. Every
        // integral operation is checked: a value object that silently wraps around is a value object that lies.
        var needsCast = underlying.Kind is UnderlyingKind.SByte or UnderlyingKind.Byte
            or UnderlyingKind.Int16 or UnderlyingKind.UInt16;
        var isIntegral = underlying.IsNumeric && underlying.Kind
            is not (UnderlyingKind.Decimal or UnderlyingKind.Double or UnderlyingKind.Single);

        string Result(string expression)
        {
            var cast = needsCast ? $"({underlying.Keyword})({expression})" : expression;
            return isIntegral ? $"checked({cast})" : cast;
        }

        EmitNumericConstants(writer, underlying, value, self, Result);

        writer.Line("/// <summary>Adds two values, validating the result.</summary>");
        writer.Line($"public static {self} operator +({self} left, {self} right) => Create({Result("left.Value + right.Value")});");
        writer.Line();

        writer.Line("/// <summary>Subtracts two values, validating the result.</summary>");
        writer.Line($"public static {self} operator -({self} left, {self} right) => Create({Result("left.Value - right.Value")});");
        writer.Line();

        writer.Line("/// <summary>Scales a value, validating the result.</summary>");
        writer.Line($"public static {self} operator *({self} left, {value} right) => Create({Result("left.Value * right")});");
        writer.Line();

        writer.Line("/// <summary>Scales a value, validating the result.</summary>");
        writer.Line($"public static {self} operator *({value} left, {self} right) => Create({Result("left * right.Value")});");
        writer.Line();

        writer.Line("/// <summary>Divides a value by a scalar, validating the result.</summary>");
        writer.Line($"public static {self} operator /({self} left, {value} right) => Create({Result("left.Value / right")});");
        writer.Line();

        writer.Line("/// <summary>Divides two values into a bare ratio, which carries no unit and is therefore not a value object.</summary>");
        writer.Line($"public static {value} operator /({self} left, {self} right) => {Result("left.Value / right.Value")};");
        writer.Line();

        if (!underlying.IsSigned)
        {
            return;
        }

        writer.Line("/// <summary>Negates a value, validating the result.</summary>");
        writer.Line($"public static {self} operator -({self} value) => Create({Result("-value.Value")});");
        writer.Line();
    }

    /// <summary>
    /// Emits concrete numeric helpers on the type itself.
    /// </summary>
    /// <remarks>
    /// <see cref="INumericValueObject{TSelf, TValue}"/> declares these as static virtual members with a default
    /// implementation, which C# only lets you reach through a type parameter. Emitting them here makes
    /// <c>Amount.Zero</c> and <c>Amount.Max(a, b)</c> ordinary code while still satisfying the interface, so the
    /// same value object serves both direct use and generic math.
    /// </remarks>
    private static void EmitNumericConstants(
        CodeWriter writer,
        UnderlyingType underlying,
        string value,
        string self,
        Func<string, string> result)
    {
        var bridge = $"{Abstractions}.UnderlyingValue";

        writer.Line("/// <summary>Gets the value representing zero.</summary>");
        writer.Line($"public static {self} Zero => Create({bridge}.Zero<{value}>());");
        writer.Line();

        writer.Line("/// <summary>Gets the value representing one.</summary>");
        writer.Line($"public static {self} One => Create({bridge}.One<{value}>());");
        writer.Line();

        writer.Line("/// <summary>Gets a value indicating whether the carried value is zero.</summary>");
        writer.Line($"public bool IsZero => {bridge}.IsZero(Value);");
        writer.Line();

        writer.Line("/// <summary>Returns the absolute value, validating the result.</summary>");
        writer.Line(underlying.IsSigned
            ? $"public static {self} Abs({self} value) => Create({bridge}.Abs(value.Value));"
            : $"public static {self} Abs({self} value) => value;");
        writer.Line();

        writer.Line("/// <summary>Returns the smaller of two values.</summary>");
        writer.Line($"public static {self} Min({self} left, {self} right) => left.CompareTo(right) <= 0 ? left : right;");
        writer.Line();

        writer.Line("/// <summary>Returns the larger of two values.</summary>");
        writer.Line($"public static {self} Max({self} left, {self} right) => left.CompareTo(right) >= 0 ? left : right;");
        writer.Line();

        writer.Line("/// <summary>Adds up a sequence, accumulating on the underlying type and validating once at the end.</summary>");
        writer.Open($"public static {self} Sum(global::System.Collections.Generic.IEnumerable<{self}> values)");
        writer.Line("global::System.ArgumentNullException.ThrowIfNull(values);");
        writer.Line();
        writer.Line($"{value} total = {bridge}.Zero<{value}>();");
        writer.Open($"foreach ({self} current in values)");
        writer.Line($"total = {result("total + current.Value")};");
        writer.Close();
        writer.Line();
        writer.Line("return Create(total);");
        writer.Close();
        writer.Line();
    }

    /// <summary>
    /// Writes author text into a one-line XML documentation comment.
    /// </summary>
    /// <remarks>
    /// A line break would end the comment and leave the rest of the text as code, so every character C# ends a
    /// line at is folded into a space, a CR LF pair into one. So are the other characters XML cannot hold, which a
    /// project generating its documentation file would report as malformed: the other control characters,
    /// U+FFFE, U+FFFF, and half of a surrogate pair. A whole pair stands for one character and is kept.
    /// </remarks>
    /// <param name="text">Text written by the author.</param>
    /// <returns>The text, escaped and on one line.</returns>
    internal static string Xml(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            switch (character)
            {
                case '&':
                    builder.Append("&amp;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '>':
                    builder.Append("&gt;");
                    break;
                case '\r' when i + 1 < text.Length && text[i + 1] == '\n':
                    // The line feed that follows becomes the space.
                    break;
                case '\u0085' or '\u2028' or '\u2029' or '\uFFFE' or '\uFFFF':
                    builder.Append(' ');
                    break;
                case >= '\uD800' and <= '\uDBFF' when i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]):
                    builder.Append(character).Append(text[++i]);
                    break;
                default:
                    builder.Append((character < ' ' && character != '\t') || char.IsSurrogate(character) ? ' ' : character);
                    break;
            }
        }

        return builder.ToString();
    }
}
