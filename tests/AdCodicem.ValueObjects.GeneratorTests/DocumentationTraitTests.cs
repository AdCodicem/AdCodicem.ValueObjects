using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// Every test that reads the repository's files carries <c>[Trait("Reads", "Documentation")]</c>, on itself or on its
/// class, and no other test does.
/// </summary>
/// <remarks>
/// A pull request that changes only <c>README.md</c>, <c>website/docs/</c> or <c>skills/value-objects/</c> runs the
/// tests of this suite carrying the trait, and none other (<c>.github/scripts/ci-plan.sh</c>): a test reading those
/// files without it would let such a pull request break it unseen. A test reads the repository through a method named
/// <c>RepositoryRoot()</c> or <c>SkillDirectory()</c>, or one looking for <c>AdCodicem.ValueObjects.slnx</c>, which
/// this follows the test's IL to, for each class xUnit runs a test in, its inherited tests included. It walks every
/// method of this assembly the test reaches: what it calls, the state machine of an asynchronous or iterator one, the
/// override the test's class runs of one of its virtual methods, the implementations of any other interface or virtual
/// method of this assembly, and the type initializer of a type whose static field it reads or whose initializer is
/// explicit. It starts from the test, its data (a <c>[MemberData]</c> method, property or field of its class, a base
/// class or another type, a <c>[ClassData]</c> class, an attribute of this assembly on it), and what xUnit runs around
/// it for its class and for the class, collection and assembly fixtures it builds: their constructors and their
/// <c>InitializeAsync</c>, <c>DisposeAsync</c> and <c>Dispose</c>.
/// </remarks>
public sealed class DocumentationTraitTests
{
    private const BindingFlags Declared =
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private const string RepositoryMarker = "AdCodicem.ValueObjects.slnx";

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(static field => (OpCode)field.GetValue(null)!)
        .ToDictionary(static opCode => opCode.Value);

    private static readonly string[] Readers = ["RepositoryRoot", "SkillDirectory"];

    private static readonly Type[] Types = typeof(DocumentationTraitTests).Assembly.GetTypes();

    [Fact]
    public void Every_test_reading_the_repository_carries_the_trait_and_no_other_does()
    {
        // As xUnit runs them: each class it can run, a static one included, with the test methods it inherits.
        var tests = Types
            .Where(static type => type.IsClass && (!type.IsAbstract || type.IsSealed) && !type.IsDefined(typeof(CompilerGeneratedAttribute)))
            .SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(static method => method.IsDefined(typeof(FactAttribute), inherit: true))
                .Select(method => new Test(type, method)))
            .ToList();

        var readers = tests.Select(static test => (Test: test, Reader: Reader(test))).Where(static found => found.Reader is not null).ToList();
        var reading = readers.Select(static found => found.Test).ToHashSet();
        var marked = tests.Where(static test => test.Marked).ToHashSet();

