using System.Collections.Frozen;
using System.Reflection;
using AdCodicem.ValueObjects.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Maps value objects onto their underlying database types.
/// </summary>
public static class ValueObjectConventionExtensions
{
    /// <summary>
    /// Maps every value object declared in the given assemblies to its underlying column type.
    /// </summary>
    /// <param name="builder">Model configuration builder, from <c>DbContext.ConfigureConventions</c>.</param>
    /// <param name="assemblies">
    /// Assemblies declaring the value objects. When none is given, everything already registered is mapped,
    /// which is enough as soon as the entity assembly has been loaded.
    /// </param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// A value object declaring a maximum length also sizes its column, so an IBAN lands in <c>varchar(34)</c>
    /// rather than an unbounded column — the rule is stated once, on the type, and the schema follows.
    /// </para>
    /// <para>
    /// A value object over <see cref="Int128"/> or <see cref="UInt128"/> is left alone: Entity Framework Core maps
    /// neither type, on any provider, so the column type, and the conversion to it, are the application's to choose
    /// with a converter of its own.
    /// </para>
    /// <para>
    /// A generic value object, <c>Code&lt;T&gt;</c> or <c>Outer&lt;T&gt;.Code</c>, is mapped too, whatever its
    /// constructions: each property of one gets the converter, the comparer and the length closed over its own.
    /// </para>
    /// <para>
    /// A property holding a collection of value objects, <c>List&lt;Iban&gt;</c>, <c>Quantity[]</c> or
    /// <c>IReadOnlyList&lt;Iban?&gt;</c>, in an entity type, an owned type or a complex type, is mapped as a primitive
    /// collection: each element gets the converter, the comparer and the length of its value object, so that an IBAN
    /// lands in a <c>character varying(34)[]</c> on PostgreSQL, and in a JSON array elsewhere, which SQL Server reads as
    /// <c>nvarchar(34)</c>. Only a property Entity Framework Core would map itself, one with a public getter and a setter,
    /// is made one, unless the application converts the whole collection itself; a primitive collection the application
    /// declares, over a read-only property, gets its elements configured too, unless the application set their converter.
    /// </para>
    /// <para>
    /// Writing refuses a value the value object rejects, which only an instance equal to <c>default(TSelf)</c> can hold,
    /// rather than store it for a read to trust: a property of the value object's type throws a
    /// <see cref="ValueObjectException"/>, and a property of its nullable type, <c>TSelf?</c>, stores a <c>NULL</c>. An
    /// element of a collection does the same, but for an element of <c>TSelf?</c> over a value type, which throws too:
    /// Entity Framework Core cannot write the <c>null</c> there.
    /// </para>
    /// <para>
    /// A primary key of one property over a <see cref="Guid"/> value object, which is no foreign key and which nothing
    /// generates, refuses an entity added without it, with an <see cref="InvalidOperationException"/> carrying
    /// <see cref="ValueObjectErrorCodes.Required"/>, whatever the value object's rules, rather than store it with
    /// <see cref="Guid.Empty"/>, a value a value object without a rule against it accepts. Have the key generated with
    /// <see cref="HasValueObjectKeyGeneration{TSelf}(PropertyBuilder{TSelf})"/>, or written as it stands with
    /// <c>ValueGeneratedNever()</c>; the refusal changes neither the column nor the migration snapshot (see
    /// <see cref="UnassignedValueObjectKeyGeneratorFactory"/>).
    /// </para>
    /// <para>
    /// The columns can also carry the rules as CHECK constraints, on request: see
    /// <see cref="AddValueObjectCheckConstraints(ModelConfigurationBuilder, Action{ValueObjectCheckOptions})"/>.
    /// </para>
    /// <para>
    /// A compiled model, which <c>dotnet ef dbcontext optimize</c> generates, holds the same mapping. A property of
    /// <c>TSelf?</c> is compared by <see cref="NullableValueObjectComparer{TSelf}"/>, which such a model can write.
    /// </para>
    /// <para>
    /// What it configures is decided once, while the model is built. Nothing here happens per query or per row, but for
    /// the generator of a key, which Entity Framework Core creates once and asks for a value when an entity is added.
    /// </para>
    /// <para>
    /// Each converter and comparer is closed over its value object through the type arguments the descriptor hands back
    /// to a visitor (<see cref="ValueObjectDescriptor.Accept{TResult}(IValueObjectVisitor{TResult})"/>), never with
    /// <see cref="Type.MakeGenericType(Type[])"/> over a <see cref="Type"/> read off the descriptor. The converter of a
    /// <c>TSelf?</c> property, or of such an element over text, which C# cannot name without a constraint the visitor
    /// does not carry, is closed with it over those type arguments. Entity Framework Core builds no model under native
    /// AOT, where it reads a compiled model instead, so none of this runs there.
    /// </para>
    /// </remarks>
    public static ModelConfigurationBuilder ConfigureValueObjects(
        this ModelConfigurationBuilder builder,
        params Assembly[] assemblies)
        => builder.ConfigureValueObjects(strict: false, assemblies);

