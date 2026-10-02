using AdCodicem.ValueObjects.Identifiers;

namespace AdCodicem.ValueObjects.UnitTests.Identifiers;

public class ValueObjectIdsTests
{
    [Fact]
    public void The_defaults_are_the_system_clock_and_a_cryptographic_source()
    {
        ValueObjectIds.TimeProvider.Should().BeSameAs(TimeProvider.System);
        ValueObjectIds.Entropy.Should().BeSameAs(IdEntropySource.System);
    }

    /// <summary>
    /// The default is process-wide, so every test class minting an identifier without a scope reads it while this
    /// test runs. The replacement forwards to the system source: a deterministic one would hand those tests the
    /// same bytes on every call, and EntityIdTests.New_mints_a_different_identifier_every_time would then fail
    /// whenever the two overlapped.
    /// </summary>
    [Fact]
    public void Configure_replaces_the_process_wide_default()
    {
        var entropy = new ForwardingEntropy();

        try
        {
            ValueObjectIds.Configure(entropy: entropy);

            ValueObjectIds.Entropy.Should().BeSameAs(entropy);
            ValueObjectIds.TimeProvider.Should().BeSameAs(TimeProvider.System, "the clock was not part of the call");
        }
        finally
        {
            ValueObjectIds.Configure(entropy: IdEntropySource.System);
        }
    }

    [Fact]
    public void Configure_keeps_the_entropy_when_only_the_clock_is_replaced()
    {
        var clock = new ForwardingClock();

        try
        {
            ValueObjectIds.Configure(timeProvider: clock);

            ValueObjectIds.TimeProvider.Should().BeSameAs(clock);
            ValueObjectIds.Entropy.Should().BeSameAs(IdEntropySource.System, "the entropy was not part of the call");
        }
        finally
        {
            ValueObjectIds.Configure(timeProvider: TimeProvider.System);
        }
    }

    [Fact]
    public void Use_wins_over_the_default_and_restores_on_dispose()
    {
        var scoped = new DeterministicEntropy(1);

        using (ValueObjectIds.Use(entropy: scoped))
        {
            ValueObjectIds.Entropy.Should().BeSameAs(scoped);
        }

        ValueObjectIds.Entropy.Should().BeSameAs(IdEntropySource.System);
    }

    [Fact]
    public void A_nested_scope_wins_and_restores_to_the_enclosing_one()
    {
        var outer = new DeterministicEntropy(1);
        var inner = new DeterministicEntropy(2);

        using (ValueObjectIds.Use(entropy: outer))
        {
            using (ValueObjectIds.Use(entropy: inner))
            {
                ValueObjectIds.Entropy.Should().BeSameAs(inner);
            }

            ValueObjectIds.Entropy.Should().BeSameAs(outer);
        }
    }

    [Fact]
    public void A_scope_inherits_what_it_does_not_replace()
    {
        var clock = new StoppedClock(DateTimeOffset.UnixEpoch);
        var entropy = new DeterministicEntropy(3);

        using (ValueObjectIds.Use(clock, entropy))
        using (ValueObjectIds.Use(entropy: new DeterministicEntropy(4)))
        {
            ValueObjectIds.TimeProvider.Should().BeSameAs(clock, "the inner scope only replaced the entropy");
        }
    }

    [Fact]
    public void A_scope_replacing_only_the_clock_keeps_the_enclosing_entropy()
    {
        var entropy = new DeterministicEntropy(5);
        var clock = new StoppedClock(DateTimeOffset.UnixEpoch);

        using (ValueObjectIds.Use(entropy: entropy))
        using (ValueObjectIds.Use(timeProvider: clock))
        {
            ValueObjectIds.TimeProvider.Should().BeSameAs(clock);
            ValueObjectIds.Entropy.Should().BeSameAs(entropy, "the inner scope only replaced the clock");
        }
    }

    [Fact]
    public void Disposing_a_scope_twice_does_not_unwind_the_enclosing_one()
    {
        var outer = new DeterministicEntropy(1);

        using (ValueObjectIds.Use(entropy: outer))
        {
            var inner = ValueObjectIds.Use(entropy: new DeterministicEntropy(2));
            inner.Dispose();
            inner.Dispose();

            ValueObjectIds.Entropy.Should().BeSameAs(outer);
        }
    }

    /// <summary>
    /// The reason the scope exists at all. A settable static alone would let two tests racing to substitute the
    /// clock corrupt each other, and the resulting failure lands in whichever test happens to observe it.
    /// </summary>
    [Fact]
    public async Task Concurrent_flows_do_not_see_each_other_scopes()
    {
        var observed = await Task.WhenAll(Enumerable.Range(0, 64).Select(RunWithItsOwnScope));

        observed.Should().OnlyContain(pair => pair.Expected == pair.Seen);

        static async Task<(int Expected, int Seen)> RunWithItsOwnScope(int index)
        {
            await Task.Yield();

            var mine = new DeterministicEntropy((byte)index);
            using (ValueObjectIds.Use(entropy: mine))
            {
                await Task.Yield();

                var buffer = new byte[1];
                ValueObjectIds.Entropy.Fill(buffer);

                return (index, buffer[0]);
            }
        }
    }

    /// <summary>
    /// An entropy source distinct from the system one but drawing from it, so that substituting it process-wide
    /// changes which object is in effect and nothing a parallel test could observe.
    /// </summary>
    private sealed class ForwardingEntropy : IdEntropySource
    {
        public override void Fill(Span<byte> destination) => IdEntropySource.System.Fill(destination);
    }

    /// <summary>
    /// A clock distinct from the system one but reading it, for the same reason as <see cref="ForwardingEntropy"/>.
    /// </summary>
    private sealed class ForwardingClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => TimeProvider.System.GetUtcNow();
    }

    private sealed class StoppedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public StoppedClock(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