        reading.Should().HaveCountGreaterThan(20, "the walk must keep finding the tests that read the repository");
        var unmarked = readers.Where(found => !marked.Contains(found.Test))
            .Select(static found => $"{found.Test} (through {Name(found.Reader!)})").Order(StringComparer.Ordinal).ToList();
        var stale = marked.Except(reading).Select(static test => test.ToString()).Order(StringComparer.Ordinal).ToList();
        unmarked.Should().BeEmpty("these tests read the repository, so each needs [Trait(\"Reads\", \"Documentation\")]: {0}", string.Join(", ", unmarked));
        stale.Should().BeEmpty("these tests read nothing of the repository, so none needs the trait: {0}", string.Join(", ", stale));
    }

    private static string Name(MethodBase method) => $"{TypeName(method.DeclaringType!)}.{method.Name}";

    private static string TypeName(Type type) => type.FullName![(type.Namespace is { } name ? name.Length + 1 : 0)..];

    /// <summary>The method through which the test, its data, its class's set-up or what they reach read it.</summary>
    private static MethodBase? Reader(Test test)
    {
        var type = test.Class;
        var walk = new Walk(type);
        walk.Push(test.Method);
        walk.PushSetUp(type);
        foreach (var fixture in Fixtures(type))
        {
            walk.PushSetUp(fixture);
        }

        foreach (var data in test.Method.GetCustomAttributes<MemberDataAttribute>())
        {
            foreach (var member in Member(data.MemberType ?? type, data.MemberName))
            {
                switch (member)
                {
                    case MethodInfo method:
                        walk.Push(method);
                        break;
                    case PropertyInfo { GetMethod: { } getter }:
                        walk.Push(getter);
                        break;
                    case FieldInfo field:
                        walk.PushInitializer(field.DeclaringType!);
                        break;
                }
            }
        }

        foreach (var data in test.Method.GetCustomAttributes<ClassDataAttribute>())
        {
            foreach (var method in data.Class.GetMethods(Declared))
            {
                walk.Push(method);
            }

            walk.PushSetUp(data.Class);
        }

        // An attribute of this assembly on the test, such as a data attribute of its own, runs when xUnit reads it.
        foreach (var attribute in test.Method.GetCustomAttributes(inherit: true).Select(static attribute => attribute.GetType())
            .Where(static attribute => attribute.Module == typeof(DocumentationTraitTests).Module))
        {
            foreach (var method in attribute.GetMethods(Declared))
            {
                walk.Push(method);
            }

            walk.PushSetUp(attribute);
        }

        return walk.Reader();
    }

    /// <summary>A member of the type or, as xUnit looks one up, of its nearest base type declaring it.</summary>
    private static MemberInfo[] Member(Type type, string name)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            if (current.GetMember(name, Declared) is { Length: > 0 } found)
            {
                return found;
            }
        }

        return [];
    }

    /// <summary>The fixtures xUnit builds for a test of the type: its class's, collection's and assembly's.</summary>
    private static IEnumerable<Type> Fixtures(Type type)
    {
        var collections = type.GetCustomAttributes<CollectionAttribute>().ToList();
        var definitions = Types.Where(definition => definition.GetCustomAttributes<CollectionDefinitionAttribute>()
            .Any(attribute => collections.Any(collection => collection.Type == definition
                || (collection.Name is { } name && attribute.Name == name))))
            .Concat(type.GetCustomAttributes(inherit: true).Select(static attribute => attribute.GetType())
                .Where(static attribute => attribute.IsGenericType && attribute.GetGenericTypeDefinition() == typeof(CollectionAttribute<>))
                .Select(static attribute => attribute.GetGenericArguments()[0]));
        var fixtures = new[] { type }.Concat(definitions)
            .SelectMany(static owner => owner.GetInterfaces())
            .Where(static contract => contract.IsGenericType
                && (contract.GetGenericTypeDefinition() == typeof(IClassFixture<>) || contract.GetGenericTypeDefinition() == typeof(ICollectionFixture<>)))
            .Select(static contract => contract.GetGenericArguments()[0]);
        return fixtures.Concat(type.Assembly.GetCustomAttributes<AssemblyFixtureAttribute>().Select(static fixture => fixture.AssemblyFixtureType));
    }

    /// <summary>A test as xUnit runs it: the class it runs in, which may inherit the method, and the method.</summary>
    private sealed record Test(Type Class, MethodInfo Method)
    {
        /// <summary>Whether the method or the class it runs in carries the trait.</summary>
        public bool Marked => Method.GetCustomAttributes<TraitAttribute>().Concat(Class.GetCustomAttributes<TraitAttribute>())
            .Any(static trait => trait is { Name: "Reads", Value: "Documentation" });

        public override string ToString() => $"{TypeName(Class)}.{Method.Name}";
    }

    /// <summary>The methods a test reaches, walked until one of them reads the repository.</summary>
    private sealed class Walk(Type concrete)
    {
        private static readonly Dictionary<MethodBase, MethodBase[]> ImplementationsOf = [];

        private static readonly Type[] Lifecycles = [typeof(IAsyncLifetime), typeof(IAsyncDisposable), typeof(IDisposable)];

        private readonly Module module = typeof(DocumentationTraitTests).Module;
        private readonly Stack<MethodBase> pending = new();
        private readonly HashSet<MethodBase> seen = [];

        public void Push(MethodBase method) => pending.Push(method);

        /// <summary>
        /// What xUnit runs around a test of the type, or of a fixture: its constructors, its explicit type initializer,
        /// and its InitializeAsync, DisposeAsync and Dispose.
        /// </summary>
        public void PushSetUp(Type type)
        {
            if (!type.IsInterface)
            {
                foreach (var lifecycle in Lifecycles.Where(lifecycle => lifecycle.IsAssignableFrom(type)))
                {
                    foreach (var method in type.GetInterfaceMap(lifecycle).TargetMethods)
                    {
                        pending.Push(method);
                    }
                }
            }

            // Instance constructors only: with BindingFlags.Static, GetConstructors returns the type initializer too.
            foreach (var constructor in type.GetConstructors(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                pending.Push(constructor);
            }

            if (!type.Attributes.HasFlag(TypeAttributes.BeforeFieldInit))
            {
                PushInitializer(type);
            }
        }

        public void PushInitializer(Type type)
        {
            if (type.TypeInitializer is { } initializer)
            {
                pending.Push(initializer);
            }
        }

        public MethodBase? Reader()
        {
            while (pending.TryPop(out var method))
            {
                if (!seen.Add(method))
                {
                    continue;
                }

                if (Readers.Contains(method.Name))
                {
                    return method;
                }

                if (method.GetCustomAttribute<StateMachineAttribute>()?.StateMachineType.GetMethod("MoveNext", Declared) is { } moveNext)
                {
                    pending.Push(moveNext);
                }

                foreach (var operand in Operands(method))
                {
                    switch (operand)
                    {
                        case RepositoryMarker:
                            return method;
                        case MethodBase callee when Readers.Contains(callee.Name):
                            return callee;
                        case MethodBase callee when callee.Module == module && !IsTheWalk(callee.DeclaringType):
                            pending.Push(callee);
                            if (callee.DeclaringType is { } declaring && !declaring.Attributes.HasFlag(TypeAttributes.BeforeFieldInit))
                            {
                                PushInitializer(declaring);
                            }

                            // A virtual method of the test's own class runs as the class xUnit runs the test in has it.
                            if (OnTheTestClass(callee) is { } resolved)
                            {
                                pending.Push(resolved);
                                break;
                            }

                            foreach (var implementation in Implementations(callee))
                            {
                                pending.Push(implementation);
                            }

                            break;
                        case FieldInfo { IsStatic: true, DeclaringType: { } owner } when owner.Module == module:
                            PushInitializer(owner);
                            break;
                    }
                }
            }

            return null;
        }

        /// <summary>The override the test's class runs of a virtual method of it or of a base class, if the method is one.</summary>
        private MethodInfo? OnTheTestClass(MethodBase method)
        {
            if (method is not MethodInfo { IsVirtual: true, DeclaringType: { } owner } virtualMethod || !owner.IsAssignableFrom(concrete))
            {
                return null;
            }

            if (owner.IsInterface)
            {
                var map = concrete.GetInterfaceMap(owner);
                return map.TargetMethods.Where((_, index) => map.InterfaceMethods[index] == virtualMethod).FirstOrDefault();
            }

            for (Type? current = concrete; current is not null; current = current.BaseType)
            {
                if (current.GetMethods(Declared).FirstOrDefault(candidate => candidate.GetBaseDefinition() == virtualMethod.GetBaseDefinition()) is { } found)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>Whether the type is this class or one of its own, which read IL, not the repository.</summary>
        private static bool IsTheWalk(Type? type)
        {
            for (var current = type; current is not null; current = current.DeclaringType)
            {
                if (current == typeof(DocumentationTraitTests))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The methods of this assembly an interface or virtual method of it dispatches to.</summary>
        private static MethodBase[] Implementations(MethodBase method)
        {
            if (method is not MethodInfo { IsVirtual: true, DeclaringType: { } owner } virtualMethod)
            {
                return [];
            }

            if (ImplementationsOf.TryGetValue(method, out var known))
            {
                return known;
            }

            var found = new List<MethodBase>();
            foreach (var type in Types.Where(type => type != owner && !type.IsInterface && owner.IsAssignableFrom(type)))
            {
                if (owner.IsInterface)
                {
                    try
                    {
                        var map = type.GetInterfaceMap(owner);
                        found.AddRange(map.TargetMethods.Where((_, index) => map.InterfaceMethods[index] == virtualMethod));
                    }
                    catch (ArgumentException)
                    {
                        // An open generic type the map cannot read: none of them reads the repository.
                    }
                }
                else
                {
                    found.AddRange(type.GetMethods(Declared).Where(candidate => candidate.GetBaseDefinition() == virtualMethod.GetBaseDefinition()));
                }
            }

            return ImplementationsOf[method] = [.. found];
        }
    }

    /// <summary>
    /// The methods, static and instance fields and strings a method's IL names, read as the unit suite's
    /// <c>RuntimeClosingTests</c> reads them.
    /// </summary>
    private static IEnumerable<object> Operands(MethodBase method)
    {
        if (method.GetMethodBody()?.GetILAsByteArray() is not { } il)
        {
            yield break;
        }

        var typeArguments = method.DeclaringType!.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        for (var offset = 0; offset < il.Length;)
        {
            var value = il[offset] == 0xFE ? unchecked((short)(0xFE00 | il[offset + 1])) : il[offset];
            var opCode = OpCodesByValue[value];
            offset += opCode.Size;
            object? operand = null;
            try
            {
                var token = opCode.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineString
                    ? BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(offset))
                    : 0;
                operand = opCode.OperandType switch
                {
                    OperandType.InlineMethod => method.Module.ResolveMethod(token, typeArguments, methodArguments),
                    OperandType.InlineField => method.Module.ResolveField(token, typeArguments, methodArguments),
                    OperandType.InlineString => method.Module.ResolveString(token),
                    _ => null,
                };
            }
            catch (ArgumentException)
            {
                // A member of a generic type the walk cannot close: none of them reads the repository.
            }

            if (operand is not null)
            {
                yield return operand;
            }

            offset += opCode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + (4 * BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(offset))),
                _ => 4,
            };
        }
    }
}
