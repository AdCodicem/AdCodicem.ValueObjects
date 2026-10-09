using System.Reflection;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;
using AdCodicem.ValueObjects.Testing.Data;
using Bogus;

namespace AdCodicem.ValueObjects.Bogus;

/// <summary>
/// Draws value objects from Bogus: a rule for every value-object member of a <see cref="Faker{T}"/>, and a value object
/// drawn from a <see cref="Faker"/>, each from the rules its type declares.
/// </summary>
/// <remarks>
/// <para>
/// Without a rule, Bogus leaves a value-object member at its default, the instance no rule of the type accepted; a rule
/// written by hand restates the bounds the type declares, and drifts from them. Here each value is drawn by a
/// <see cref="ValueObjectSampler"/>: a candidate built from the type's schema, kept only once its rules accept it, or the
/// example it declares.
/// </para>
/// <para>
/// Each value object takes one number from the faker's <see cref="Randomizer"/>, which seeds the sampler that draws it:
/// <c>UseSeed</c> replays the same values, and the rules after it see the same numbers whatever the sampler made of its
/// own draws. A value object is found through <c>ValueObjectRegistry.TryResolve</c>, which describes a value object written
/// by hand the first time it is asked for, and keeps that description.
/// </para>
/// </remarks>
public static class ValueObjectFakerExtensions
{
    private static readonly ValueObjectSamplerOptions Defaults = new();

    /// <summary>
    /// Draws a value of a value object its rules accept, from the schema it declares.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <param name="faker">The faker whose <see cref="Faker.Random"/> seeds the draw.</param>
    /// <param name="options">How the value is drawn, or <see langword="null"/> for the defaults.</param>
    /// <returns>The value object.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="faker"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><typeparamref name="TSelf"/> carries the marker of a value object but implements no <c>IValueObject&lt;TSelf, TValue&gt;</c> over itself.</exception>
    /// <exception cref="ValueObjectSamplingException">No value the type accepts could be drawn.</exception>
    /// <remarks>
    /// The underlying type is not named, since C# cannot infer it: the value object is closed through its descriptor. For a
    /// rule no schema carries, a checksum for one, name it and give a generator:
    /// <see cref="ValueObject{TSelf, TValue}(Faker, Func{Faker, TValue})"/>.
    /// </remarks>
    public static TSelf ValueObject<TSelf>(this Faker faker, ValueObjectSamplerOptions? options = null)
        where TSelf : struct, IValueObject
    {
        ArgumentNullException.ThrowIfNull(faker);
        return ValueObjectRegistry.TryResolve(typeof(TSelf), out var descriptor)
            ? (TSelf)descriptor.Accept(new Draw(faker, new Sampling(options ?? Defaults)))
            : throw new ArgumentException(
                $"'{TypeNames.Of(typeof(TSelf))}' is not a value object: it implements no IValueObject<TSelf, TValue> over itself.");
    }

