using System.Globalization;
using AdCodicem.ValueObjects.Generators.Internal;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// Where a value object can be declared: generic, nested in a generic type or an interface, private or protected, and
/// what the generated code then writes to reopen, name and register it.
/// </summary>
public sealed class DeclarationContextTests
{
    /// <summary>
    /// Every one of these used to be <c>VO0019</c>. The generated code now reopens each type around the value object
    /// with its type parameters, and an interface as an interface, and reaches a private or protected type through the
    /// types around it. Compiled with documentation diagnosed, which is the only mode that reports a broken <c>cref</c>
    /// in the generated files.
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    [Theory]
    [InlineData(
        """
        [ValueObject<string>(MaxLength = 12)]
        public readonly partial struct Code<T>;
        """)]
    [InlineData(
        """
        [ValueObject<int>(Arithmetic = true, ImplicitConversionToValue = true, ExplicitConversionFromValue = true)]
        [KnownValue("Dozen", 12)]
        public readonly partial struct Count<TUnit, TScale> : IValueObjectMinimum<int>
            where TUnit : struct
            where TScale : class, new()
        {
            public static int Minimum => 0;
        }
        """)]
    [InlineData(
        """
        [ValueObject<string>(ValueSet = ValueSetKind.Closed)]
        [KnownValue("France", "FR")]
        public readonly partial struct Country<@class> : IValueObjectNormalizer<string>, IValueObjectPatternValidator
        {
            public static string NormalizeValue(string value) => value.ToUpperInvariant();

            [System.Text.RegularExpressions.GeneratedRegex("^[A-Z]{2}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant, 1000)]
            public static partial System.Text.RegularExpressions.Regex Pattern { get; }
        }
        """)]
    [InlineData(
        """
        public partial class Outer<T>
        {
            [ValueObject<decimal>]
            public readonly partial struct Code : IValueObjectFormatter<decimal>
            {
                public static bool TryFormatValue(in decimal value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                    => value.TryFormat(destination, out charsWritten, format, provider);
            }
        }
        """)]
    [InlineData(
        """
        public static partial class Outer<TFirst>
        {
            public partial record Middle
            {
                [ValueObject<Guid>]
                public readonly partial struct Code<TSecond>;
            }
        }
        """)]
    [InlineData(
        """
        public partial interface IOuter
        {
            [ValueObject<string>]
            public readonly partial struct Code;

            [EntityId("acc")]
            public readonly partial struct AccountId;
        }
        """)]
    [InlineData(
        """
        public partial interface IOuter<T>
        {
            [ValueObject<DateOnly>]
            public readonly partial struct Code;
        }
        """)]
    [InlineData(
        """
        public partial class Outer
        {
            [ValueObject<string>]
            private readonly partial struct Code;

            [ValueObject<long>]
            protected readonly partial struct Total;

            [EntityId("acc")]
            private protected readonly partial struct AccountId;

            [ValueObject<string>]
            private readonly partial struct Label<T>;
        }
        """)]
    [InlineData(
        """
        public partial class Outer
        {
            private partial class Inner
            {
                protected partial record Deepest
                {
                    [ValueObject<string>]
                    public readonly partial struct Code;

                    [EntityId("acc")]
                    internal readonly partial struct AccountId;
                }
            }
        }
        """)]
    [InlineData(
        """
        public partial class Outer
        {
            private partial class Inner<T>
            {
                [ValueObject<string>]
                public readonly partial struct Code;
            }
        }
        """)]
    [InlineData(
        """
        public partial interface IOuter
        {
            private partial interface IInner
            {
                [ValueObject<int>]
                public readonly partial struct Code;
            }
        }
        """)]
    public void A_value_object_declared_in_any_supported_context_generates_and_compiles(string declaration)
    {
        var run = GeneratorHarness.Run(declaration, DocumentationMode.Diagnose);

        run.Diagnostics.Should().BeEmpty();
        run.Files.Should().Contain(file => file.HintName == "ValueObjectRegistration.g.cs");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A generic value object is reopened with its type parameters and names itself through them. Its attributes name
    /// converters that close its own over the construction they convert, since an attribute cannot name a type through
    /// a type parameter, and its registration registers its definition, since it knows none of its constructions.
    /// </summary>
    [Fact]
    public void A_generic_value_object_is_reopened_with_its_type_parameters_and_registers_its_definition()
    {
        var run = GeneratorHarness.Run("""
            public partial class Outer<TFirst>
            {
                [ValueObject<string>]
                public readonly partial struct Code<TSecond, TThird>;
            }
            """);

        var generated = run.SingleValueObject;
        generated.Should()
            .Contain("partial class Outer<TFirst>")
            .And.Contain("partial struct Code<TSecond, TThird> : global::AdCodicem.ValueObjects.IValueObject<global::Test.Outer<TFirst>.Code<TSecond, TThird>, global::System.String>")
            .And.Contain("[global::System.Text.Json.Serialization.JsonConverter(typeof(global::AdCodicem.ValueObjects.Metadata.GenericValueObjectJsonConverterFactory))]")
            .And.Contain("[global::System.ComponentModel.TypeConverter(typeof(global::AdCodicem.ValueObjects.Metadata.GenericValueObjectTypeConverter))]")
            .And.Contain("<see cref=\"Code{TSecond, TThird}\"/>");

        Registration(run).Should().Contain(
            "global::AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.RegisterGenericDefinition(typeof(global::Test.Outer<>.Code<,>));");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// An interface around a value object is reopened as an interface. Nothing around the value object is private, so
    /// it is registered directly.
    /// </summary>
    [Fact]
    public void An_interface_around_a_value_object_is_reopened_as_an_interface()
    {
        var run = GeneratorHarness.Run("""
            public partial interface IOuter
            {
                [ValueObject<string>]
                public readonly partial struct Code;
            }
            """);

        run.SingleValueObject.Should().Contain("partial interface IOuter").And.NotContain("ValueObjectRegistration_");
        Registration(run).Should().Contain(
            "ValueObjectRegistry.Register<global::Test.IOuter.Code, global::System.String>(global::Test.IOuter.Code.Schema, static () => new global::Test.IOuter.Code.ValueJsonConverter());");
    }

    /// <summary>
    /// A private or protected type is reachable only from the type declaring it. The registration of the assembly calls
    /// a step nested in the type declaring the outermost private or protected type, each step calls the next one, nested
    /// in the type it can see, and the last, in the type declaring the most deeply nested private or protected type,
    /// registers the value object, and an identifier's prefix with it. Each step is a class of its own, so that calling
    /// it runs no static constructor of the author's types, named after its position on the route, so that a type of the
    /// route deriving from another one hides nothing. The types nested deeper are public, and visible from there.
    /// </summary>
    [Fact]
    public void A_value_object_nested_in_a_private_type_is_registered_through_the_types_around_it()
    {
        var run = GeneratorHarness.Run("""
            public partial class Outer
            {
                private partial class Inner
                {
                    protected partial class Deeper
                    {
                        public partial class Public
                        {
                            [EntityId("acc")]
                            public readonly partial struct AccountId;
                        }
                    }
                }
            }
            """);

        var step = "ValueObjectRegistration_" + HintNames.Hash("Test.Outer.Inner.Deeper.Public.AccountId");
        const string Self = "global::Test.Outer.Inner.Deeper.Public.AccountId";

        run.SingleValueObject.Should().Contain($$"""
                partial class Outer
                {
                    /// <summary>Registers <c>AccountId</c>, from a scope that can see it.</summary>
                    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                    internal static class {{step}}_0
                    {
                        /// <summary>Runs the step.</summary>
                        internal static void Register()
                        {
                            global::Test.Outer.Inner.{{step}}_1.Register();
                        }
                    }

                    partial class Inner
                    {
                        /// <summary>Registers <c>AccountId</c>, from a scope that can see it.</summary>
                        [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
                        internal static class {{step}}_1
                        {
                            /// <summary>Runs the step.</summary>
                            internal static void Register()
                            {
                                global::AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.Register<{{Self}}, global::System.String>({{Self}}.Schema, static () => new {{Self}}.ValueJsonConverter());
                                global::AdCodicem.ValueObjects.Identifiers.EntityIdRegistry.Register<{{Self}}>();
                            }
                        }

                        partial class Deeper
                        {
                            partial class Public
                            {
            """.ReplaceLineEndings("\n"));

        var registration = Registration(run);
        registration.Should().Contain($"global::Test.Outer.{step}_0.Register();");
        registration.Should().NotContain("EntityIdRegistry", "the step registers the prefix, from where the type is visible");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The registration of the assembly reaches every public type down to the one declaring the outermost private type,
    /// so the route starts there: a public type around it receives no step.
    /// </summary>
    [Fact]
    public void A_route_starts_at_the_type_declaring_the_outermost_private_type()
    {
        var run = GeneratorHarness.Run("""
            public partial class Outer
            {
                public partial class Middle
                {
                    [ValueObject<string>]
                    private readonly partial struct Code;
                }
            }
            """);

        var step = "ValueObjectRegistration_" + HintNames.Hash("Test.Outer.Middle.Code");

        Registration(run).Should().Contain($"global::Test.Outer.Middle.{step}_0.Register();");
        run.SingleValueObject.Should().NotContain($"{step}_1").And.Contain($$"""
                partial class Outer
                {
                    partial class Middle
                    {
                        /// <summary>Registers <c>Code</c>, from a scope that can see it.</summary>
            """.ReplaceLineEndings("\n"));
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A closed hierarchy nests its cases in the type they derive from. Each type of the route holds a step named after
    /// its position, so the step nested in a case hides none it inherits, which would be CS0108 in a file the author
    /// cannot edit.
    /// </summary>
    /// <param name="declaration">The declaration.</param>
    [Theory]
    [InlineData(
        """
        public abstract partial record PaymentMethod
        {
            private sealed partial record Card : PaymentMethod
            {
                [ValueObject<string>(MaxLength = 4)]
                private readonly partial struct Last4;
            }
        }
        """)]
    [InlineData(
        """
        public partial interface IShape
        {
            protected partial interface ICircle : IShape
            {
                [ValueObject<int>]
                protected readonly partial struct Radius;
            }
        }
        """)]
    public void A_route_type_deriving_from_the_type_around_it_hides_no_step(string declaration)
    {
        var run = GeneratorHarness.Run(declaration, DocumentationMode.Diagnose);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A private generic value object registers its definition from the type declaring it, which can name it unbound.
    /// </summary>
    [Fact]
    public void A_private_generic_value_object_registers_its_definition_from_the_type_declaring_it()
    {
        var run = GeneratorHarness.Run("""
            public partial class Outer
            {
                [ValueObject<string>]
                private readonly partial struct Code<T>;
            }
            """);

        run.SingleValueObject.Should().Contain(
            "global::AdCodicem.ValueObjects.Metadata.ValueObjectRegistry.RegisterGenericDefinition(typeof(global::Test.Outer.Code<>));");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The generated code names a value object nested in a generic type through that type's type parameters, from
    /// inside it. A nested type or a type parameter of the same name, between the two, would take the name, and the
    /// generated code would name another type.
    /// </summary>
    [Theory]
    [InlineData(
        """
        public partial class Outer<T>
        {
            public partial class Middle
            {
                public sealed class T;

                [ValueObject<string>]
                public readonly partial struct Code;
            }
        }
        """,
        "names the type parameter 'T' of 'Outer<T>', which the type 'Middle.T' hides")]
    [InlineData(
        """
        public partial class Outer<T>
        {
            #pragma warning disable CS0693 // The shadowing under test.
            [ValueObject<string>]
            public readonly partial struct Code<T>;
            #pragma warning restore CS0693
        }
        """,
        "names the type parameter 'T' of 'Outer<T>', which the type parameter of 'Code<T>' hides")]
    [InlineData(
        """
        public partial class Outer<ValueJsonConverter>
        {
            [ValueObject<string>]
            public readonly partial struct Code;
        }
        """,
        "names the type parameter 'ValueJsonConverter' of 'Outer<ValueJsonConverter>', which the type 'Code.ValueJsonConverter' hides")]
    [InlineData(
        """
        public class EntityBase
        {
            public sealed class TKey;
        }

        public partial class Repository<TKey>
        {
            public partial class Entry : EntityBase
            {
                [ValueObject<string>]
                public readonly partial struct Code;
            }
        }
        """,
        "names the type parameter 'TKey' of 'Repository<TKey>', which the type 'EntityBase.TKey' hides")]
    [InlineData(
        """
        public interface IBase
        {
            public sealed class T;
        }

        public partial class Outer<T>
        {
            public partial interface IMiddle : IBase
            {
                [ValueObject<string>]
                public readonly partial struct Code;
            }
        }
        """,
        "names the type parameter 'T' of 'Outer<T>', which the type 'IBase.T' hides")]
    [InlineData(
        """
        [ValueObject<int>]
        public readonly partial struct Code<StandardValuesCollection>;
        """,
        "names the type parameter 'StandardValuesCollection' of 'Code<StandardValuesCollection>', which the type "
        + "'TypeConverter.StandardValuesCollection', which a generated converter inherits, hides")]
    [InlineData(
        """
        public partial class Outer<SimplePropertyDescriptor>
        {
            [ValueObject<int>]
            public readonly partial struct Code;
        }
        """,
        "names the type parameter 'SimplePropertyDescriptor' of 'Outer<SimplePropertyDescriptor>', which the type "
        + "'TypeConverter.SimplePropertyDescriptor', which a generated converter inherits, hides")]
    public void A_type_parameter_the_generated_code_cannot_name_is_reported_and_not_generated(string declaration, string reason)
    {
        var run = GeneratorHarness.Run(declaration);

        var diagnostic = run.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("VO0019");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"'Code' {reason}, which the generator does not support. Rename the type parameter: the generated code names "
            + "the types around the value object through their type parameters, from inside it.");
        run.Files.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A type parameter is named without type arguments, so a nested type with type parameters of its own, or a private
    /// nested type of a base class, which name lookup skips, hides nothing.
    /// </summary>
    [Fact]
    public void A_type_that_name_lookup_skips_hides_no_type_parameter()
    {
        var run = GeneratorHarness.Run("""
            public class Base
            {
                private sealed class T;
            }

            public partial class Outer<T>
            {
                public partial class Middle : Base
                {
                    public sealed class T<TOther>;

                    [ValueObject<string>]
                    public readonly partial struct Code;
                }
            }
            """);

        run.Diagnostics.Should().BeEmpty();
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// A type parameter shares the declaration space of the members of its type, so neither a generated member nor a
    /// known value can take its name.
    /// </summary>
    [Fact]
    public void A_name_a_type_parameter_takes_is_left_to_it()
    {
        var run = GeneratorHarness.Run("""
            [ValueObject<string>]
            public readonly partial struct Code<Value>;

            [ValueObject<string>]
            [KnownValue("T", "t")]
            public readonly partial struct Label<T>;

            [ValueObject<string>]
            [KnownValue("Foo", "foo")]
            public readonly partial struct Tag<get_Foo>;
            """);

        run.Diagnostics.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)).Should().BeEquivalentTo(
            "'Code' has a type parameter, 'Value', named after a member the generated code writes on it, which the "
            + "generator does not support. Rename the type parameter: C# does not let a member take the name of a type "
            + "parameter of its type.",
            "'T' is not usable as the name of a generated member on 'Label': a type parameter of the type already takes "
            + "that name",
            "'Foo' is not usable as the name of a generated member on 'Tag': a type parameter of the type already takes "
            + "the name get_Foo, which the property's getter would take");
        run.CompilationDiagnostics.Should().BeEmpty();
    }

    private static string Registration(GeneratorRun run)
        => run.Files.Single(file => file.HintName == "ValueObjectRegistration.g.cs").Text;
}
