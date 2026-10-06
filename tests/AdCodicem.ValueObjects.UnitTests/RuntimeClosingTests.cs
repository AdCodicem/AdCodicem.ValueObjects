using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// Reads the IL of every package for the calls that close a generic over a <see cref="Type"/> at run time, which native
/// AOT has no code for when a type argument is a struct.
/// </summary>
/// <remarks>
/// An integration that knows a value object only by its <see cref="Type"/> closes its adapter through the descriptor's
/// visitor instead. The native AOT application notices a return to <c>MakeGenericType</c> in the packages it references,
/// whose native binary then fails where the JIT run does not. It cannot reference the ASP.NET Core and Entity Framework
/// Core packages, which are not AOT-compatible and are built with the trimming and AOT analyzers off, and a closing by
/// reflection behaves the same as one through the visitor under the JIT: there, this test is what notices it.
/// </remarks>
public sealed class RuntimeClosingTests
{
    private const BindingFlags Declared =
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(static field => (OpCode)field.GetValue(null)!)
        .ToDictionary(static opCode => opCode.Value);

    /// <summary>
    /// Gets each package, with the calls closing a generic at run time it is allowed: one line per method and API.
    /// </summary>
    public static TheoryData<string, string[]> Packages => new()
    {
        // The registry describes by reflection a value object nothing registered, which only the JIT can do: a
        // construction of a generic value object it was never told about, or a value object written by hand.
        {
            "AdCodicem.ValueObjects.Abstractions",
            [
                "ValueObjectRegistry.BuildByReflection calls MethodInfo.MakeGenericMethod",
                "ValueObjectRegistry.BuildByReflection calls Type.MakeGenericType",
                "ValueObjectRegistry.Describe calls Activator.CreateInstance",
            ]
        },
        { "AdCodicem.ValueObjects.AspNetCore", [] },
        { "AdCodicem.ValueObjects.AspNetCore.Http", [] },
        { "AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson", [] },
        { "AdCodicem.ValueObjects.Dapper", [] },

        // The converter of a TSelf? property, which C# names only under a constraint a visitor cannot prove, closed over
        // the visitor's own type arguments while a model is built, which Entity Framework Core never does under native AOT.
        {
            "AdCodicem.ValueObjects.EntityFrameworkCore",
            [
                "ConverterTypes.Optional calls Type.MakeGenericType",
                "GenericValueObjectConvention.PropertyConversion.Visit calls Activator.CreateInstance",
            ]
        },
        { "AdCodicem.ValueObjects.FluentValidation", [] },
        { "AdCodicem.ValueObjects.Identifiers", [] },
        { "AdCodicem.ValueObjects.Identifiers.EntityFrameworkCore", [] },
        { "AdCodicem.ValueObjects.Json", [] },
        { "AdCodicem.ValueObjects.NewtonsoftJson", [] },
        { "AdCodicem.ValueObjects.OpenApi", [] },
        { "AdCodicem.ValueObjects.Testing", [] },
    };

    /// <summary>
    /// A package closes a generic at run time only where it is known to, and the lines of the two packages that do show
    /// the reading finds such a call wherever it is, a lambda or a nested type included.
    /// </summary>
    /// <param name="package">Name of the package's assembly.</param>
    /// <param name="allowed">The calls it is allowed, each named after its method.</param>
    [Theory]
    [MemberData(nameof(Packages))]
    public void A_package_closes_a_generic_over_a_type_at_run_time_only_where_it_is_known_to(string package, string[] allowed)
    {
        var calls = Assembly.Load(package).GetTypes()
            .SelectMany(static type => type.GetMethods(Declared).Cast<MethodBase>().Concat(type.GetConstructors(Declared)))
            .SelectMany(static method => Callees(method)
                .Where(IsRuntimeClosing)
                .Select(callee => $"{DisplayName(method)} calls {callee.DeclaringType!.Name}.{callee.Name}"))
            .Distinct()
            .Order(StringComparer.Ordinal);

        calls.Should().Equal(allowed);
    }

    /// <summary>Tells the calls that close a generic over a <see cref="Type"/>, or create an instance of one.</summary>
    private static bool IsRuntimeClosing(MethodBase callee)
        => (callee.DeclaringType == typeof(Type) && callee.Name == nameof(Type.MakeGenericType))
           || (callee.DeclaringType == typeof(MethodInfo) && callee.Name == nameof(MethodInfo.MakeGenericMethod))
           || (callee.DeclaringType == typeof(Activator) && callee.Name == nameof(Activator.CreateInstance) && !callee.IsGenericMethod);

    /// <summary>Reads the methods a method calls, creates or takes the address of, from its IL.</summary>
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
                yield return method.Module.ResolveMethod(BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(offset)), typeArguments, methodArguments)!;
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

    /// <summary>
    /// Names a method as its source does: a lambda, a local function or an iterator after the method declaring it, and a
    /// nested type after the types it is nested in.
    /// </summary>
    private static string DisplayName(MethodBase method)
    {
        var name = method.Name;
        var type = method.DeclaringType!;
        while (type.IsDefined(typeof(CompilerGeneratedAttribute)) && type.DeclaringType is { } outer)
        {
            name = SourceName(name) is { Length: > 0 } ? name : type.Name;
            type = outer;
        }

        var path = new List<string> { SourceName(name) is { Length: > 0 } source ? source : name };
        for (var current = type; current is not null; current = current.DeclaringType)
        {
            path.Insert(0, current.Name.Split('`')[0]);
        }

        return string.Join('.', path);
    }

    /// <summary>Reads the source name the compiler wrote between angle brackets, <c>Describe</c> in <c>&lt;Describe&gt;b__0</c>.</summary>
    private static string? SourceName(string name)
        => name.StartsWith('<') && name.IndexOf('>', StringComparison.Ordinal) is > 0 and var end ? name[1..end] : null;
}
