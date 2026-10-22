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
/// <c>RepositoryRoot()</c> or <c>SkillDirectory()</c>, which this follows the test's IL to: through every method of
/// this assembly it calls, the state machine of an asynchronous or iterator one, its data (a <c>[MemberData]</c>
/// method, property or field, of its class or of another, or a <c>[ClassData]</c> class), and its class's constructors
/// and type initializer, which xUnit runs for it.
/// </remarks>
public sealed class DocumentationTraitTests
{
    private const BindingFlags Declared =
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(static field => (OpCode)field.GetValue(null)!)
        .ToDictionary(static opCode => opCode.Value);

    private static readonly string[] Readers = ["RepositoryRoot", "SkillDirectory"];

    [Fact]
    public void Every_test_reading_the_repository_carries_the_trait_and_no_other_does()
    {
        var tests = typeof(DocumentationTraitTests).Assembly.GetTypes()
            .Where(static type => !type.IsDefined(typeof(CompilerGeneratedAttribute)))
            .SelectMany(static type => type.GetMethods(Declared))
            .Where(static method => method.IsDefined(typeof(FactAttribute), inherit: true))
            .ToList();

        var reading = tests.Where(static test => Reads(test)).Select(Name).Order(StringComparer.Ordinal).ToList();
        var marked = tests.Where(static test => Marked(test)).Select(Name).Order(StringComparer.Ordinal).ToList();

        reading.Should().HaveCountGreaterThan(20, "the walk must keep finding the tests that read the repository");
        reading.Except(marked).Should().BeEmpty("these tests read the repository: mark each [Trait(\"Reads\", \"Documentation\")]");
        marked.Except(reading).Should().BeEmpty("these tests read nothing of the repository: drop their trait");
    }

    private static string Name(MethodInfo method) => $"{method.DeclaringType!.Name}.{method.Name}";

    private static bool Marked(MethodInfo test)
        => test.GetCustomAttributes<TraitAttribute>().Concat(test.DeclaringType!.GetCustomAttributes<TraitAttribute>())
            .Any(static trait => trait is { Name: "Reads", Value: "Documentation" });

    /// <summary>Whether the test, its data, its class's set-up or what they call reach a reader of the repository.</summary>
    private static bool Reads(MethodInfo test)
    {
        var type = test.DeclaringType!;
        var module = type.Module;
        var pending = new Stack<MethodBase>([test]);
        PushSetUp(pending, type);
        foreach (var data in test.GetCustomAttributes<MemberDataAttribute>())
        {
            var owner = data.MemberType ?? type;
            foreach (var member in owner.GetMember(data.MemberName, Declared | BindingFlags.FlattenHierarchy))
            {
                switch (member)
                {
                    case MethodInfo method:
                        pending.Push(method);
                        break;
                    case PropertyInfo { GetMethod: { } getter }:
                        pending.Push(getter);
                        break;
                    case FieldInfo:
                        PushSetUp(pending, owner);
                        break;
                }
            }
        }

        foreach (var data in test.GetCustomAttributes<ClassDataAttribute>())
        {
            foreach (var method in data.Class.GetMethods(Declared))
            {
                pending.Push(method);
            }

            PushSetUp(pending, data.Class);
        }

        var seen = new HashSet<MethodBase>();
        while (pending.TryPop(out var method))
        {
            if (!seen.Add(method))
            {
                continue;
            }

            if (Readers.Contains(method.Name))
            {
                return true;
            }

            if (method.GetCustomAttribute<StateMachineAttribute>()?.StateMachineType.GetMethod("MoveNext", Declared) is { } moveNext)
            {
                pending.Push(moveNext);
            }

            foreach (var callee in Callees(method))
            {
                if (Readers.Contains(callee.Name) || callee.Module == module)
                {
                    pending.Push(callee);
                }
            }
        }

        return false;
    }

    /// <summary>What runs before a test of the type: its constructors and its type initializer.</summary>
    private static void PushSetUp(Stack<MethodBase> pending, Type type)
    {
        foreach (var constructor in type.GetConstructors(Declared))
        {
            pending.Push(constructor);
        }

        if (type.TypeInitializer is { } initializer)
        {
            pending.Push(initializer);
        }
    }

    /// <summary>The methods a method's IL calls or loads, as the unit suite's <c>RuntimeClosingTests</c> reads them.</summary>
    private static IEnumerable<MethodBase> Callees(MethodBase method)
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
            if (opCode.OperandType == OperandType.InlineMethod)
            {
                MethodBase? callee = null;
                try
                {
                    callee = method.Module.ResolveMethod(BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(offset)), typeArguments, methodArguments);
                }
                catch (ArgumentException)
                {
                    // A method of a generic type the walk cannot close: none of them reads the repository.
                }

                if (callee is not null)
                {
                    yield return callee;
                }
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
