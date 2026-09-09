using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace AdCodicem.ValueObjects.Generators.Internal;

/// <summary>
/// An immutable array with structural equality, so that models flowing through the incremental pipeline are
/// cached instead of being regenerated on every keystroke.
/// </summary>
/// <typeparam name="T">Element type.</typeparam>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _items;

    public EquatableArray(ImmutableArray<T> items) => _items = items;

    public static EquatableArray<T> Empty { get; } = new(ImmutableArray<T>.Empty);

    public int Length => _items.IsDefault ? 0 : _items.Length;

    public bool IsEmpty => Length == 0;

    public T this[int index] => _items[index];

    public static EquatableArray<T> From(IEnumerable<T> items) => new(items.ToImmutableArray());

    public bool Equals(EquatableArray<T> other)
    {
        if (Length != other.Length)
        {
            return false;
        }

        for (var i = 0; i < Length; i++)
        {
            if (!_items[i].Equals(other._items[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        for (var i = 0; i < Length; i++)
        {
            hash = (hash * 31) + _items[i].GetHashCode();
        }

        return hash;
    }

    public IEnumerator<T> GetEnumerator() => (_items.IsDefault ? Enumerable.Empty<T>() : _items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