    /// <summary>
    /// Maps every value object declared in the given assemblies, optionally re-validating on read.
    /// </summary>
    /// <param name="builder">Model configuration builder, from <c>DbContext.ConfigureConventions</c>.</param>
    /// <param name="strict">
    /// When <see langword="true"/>, values read from the database are normalized and validated again, the elements of a
    /// collection included. Use it for tables another system also writes to; it costs a validation per materialized
    /// value.
    /// </param>
    /// <param name="assemblies">Assemblies declaring the value objects.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// <paramref name="strict"/> is read once per context type, not per instance: Entity Framework Core builds the
    /// model the first time a context type is used and caches it for every later instance. A context that chooses the
    /// value from a constructor argument gets whichever model was built first. Give strict reads a context type of
    /// their own, or an <see cref="Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory"/> that puts the
    /// choice in the cache key.
    /// </remarks>
    public static ModelConfigurationBuilder ConfigureValueObjects(
        this ModelConfigurationBuilder builder,
        bool strict,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
#pragma warning disable IL2026 // Model building is reflection-based already; EF Core is not trim compatible.
            ValueObjectRegistry.EnsureAssemblyRegistered(assembly);
#pragma warning restore IL2026
        }

        var mapped = new Dictionary<Type, ValueObjectDescriptor>();
        foreach (var descriptor in ValueObjectRegistry.GetRegistered())
        {
            // The length is the descriptor's: a registration made by hand may give a value object another schema than
            // the one its type declares.
            if (descriptor.Accept(new PropertiesConfiguration(builder, strict, descriptor.Schema.MaxLength)))
            {
                mapped.Add(descriptor.ValueObjectType, descriptor);
            }
        }

        // A generic value object is configured by its definition, which makes each construction a scalar property, and
        // the convention closes the converter over the construction of each property it meets.
        var definitions = ValueObjectRegistry.GetRegisteredGenericDefinitions()
            .Where(static definition => !Is128Bit(ValueTypeOf(definition)))
            .ToHashSet();
        if (definitions.Count > 0)
        {
            foreach (var definition in definitions)
            {
                builder.Properties(definition);
            }

            builder.Conventions.Add(_ => new GenericValueObjectConvention(definitions, strict));
        }

        // A collection of value objects, mapped or of a generic definition, is a primitive collection whose elements get
        // the converter, the comparer and the length of their value object.
        builder.Conventions.Add(_ => new ValueObjectCollectionConvention(mapped, definitions, strict));

        // A primary key over a Guid value object that nothing generates refuses an entity added without it.
        builder.Conventions.Add(static _ => new ValueObjectKeyConvention());