    /// <summary>
    /// Draws a value of a value object from a generator of its underlying value, a semantic one of Bogus's for instance,
    /// kept only once the type's rules accept it.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="faker">The faker handed to <paramref name="generator"/>.</param>
    /// <param name="generator">Draws a candidate underlying value: <c>f =&gt; f.Finance.Iban()</c>.</param>
    /// <returns>The value object.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="faker"/> or <paramref name="generator"/> is <see langword="null"/>.</exception>
    /// <exception cref="ValueObjectSamplingException">
    /// None of the candidates the generator gave passed the type's rules, and it declares no example they accept.
    /// </exception>
    /// <remarks>
    /// The generator is tried as many times as the sampler tries candidates, 100 by default, and the example the type
    /// declares comes out when none passed.
    /// </remarks>
    public static TSelf ValueObject<TSelf, TValue>(this Faker faker, Func<Faker, TValue> generator)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(faker);
        ArgumentNullException.ThrowIfNull(generator);
        var options = new ValueObjectSamplerOptions().Use<TSelf, TValue>(_ => generator(faker));
        try
        {
            return new ValueObjectSampler(new Random(faker.Random.Int()), options).Next<TSelf, TValue>();
        }
        catch (ValueObjectSamplingException exception)
        {
            throw new ValueObjectSamplingException(
                exception.ValueObjectType,
                exception.Attempts,
                exception.ErrorCode,
                Registration<TSelf, TValue>(),
                fromGenerator: true);
        }
    }

    /// <summary>
    /// Gives every value-object member of <typeparamref name="T"/> a rule, its nullable, array and <see cref="List{T}"/>
    /// members included, each value drawn from the rules its type declares, so that <c>StrictMode(true)</c> is met.
    /// </summary>
    /// <typeparam name="T">The type the faker builds.</typeparam>
    /// <param name="faker">The faker to add the rules to.</param>
    /// <param name="options">How each value is drawn, or <see langword="null"/> for the defaults.</param>
    /// <returns>The faker, so that rules chain.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="faker"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Call it before the rules of the members it should leave alone: a rule of Bogus replaces the earlier rule of the
    /// same member, so a rule written after this one wins, and one written before is replaced. A nullable member gets a
    /// value, never <see langword="null"/>; an array or a list gets one to three values.
    /// </para>
    /// <para>
    /// The rules are added through Bogus's <c>RuleForType</c>, so they reach exactly the members the faker binds, through a
    /// binder of its own included. A <see cref="ValueObjectSamplingException"/> is thrown by <c>Generate</c>, when a member
    /// is drawn.
    /// </para>
    /// </remarks>
    public static Faker<T> RuleForValueObjects<T>(this Faker<T> faker, ValueObjectSamplerOptions? options = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(faker);
        var sampling = new Sampling(options ?? Defaults);
        foreach (var memberType in MemberTypes(typeof(T)))
        {
            var element = memberType.IsArray
                ? memberType.GetElementType()!
                : memberType.IsGenericType && memberType.GetGenericTypeDefinition() == typeof(List<>)
                    ? memberType.GetGenericArguments()[0]
                    : memberType;
            if (ValueObjectRegistry.TryResolve(element, out var descriptor))
            {
                descriptor.Accept(new Rule<T>(faker, memberType, sampling));
            }
        }

        return faker;
    }

    /// <summary>The registration of a generator, in Bogus's terms.</summary>
    private static string Registration<TSelf, TValue>()
        => $"faker.ValueObject<{TypeNames.Of(typeof(TSelf))}, {TypeNames.Of(typeof(TValue))}>(f => ...)";

    /// <summary>The types of the instance properties and fields of a type and of its base types, public or not.</summary>
    private static HashSet<Type> MemberTypes(Type type)
    {
        var types = new HashSet<Type>();
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                switch (member)
                {
                    case PropertyInfo property:
                        types.Add(property.PropertyType);
                        break;
                    case FieldInfo field:
                        types.Add(field.FieldType);
                        break;
                }
            }
        }

        return types;
    }

    /// <summary>Draws a value object from a faker's randomizer, rewording a failure in Bogus's terms.</summary>
    private sealed class Sampling(ValueObjectSamplerOptions options)
    {
        public TSelf Next<TSelf, TValue>(Faker from)
            where TSelf : struct, IValueObject<TSelf, TValue>
        {
            try
            {
                return new ValueObjectSampler(new Random(from.Random.Int()), options).Next<TSelf, TValue>();
            }
            catch (ValueObjectSamplingException exception) when (!exception.FromGenerator)
            {
                // A generator registered with the options keeps the sampler's message, which names that registration.
                throw new ValueObjectSamplingException(
                    exception.ValueObjectType,
                    exception.Attempts,
                    exception.ErrorCode,
                    Registration<TSelf, TValue>());
            }
        }
    }

    /// <summary>Draws a value object from the faker it is given, as its visitor.</summary>
    private sealed class Draw(Faker faker, Sampling sampling) : IValueObjectVisitor<object>
    {
        public object Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => sampling.Next<TSelf, TValue>(faker);
    }

    /// <summary>
    /// Adds the rule of one value object to the members whose type is the value object, its nullable, an array or a list
    /// of it; an array or a list of its nullable is left to the faker's other rules.
    /// </summary>
    private sealed class Rule<T>(Faker<T> faker, Type memberType, Sampling sampling) : IValueObjectVisitor<bool>
        where T : class
    {
        public bool Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
        {
            if (memberType == typeof(TSelf))
            {
                faker.RuleForType(typeof(TSelf), sampling.Next<TSelf, TValue>);
            }
            else if (memberType == typeof(TSelf?))
            {
                faker.RuleForType(typeof(TSelf?), f => (TSelf?)sampling.Next<TSelf, TValue>(f));
            }
            else if (memberType == typeof(TSelf[]))
            {
                faker.RuleForType(typeof(TSelf[]), f => Several<TSelf, TValue>(f).ToArray());
            }
            else if (memberType == typeof(List<TSelf>))
            {
                faker.RuleForType(typeof(List<TSelf>), Several<TSelf, TValue>);
            }
            else
            {
                return false;
            }

            return true;
        }

        private List<TSelf> Several<TSelf, TValue>(Faker f)
            where TSelf : struct, IValueObject<TSelf, TValue>
        {
            var count = f.Random.Int(1, 3);
            var values = new List<TSelf>(count);
            for (var index = 0; index < count; index++)
            {
                values.Add(sampling.Next<TSelf, TValue>(f));
            }

            return values;
        }
    }
}
