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
/// A pull request that changes only <c>README.md</c>, <c>website/docs/</c> or <c>skills/value-objects/</c> runs the tests
/// of this suite carrying the trait, and none other (<c>.github/scripts/ci-plan.sh</c>): a test reading those files
/// without it would let such a pull request break it unseen. A test reads the repository through a
/// <c>RepositoryRoot()</c> or <c>SkillDirectory()</c> of its own class, which this follows the test's IL to, through
/// what the test calls within its class, its data member and the state machine of an asynchronous one.
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
        marked.Should().Equal(reading);
    }

    private static string Name(MethodInfo method) => $"{method.DeclaringType!.Name}.{method.Name}";

    private static bool Marked(MethodInfo test)
        => test.GetCustomAttributes<TraitAttribute>().Concat(test.DeclaringType!.GetCustomAttributes<TraitAttribute>())
            .Any(static trait => trait is { Name: "Reads", Value: "Documentation" });

    /// <summary>Whether the test, its data and what they call within its class reach a reader of the repository.</summary>
    private static bool Reads(MethodInfo test)
    {
        var type = test.DeclaringType!;
        var pending = new Stack<MethodBase>([test]);
        foreach (var data in test.GetCustomAttributes<MemberDataAttribute>())
        {
            if (type.GetMethod(data.MemberName, Declared) is { } method)
            {
                pending.Push(method);
            }

            if (type.GetProperty(data.MemberName, Declared)?.GetMethod is { } getter)
            {
                pending.Push(getter);
            }
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

            if (method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType.GetMethod("MoveNext", Declared) is { } moveNext)
            {
                pending.Push(moveNext);
            }

            foreach (var callee in Callees(method))
            {
                if (callee.DeclaringType is { } owner && (owner == type || IsNestedIn(owner, type)))
                {
                    pending.Push(callee);
                }
            }
        }

        return false;
    }

    private static bool IsNestedIn(Type type, Type outer)
    {
        for (var current = type.DeclaringType; current is not null; current = current.DeclaringType)
        {
            if (current == outer)
            {
                return true;
            }
        }

        return false;
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