        return builder;
    }

    /// <summary>
    /// Maps a single property to the underlying value of its value object.
    /// </summary>
    /// <typeparam name="TSelf">Value object type.</typeparam>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="builder">Property builder.</param>
    /// <param name="strict">When <see langword="true"/>, values read from the database are validated again.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// Use this for a property that needs to depart from the convention; otherwise prefer the convention. It cannot
    /// map a value object over <see cref="Int128"/> or <see cref="UInt128"/>, which Entity Framework Core maps to no
    /// column: such a property needs a converter of the application's own. Writing refuses a value the value object
    /// rejects with a <see cref="ValueObjectException"/>, as the convention does. A primary key over a
    /// <see cref="Guid"/> value object is refused unset only under <c>ConfigureValueObjects</c>, whose convention does
    /// it; <c>HasValueObjectKeyGeneration()</c> generates a key over a <see cref="Guid"/> or an integer either way.
    /// </remarks>
    public static PropertyBuilder<TSelf> HasValueObjectConversion<TSelf, TValue>(
        this PropertyBuilder<TSelf> builder,
        bool strict = false)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasConversion(
            strict
                ? new StrictValueObjectConverter<TSelf, TValue>()
                : new ValueObjectConverter<TSelf, TValue>(),
            new ValueObjectComparer<TSelf>());

        // The length the value object declares, read off the type rather than out of the registry, which would describe
        // a construction of a generic value object by reflection.
        if (TSelf.Schema.MaxLength is { } maxLength)
        {
            builder.HasMaxLength(maxLength);
        }

        return builder;
    }

    /// <summary>
    /// Generates a key over a <see cref="Guid"/> when an entity is added, with GUIDs the database keeps in order: SQL
    /// Server's sequential ones on SQL Server, as Entity Framework Core generates for a <see cref="Guid"/> key there,
    /// and version 7 ones on every other provider, as Npgsql generates for PostgreSQL.
    /// </summary>
    /// <typeparam name="TSelf">Value object type, over <see cref="Guid"/>.</typeparam>
    /// <param name="builder">Property builder of the key.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// <para>
    /// The key becomes generated on add, as a <see cref="Guid"/> key is by convention, and gets the generator of
    /// <see cref="ValueObjectKeyGeneratorFactory{TSelf, TValue}"/>, which a compiled model can write.
    /// <c>ValueGeneratedOnAdd()</c> alone would not do: Entity Framework Core would then wrap its random generator in a
    /// conversion on every provider, losing the order SQL Server's clustered index and PostgreSQL's version 7 keep. On
    /// SQLite and the other providers, where Entity Framework Core generates a random GUID for a <see cref="Guid"/>
    /// key, the key is a version 7 one all the same.
    /// </para>
    /// <para>
    /// A key the domain set is kept: Entity Framework Core asks a generator only for a key still at its default,
    /// <c>default(TSelf)</c>, which a value object holding <see cref="Guid.Empty"/> equals, so that one is generated over.
    /// The GUID goes through the value object's <c>Create</c>, so a rule it breaks fails the add with a
    /// <see cref="ValueObjectException"/>. <c>Attach</c> or <c>Update</c> of an entity whose key is unset adds it, as for a
    /// <see cref="Guid"/> key. The key is generated in the application: a default the column declares is not used.
    /// </para>
    /// <para>
    /// It works with or without <c>ConfigureValueObjects</c>, beside
    /// <see cref="HasValueObjectConversion{TSelf, TValue}(PropertyBuilder{TSelf}, bool)"/>. A key over a
    /// <see cref="short"/>, an <see cref="int"/> or a <see cref="long"/> is generated by the database, through the method
    /// of the same name of <see cref="ValueObjectInt16KeyGenerationExtensions"/>,
    /// <see cref="ValueObjectInt32KeyGenerationExtensions"/> and <see cref="ValueObjectInt64KeyGenerationExtensions"/>,
    /// which a call picks by the key's type; a value object over any other type is a compile error.
    /// </para>
    /// </remarks>
    public static PropertyBuilder<TSelf> HasValueObjectKeyGeneration<TSelf>(this PropertyBuilder<TSelf> builder)
        where TSelf : struct, IValueObject<TSelf, Guid>
        => KeyGeneration.Configure<TSelf, Guid>(builder);

    /// <summary>
    /// Writes the rules value objects declare as CHECK constraints on the columns this package maps them to, so that the
    /// database refuses a row another writer stores with a value the type rejects.
    /// </summary>
    /// <param name="builder">Model configuration builder, from <c>DbContext.ConfigureConventions</c>.</param>
    /// <param name="configure">
    /// Chooses the rules and the dialect of patterns; when it is not given, lengths, bounds and closed value sets are
    /// written, and no pattern.
    /// </param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="configure"/> names a dialect of patterns <see cref="ValueObjectPatternDialect"/> does not define.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Off unless called. Each column one of this package's converters maps, through <c>ConfigureValueObjects</c>,
    /// <c>ConfigureEntityIds</c> or one property at a time, gets a constraint per rule its value object declares:
    /// <c>MinLength</c>, the bounds of <see cref="IValueObjectMinimum{TValue}"/> and
    /// <see cref="IValueObjectMaximum{TValue}"/>, and the known values of a closed value set. A migration picks them up,
    /// so a rule changed on the type reaches the next migration, as a column size does; a table already holding a row the
    /// rule refuses fails that migration.
    /// </para>
    /// <para>
    /// A constraint is laxer than the type where the database cannot say the rule as .NET reads it, never stricter: a
    /// length is counted in bytes where the database cannot count UTF-16 code units, and a bound or a closed set is left
    /// out where the column does not compare values as .NET does. A rule only code checks, in
    /// <see cref="IValueObjectValidator{TValue}"/>, stays in the application. SQL Server, PostgreSQL and SQLite are known;
    /// another provider gets no constraint. A column mapped to JSON, the elements of a primitive collection, and a
    /// property mapped by a converter of the application's own get none either.
    /// </para>
    /// <para>
    /// A pattern is written only when <see cref="ValueObjectCheckOptions.Patterns"/> names the dialect of the database,
    /// which the model cannot tell: <c>REGEXP_LIKE</c> on SQL Server 2025, <c>~</c> on PostgreSQL, none on SQLite. Naming
    /// one fails the model, with an <see cref="InvalidOperationException"/>, on a pattern the dialect would read apart from
    /// .NET, unless <see cref="ValueObjectCheckOptions.ExcludePattern{TSelf}"/> leaves that value object's pattern out.
    /// </para>
    /// <para>
    /// The application's own writes pass, since the value objects it writes went through their rules. A constraint refuses
    /// what another writer stores, and an invalid instance a lax read let in and the application writes back, with the
    /// provider's error inside a <see cref="DbUpdateException"/> for a write through Entity Framework Core: it complements
    /// <c>strict: true</c>, which refuses on read a row stored before the constraint existed. Each constraint's name ends
    /// with its rule, which
    /// <see cref="ValueObjectDbContextOptionsBuilderExtensions.UseValueObjectCheckErrors(DbContextOptionsBuilder)"/>, on
    /// the options of the context, adds to that error as the code <c>Validate</c> reports for the rule, and
    /// <see cref="ValueObjectCheckConstraints.TryGetCode"/> reads without it.
    /// </para>
    /// <para>
    /// The options are read once per context type, as <c>strict</c> is: Entity Framework Core builds the model the first
    /// time a context type is used and caches it for every later instance. A second call replaces the first, whose
    /// options then apply nowhere, so that a context deriving from another can call it again with options of its own. The
    /// constraints are part of the model a migration, <c>EnsureCreated</c> or a script is made from, never of a compiled
    /// model, which reads none: nothing here runs per query or per row.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    ///     => builder
    ///         .ConfigureValueObjects(typeof(Iban).Assembly)
    ///         .AddValueObjectCheckConstraints(checks => checks.Patterns = ValueObjectPatternDialect.PostgreSql);
    /// </code>
    /// </example>
    public static ModelConfigurationBuilder AddValueObjectCheckConstraints(
        this ModelConfigurationBuilder builder,
        Action<ValueObjectCheckOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new ValueObjectCheckOptions();
        configure?.Invoke(options);
        if (!Enum.IsDefined(options.Patterns))
        {
            throw new ArgumentOutOfRangeException(
                nameof(configure),
                options.Patterns,
                "The dialect of patterns is none of those ValueObjectPatternDialect defines.");
        }

        // Read now: the convention is created each time a model is built, and the options are not the caller's to change
        // afterwards.
        var rules = options.Rules;
        var patterns = options.Patterns;
        var exclusions = options.PatternExclusions.ToFrozenSet();

        // A second call replaces the first, as a second strict does: beside it, the first convention would still write
        // and check what its options name, and the second would write every constraint again under a hashed name.
        builder.Conventions.Remove<ValueObjectCheckConstraintConvention>();
        builder.Conventions.Add(services => new ValueObjectCheckConstraintConvention(services, rules, patterns, exclusions));

        return builder;
    }

    private static bool Is128Bit(Type? valueType) => valueType == typeof(Int128) || valueType == typeof(UInt128);

    /// <summary>
    /// Reads the underlying type of a generic value object off its definition, which the registry describes no
    /// construction of.
    /// </summary>
    private static Type? ValueTypeOf(Type definition)
        => definition.GetInterfaces()
            .FirstOrDefault(static candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IValueObject<,>))
            ?.GetGenericArguments()[1];

    /// <summary>
    /// Configures the properties of the value object a descriptor stands for, and those of its nullable type, closed
    /// over the type arguments the descriptor hands back.
    /// </summary>
    /// <param name="builder">Model configuration builder.</param>
    /// <param name="strict">Whether what is read is validated again.</param>
    /// <param name="maxLength">The length the descriptor declares, which sizes the column.</param>
    private sealed class PropertiesConfiguration(ModelConfigurationBuilder builder, bool strict, int? maxLength)
        : IValueObjectVisitor<bool>
    {
        /// <summary>
        /// Configures the properties, unless the value object is over a 128-bit integer, which Entity Framework Core
        /// maps to no column.
        /// </summary>
        /// <typeparam name="TSelf">Value object type.</typeparam>
        /// <typeparam name="TValue">Underlying value type.</typeparam>
        /// <returns><see langword="true"/> when the properties were configured.</returns>
        public bool Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
        {
            if (Is128Bit(typeof(TValue)))
            {
                return false;
            }

            var converter = ConverterTypes.Required<TSelf, TValue>(strict);
            var properties = builder.Properties<TSelf>();
            properties.HaveConversion(converter, typeof(ValueObjectComparer<TSelf>));

            if (maxLength is { } length)
            {
                properties.HaveMaxLength(length);
            }

            // A TSelf? property takes the configuration of TSelf, the length included, then its own: a converter
            // storing a value the value object rejects as NULL, where the column of a TSelf throws (a value object
            // written by hand over a reference type other than string keeps its own), and a comparer of the nullable
            // type, which a compiled model can write, where it cannot write the wrapping EF Core would otherwise give
            // ValueObjectComparer.
            builder.Properties<TSelf?>().HaveConversion(
                ConverterTypes.Optional<TSelf, TValue>(strict) ?? converter,
                typeof(NullableValueObjectComparer<TSelf>));

            return true;
        }
    }
}
