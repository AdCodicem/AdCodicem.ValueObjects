using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AdCodicem.ValueObjects.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Writes the rules of the value object each column holds as CHECK constraints, once the model is complete.
/// </summary>
/// <remarks>
/// <para>
/// A column is constrained when one of this package's converters maps its property, whichever call set it:
/// <c>ConfigureValueObjects</c>, <c>ConfigureEntityIds</c>, <c>HasValueObjectConversion</c> or
/// <c>HasEntityIdConversion</c>. A converter of the application's own is left alone, since it may not store the value
/// the rules speak of. The rules are those the type declares, <c>TSelf.Schema</c>, which its <c>Validate</c> enforces,
/// read through the type arguments the descriptor hands back to <see cref="CheckConstraintRules"/>.
/// </para>
/// <para>
/// The columns of an entity type's table are constrained, those of an owned type or a complex type that shares it
/// included. A type mapped to JSON, a complex collection and the elements of a primitive collection live in a JSON
/// document or an array, and get none; a view, a property split into another table, and a type of a table-per-concrete-
/// type hierarchy that other types derive from get none either: Entity Framework Core copies a constraint of such a type
/// into the table of every type derived from it under one name, which SQL Server refuses twice in a schema. A type with
/// no derived type in that hierarchy constrains every property it holds, its inherited ones included, but for a complex
/// property it inherits, which Entity Framework Core 10 maps to no column of its table. A column several properties
/// share, those of two types of a table-per-hierarchy, is constrained only when the package maps each of them through
/// the same value object: a constraint of one would apply to the rows of the other.
/// </para>
/// <para>
/// Each constraint is named <c>CK_{table}_{column}_{rule}</c>, the rule being <c>MinLength</c>, <c>Minimum</c>,
/// <c>Maximum</c>, <c>KnownValues</c> or <c>Pattern</c>. A name longer than the provider takes, 63 bytes on PostgreSQL,
/// 128 characters on SQL Server, a name another check constraint of the model already holds, one the application
/// declared, in the same table, or in the schema on SQL Server, and on SQL Server, where a constraint name is unique in
/// its schema, a name another table of the schema also takes, <c>CK_a_b_C_Maximum</c> for column <c>C</c> of table
/// <c>a_b</c> and column <c>B_C</c> of table <c>a</c>, keeps the rule whole and ends with eight hexadecimal digits of the
/// SHA-256 of the table and the column, its start cut as short as the provider needs:
/// <c>CK_{table}_{column}_{rule}_{hash}</c>, which <see cref="ValueObjectCheckConstraints"/> reads back to the code of
/// the rule. A column that takes a <c>NULL</c> wraps the check: <c>[c] IS NULL OR (…)</c>.
/// </para>
/// <para>
/// SQL Server, PostgreSQL and SQLite are known; another provider gets no constraint. Nothing here runs per query or per
/// row: a compiled model holds no check constraint, and a migration, <c>EnsureCreated</c> or a script builds the model
/// that does, under the JIT.
/// </para>
/// </remarks>
/// <param name="services">The services of the context whose model is built.</param>
/// <param name="rules">The rules written.</param>
/// <param name="patterns">The dialect a pattern is written in.</param>
/// <param name="exclusions">The value objects whose pattern is left out.</param>
internal sealed class ValueObjectCheckConstraintConvention(
    IServiceProvider services,
    ValueObjectChecks rules,
    ValueObjectPatternDialect patterns,
    IReadOnlySet<Type> exclusions)
    : IModelFinalizingConvention
{
    /// <inheritdoc />
    public void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        CheckEngine? engine = services.GetRequiredService<IDatabaseProvider>().Name switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" => CheckEngine.SqlServer,
            "Npgsql.EntityFrameworkCore.PostgreSQL" => CheckEngine.PostgreSql,
            "Microsoft.EntityFrameworkCore.Sqlite" => CheckEngine.Sqlite,
            _ => null,
        };
        if (engine is not { } known)
        {
            return;
        }

        var model = modelBuilder.Metadata;
        var writer = new Writer(
            known,
            rules,
            patterns,
            exclusions,
            services.GetRequiredService<IRelationalTypeMappingSource>(),
            services.GetRequiredService<ISqlGenerationHelper>(),
            model.GetMaxIdentifierLength());

        foreach (var entityType in model.GetEntityTypes().ToList())
        {
            if (entityType.IsMappedToJson() || StoreObjectIdentifier.Create(entityType, StoreObjectType.Table) is not { } table)
            {
                continue;
            }

            var concrete = entityType.GetMappingStrategy() == RelationalAnnotationNames.TpcMappingStrategy;
            if (concrete && entityType.GetDirectlyDerivedTypes().Any())
            {
                continue;
            }

            foreach (var property in concrete ? entityType.GetProperties() : entityType.GetDeclaredProperties())
            {
                writer.Collect(entityType, table, property);
            }

            // A complex property a type of a table-per-concrete-type hierarchy inherits has no column in its table:
            // Entity Framework Core 10 maps none there.
            foreach (var complex in entityType.GetDeclaredComplexProperties())
            {
                writer.Walk(entityType, table, complex);
            }
        }

        writer.Constrain(model);
    }

    /// <summary>
    /// Names a constraint, <c>CK_{table}_{column}_{rule}</c>, within what the provider takes, in UTF-8 bytes, and apart
    /// from any other name the database holds where it must be unique.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="column">The column.</param>
    /// <param name="rule">The rule, which the name always ends with, before the hash it may take.</param>
    /// <param name="maxLength">The longest identifier the provider takes.</param>
    /// <param name="distinct">Whether the name is taken elsewhere, and must end with a hash to stay apart.</param>
    /// <returns>
    /// The name, or, when it is longer than the provider takes or taken elsewhere, its start, the rule and eight
    /// hexadecimal digits of the SHA-256 of the table and the column.
    /// </returns>
    internal static string Name(string table, string column, string rule, int maxLength, bool distinct)
    {
        var name = $"CK_{table}_{column}_{rule}";
        if (!distinct && Encoding.UTF8.GetByteCount(name) <= maxLength)
        {
            return name;
        }

        // The lengths keep two tables and columns apart whose names, joined, read alike.
        var key = string.Create(CultureInfo.InvariantCulture, $"{table.Length}:{table}{column.Length}:{column}");
        var end = $"_{rule}_{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)), 0, 4)}";
        var start = $"CK_{table}_{column}";
        var kept = start.Length;
        while (kept > 0 && (Encoding.UTF8.GetByteCount(start.AsSpan(0, kept)) + end.Length > maxLength
                            || char.IsHighSurrogate(start[kept - 1])))
        {
            kept--;
        }

        return string.Concat(start.AsSpan(0, kept), end);
    }

    /// <summary>
    /// Adds the constraints of the columns of the model's tables, once the convention has walked every type mapped to
    /// them.
    /// </summary>
    /// <param name="engine">The database.</param>
    /// <param name="rules">The rules written.</param>
    /// <param name="patterns">The dialect a pattern is written in.</param>
    /// <param name="exclusions">The value objects whose pattern is left out.</param>
    /// <param name="mappings">The type mappings of the provider.</param>
    /// <param name="sql">The SQL helper of the provider, which delimits a column.</param>
    /// <param name="maxLength">The longest identifier the provider takes.</param>
    private sealed class Writer(
        CheckEngine engine,
        ValueObjectChecks rules,
        ValueObjectPatternDialect patterns,
        IReadOnlySet<Type> exclusions,
        IRelationalTypeMappingSource mappings,
        ISqlGenerationHelper sql,
        int maxLength)
    {
        /// <summary>
        /// The properties mapped to each column, each with the value object one of this package's converters maps it
        /// through, or <see langword="null"/> for any other.
        /// </summary>
        private readonly Dictionary<(StoreObjectIdentifier Table, string Column), List<(IConventionEntityType Owner, IConventionProperty Property, Type? ValueObject)>> _columns = [];

        /// <summary>
        /// Collects the columns of a complex type that shares the table, and of those nested in it.
        /// </summary>
        public void Walk(IConventionEntityType owner, StoreObjectIdentifier table, IConventionComplexProperty complex)
        {
            if (complex.IsCollection || complex.ComplexType.IsMappedToJson())
            {
                return;
            }

            foreach (var property in complex.ComplexType.GetProperties())
            {
                Collect(owner, table, property);
            }

            foreach (var nested in complex.ComplexType.GetComplexProperties())
            {
                Walk(owner, table, nested);
            }
        }

        /// <summary>
        /// Collects the column of a property, if it lies in the table.
        /// </summary>
        public void Collect(IConventionEntityType owner, StoreObjectIdentifier table, IConventionProperty property)
        {
            if (property.GetColumnName(table) is not { } column)
            {
                return;
            }

            _ = ConverterTypes.TryGetValueObjectType(property.GetValueConverter(), out var valueObjectType);
            if (!_columns.TryGetValue((table, column), out var properties))
            {
                _columns.Add((table, column), properties = []);
            }

            properties.Add((owner, property, valueObjectType));
        }

        /// <summary>
        /// Constrains each column whose every property one of this package's converters maps through the same value
        /// object: a column two types of a hierarchy share for two value objects, or for a value object and another type,
        /// holds values whose rules the other's constraint would refuse.
        /// </summary>
        /// <param name="model">The model, whose other check constraints hold names to keep apart from.</param>
        public void Constrain(IConventionModel model)
        {
            var constraints = new List<(IConventionEntityType Owner, StoreObjectIdentifier Table, string Column, string Rule, string Sql)>();
            foreach (var ((table, column), properties) in _columns)
            {
                var (owner, property, valueObjectType) = properties[0];
                if (valueObjectType is null || properties.Exists(other => other.ValueObject != valueObjectType))
                {
                    continue;
                }

                // The type a converter of this package maps is a value object, which the registry resolves: generated, it
                // is registered; a construction of a generic one, or one written by hand, it describes.
                _ = ValueObjectRegistry.TryResolve(valueObjectType, out var descriptor);
                var delimited = sql.DelimitIdentifier(column);
                var nullable = property.IsColumnNullable(table);
                var checks = descriptor!.Accept(new CheckConstraintRules(engine, rules, patterns, exclusions, mappings, property, delimited));
                foreach (var (rule, check) in checks)
                {
                    var wrapped = nullable ? $"{delimited} IS NULL OR ({check})" : check;
                    constraints.Add((owner, table, column, rule, wrapped));
                }
            }

            // A name is taken when another check constraint of the model holds it, or another table would take it too:
            // within a schema on SQL Server, which keeps constraint names there and compares them ignoring case under its
            // default collations, within a table elsewhere.
            var taken = new HashSet<(string? Schema, string? Table, string Name)>();
            foreach (var entityType in model.GetEntityTypes())
            {
                if (StoreObjectIdentifier.Create(entityType, StoreObjectType.Table) is not { } table)
                {
                    continue;
                }

                foreach (var check in entityType.GetCheckConstraints())
                {
                    if (check.GetName(table) is { } name)
                    {
                        taken.Add(Scope(table, name));
                    }
                }
            }

            var planned = constraints
                .GroupBy(constraint => Scope(constraint.Table, constraint.Column, constraint.Rule))
                .ToDictionary(static group => group.Key, static group => group.Count());
            foreach (var (owner, table, column, rule, check) in constraints)
            {
                var scope = Scope(table, column, rule);
                var distinct = taken.Contains(scope) || planned[scope] > 1;
                owner.Builder.HasCheckConstraint(Name(table.Name, column, rule, maxLength, distinct), check);
            }
        }

        /// <summary>
        /// Tells where the name of a constraint must be unique: in its schema on SQL Server, ignoring case, a table with no
        /// schema taken for one in <c>dbo</c>; in its table elsewhere.
        /// </summary>
        private (string? Schema, string? Table, string Name) Scope(StoreObjectIdentifier table, string name)
            => engine == CheckEngine.SqlServer
                ? ((table.Schema ?? "dbo").ToUpperInvariant(), null, name.ToUpperInvariant())
                : (table.Schema, table.Name, name);

        /// <summary>
        /// Tells where the name the convention would give a constraint must be unique.
        /// </summary>
        private (string? Schema, string? Table, string Name) Scope(StoreObjectIdentifier table, string column, string rule)
            => Scope(table, $"CK_{table.Name}_{column}_{rule}");
    }
}
