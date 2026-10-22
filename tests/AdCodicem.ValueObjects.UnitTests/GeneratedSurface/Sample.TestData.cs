using System.ComponentModel;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Testing.Data;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

// The testdata family's part of Sample (GeneratedSurface/Sample.cs): the checks that hold every sample to the test-data
// sampler.

public abstract partial class Sample
{
    /// <summary>
    /// Draws values of the value object from its schema with <c>ValueObjectSampler</c>, and checks that each is a value its
    /// rules accept, that they vary and replay from their seed, that a descriptor draws the same, and that the values at
    /// its edges are accepted and the values its schema rules out refused.
    /// </summary>
    public abstract void DrawsValuesItsRulesAccept();
}

public partial class Sample<TSelf, TValue>
{
    public override void DrawsValuesItsRulesAccept()
    {
        const int Draws = 200;
        var sampler = new ValueObjectSampler(new Random(1));
        var drawn = new List<TSelf>(Draws);
        for (var draw = 0; draw < Draws; draw++)
        {
            var value = sampler.Next<TSelf, TValue>();
            TSelf.TryCreate(value.Value, out var created, out var validation).Should().BeTrue("{0} was drawn: {1}", value, validation);
            created.Should().Be(value);
            drawn.Add(value);
        }

        var distinct = drawn.Distinct().Count();
        distinct.Should().BeGreaterThan(1, "a sampler drawing one value is a constant");

        // A wide type is drawn from its schema rather than from the fallback on its example, but for a checksum no schema
        // carries: the MOD-97 digits of an IBAN refuse about a third of its draws, the check character of an entity
        // identifier one in twenty-five.
        var example = TSelf.Schema.Example is TValue declared ? TSelf.Create(declared) : default(TSelf?);
        var examples = drawn.Count(value => value.Equals(example));
        if (distinct > 10)
        {
            var checksummed = typeof(TSelf) == typeof(Iban) || typeof(TSelf).IsAssignableTo(typeof(AdCodicem.ValueObjects.Identifiers.IEntityId));
            examples.Should().BeLessThan(checksummed ? Draws / 2 : Draws / 20, "{0} draws of {1} were the example", examples, Draws);
        }

        var replay = new ValueObjectSampler(new Random(1));
        Enumerable.Range(0, Draws).Select(_ => replay.Next<TSelf, TValue>()).Should().Equal(drawn, "a seed replays its draws");

        ValueObjectRegistry.TryGet(typeof(TSelf), out var descriptor).Should().BeTrue();
        new ValueObjectSampler(new Random(1)).Next(descriptor!).Should().Be(drawn[0], "a descriptor draws as its types do");

        var edges = sampler.Boundaries<TSelf, TValue>().ToList();
        foreach (var edge in edges)
        {
            TSelf.TryCreate(edge, out _).Should().BeTrue("{0} is at an edge of {1}", edge, typeof(TSelf).Name);
        }

        var violations = sampler.RejectedValues<TSelf, TValue>().ToList();
        foreach (var (value, rule) in violations)
        {
            TSelf.TryCreate(value, out _).Should().BeFalse("{0} breaks {1}", value, rule);
        }

        // Each rule the schema declares has its edge and the value past it, the bounds read here as the type converter of
        // the underlying type reads them, apart from the sampler: none of this domain lies at an extreme of its type.
        var schema = TSelf.Schema;
        var rules = violations.Select(violation => violation.Rule).ToList();
        foreach (var (bound, name) in new[] { (schema.Minimum, "Minimum"), (schema.Maximum, "Maximum") })
        {
            if (bound is null)
            {
                continue;
            }

            var read = (TValue)TypeDescriptor.GetConverter(typeof(TValue)).ConvertFromInvariantString(bound)!;
            TSelf.TryCreate(read, out var accepted).Should().BeTrue("{0} accepts its {1}", typeof(TSelf).Name, name);
            edges.Should().Contain(accepted.Value, "{0} declares {1} {2}", typeof(TSelf).Name, name, bound);
            rules.Should().Contain($"{name} ({bound})");
        }

        if (schema.MinLength > 0)
        {
            rules.Should().Contain($"MinLength ({schema.MinLength})");
        }

        if (schema.MaxLength is { } longest)
        {
            rules.Should().Contain($"MaxLength ({longest})");
        }

        if (schema.IsClosedValueSet)
        {
            rules.Should().Contain("a closed value set");
        }
    }
}
