using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Adapters;
using Microsoft.AspNetCore.JsonPatch.Exceptions;
using Microsoft.AspNetCore.JsonPatch.Internal;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Newtonsoft.Json.Serialization;

namespace AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson.Patching;

/// <summary>
/// JSON Patch's <see cref="ObjectAdapter"/> over the adapters of <see cref="ValueObjectAdapterFactory"/>, but for
/// <c>copy</c> and <c>move</c>, which it does itself.
/// </summary>
/// <remarks>
/// <para>
/// JSON Patch copies a value by writing it to text and reading it back under its default settings, which gives the
/// default instance of a value object written by hand and fails for a <see cref="DateTimeOffset"/>: a value object is
/// immutable, so the instance is copied as it is. JSON Patch's <c>move</c> adds the value at its target even when the
/// removal at its source failed: a source this refuses to remove is not moved.
/// </para>
/// <para>
/// An operation that names no <c>path</c>, or a <c>copy</c> or a <c>move</c> that names no <c>from</c>, is reported,
/// where JSON Patch throws an <see cref="ArgumentNullException"/>. Every error is reported under the location it
/// concerns, JSON Patch's own through <see cref="Log"/>.
/// </para>
/// </remarks>
internal sealed class ValueObjectObjectAdapter : IObjectAdapterWithTest
{
    private readonly ObjectAdapter _inner;
    private readonly IContractResolver _resolver;
    private readonly IAdapterFactory _factory;
    private readonly ValueObjectPatchConversion _conversion;
    private readonly Action<JsonPatchError, string?> _report;

    /// <summary>
    /// The operation whose <c>from</c> this reported as unreadable, which JSON Patch then reports again as it stops.
    /// </summary>
    private Operation? _stopped;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectObjectAdapter"/> class, over JSON Patch's adapter, which
    /// reports its errors through <see cref="Log"/>.
    /// </summary>
    /// <param name="resolver">The document's contract resolver.</param>
    /// <param name="factory">The factory of the adapters.</param>
    /// <param name="conversion">The conversion of the document's values.</param>
    /// <param name="report">Reports an error, with the location it concerns.</param>
    public ValueObjectObjectAdapter(
        IContractResolver resolver,
        IAdapterFactory factory,
        ValueObjectPatchConversion conversion,
        Action<JsonPatchError, string?> report)
    {
        _inner = new ObjectAdapter(resolver, Log, factory);
        _resolver = resolver;
        _factory = factory;
        _conversion = conversion;
        _report = report;
    }

    /// <summary>
    /// Reports an error JSON Patch hands over, under the operation's <c>path</c>, but for one this reported already.
    /// </summary>
    /// <remarks>
    /// JSON Patch reports an operation it cannot read under that operation, and stops there. A <c>from</c> it cannot
    /// read is reported under the <c>from</c> as it is read, before JSON Patch reports it again.
    /// </remarks>
    /// <param name="error">The error.</param>
    public void Log(JsonPatchError error)
    {
        if (!ReferenceEquals(error.Operation, _stopped))
        {
            _report(error, error.Operation.path);
        }
    }

    /// <inheritdoc />
    public void Add(Operation operation, object objectToApplyTo)
    {
        if (Located(operation, objectToApplyTo))
        {
            _inner.Add(operation, objectToApplyTo);
        }
    }

    /// <inheritdoc />
    public void Remove(Operation operation, object objectToApplyTo)
    {
        if (Located(operation, objectToApplyTo))
        {
            _inner.Remove(operation, objectToApplyTo);
        }
    }

    /// <inheritdoc />
    public void Replace(Operation operation, object objectToApplyTo)
    {
        if (Located(operation, objectToApplyTo))
        {
            _inner.Replace(operation, objectToApplyTo);
        }
    }

    /// <inheritdoc />
    public void Test(Operation operation, object objectToApplyTo)
    {
        if (Located(operation, objectToApplyTo))
        {
            _inner.Test(operation, objectToApplyTo);
        }
    }

    /// <inheritdoc />
    public void Copy(Operation operation, object objectToApplyTo)
    {
        if (!Located(operation, objectToApplyTo) || !TryGet(operation, objectToApplyTo, out _, out _, out _, out var value))
        {
            return;
        }

        // A value object is immutable: the instance is the copy. Anything else is copied as JSON Patch copies it.
        if (value is null || !ValueObjectPatchConversion.Handles(value.GetType()))
        {
            var copy = ConversionResultProvider.CopyTo(value!, value?.GetType()!);
            if (!copy.CanBeConverted)
            {
                _report(new JsonPatchError(objectToApplyTo, operation, $"The property at '{operation.from}' could not be copied."), operation.from);
                return;
            }

            value = copy.ConvertedInstance;
        }

        TryAdd(operation, objectToApplyTo, value);
    }

    /// <inheritdoc />
    public void Move(Operation operation, object objectToApplyTo)
    {
        if (!Located(operation, objectToApplyTo) || !TryGet(operation, objectToApplyTo, out var parent, out var adapter, out var from, out var value))
        {
            return;
        }

        // RFC 6902 moves a value to the location it comes from by leaving it there, where removing it first would
        // refuse a value object that cannot be null.
        if (from.Segments.SequenceEqual(new ParsedPath(operation.path).Segments, StringComparer.Ordinal))
        {
            return;
        }

        // The value is removed from the parent it was found in, and added at its target only once it is.
        if (!adapter.TryRemove(parent, from.LastSegment, _resolver, out var message))
        {
            Fail(operation, objectToApplyTo, message, operation.from);
            return;
        }

        TryAdd(operation, objectToApplyTo, value);
    }

    /// <summary>
    /// Starts an operation, and reports one that names no location where it needs one.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="root">The object the document patches.</param>
    /// <returns><see langword="true"/> when the operation names every location it needs.</returns>
    private bool Located(Operation operation, object root)
    {
        _conversion.Clear();
        var missing = operation.path is null
            ? "path"
            : operation.OperationType is OperationType.Copy or OperationType.Move && operation.from is null ? "from" : null;
        if (missing is null)
        {
            return true;
        }

        _report(new JsonPatchError(root, operation, $"The '{operation.op}' operation has no '{missing}'."), operation.path);
        return false;
    }

    /// <summary>
    /// Gets the value at the operation's <c>from</c>, with the object holding it and that object's adapter.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="root">The object the document patches.</param>
    /// <param name="parent">The object holding the value.</param>
    /// <param name="adapter">The adapter of <paramref name="parent"/>.</param>
    /// <param name="from">The operation's <c>from</c>, read.</param>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value was found.</returns>
    private bool TryGet(Operation operation, object root, out object parent, out IAdapter adapter, out ParsedPath from, out object? value)
    {
        value = null;
        from = ReadFrom(operation, root);
        parent = root;
        if (!new ObjectVisitor(from, _resolver, _factory).TryVisit(ref parent, out adapter, out var message)
            || !adapter.TryGet(parent, from.LastSegment, _resolver, out value, out message))
        {
            Fail(operation, root, message, operation.from);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Reads the operation's <c>from</c>.
    /// </summary>
    /// <remarks>
    /// A <c>from</c> JSON Patch cannot read, a bad escape, is reported under the <c>from</c>, and its exception thrown
    /// again, so that JSON Patch stops the document there, as it does at any path it cannot read.
    /// </remarks>
    /// <param name="operation">The operation.</param>
    /// <param name="root">The object the document patches.</param>
    /// <returns>The <c>from</c>, read.</returns>
    private ParsedPath ReadFrom(Operation operation, object root)
    {
        try
        {
            return new ParsedPath(operation.from);
        }
        catch (JsonPatchException exception)
        {
            _report(new JsonPatchError(root, operation, exception.Message), operation.from);
            _stopped = operation;
            throw;
        }
    }

    /// <summary>
    /// Adds a value at the operation's <c>path</c>.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="root">The object the document patches.</param>
    /// <param name="value">The value.</param>
    private void TryAdd(Operation operation, object root, object? value)
    {
        var path = new ParsedPath(operation.path);
        var target = root;
        if (!new ObjectVisitor(path, _resolver, _factory).TryVisit(ref target, out var adapter, out var message)
            || !adapter.TryAdd(target, path.LastSegment, _resolver, value!, out message))
        {
            Fail(operation, root, message, operation.path);
        }
    }

    /// <summary>
    /// Reports a step that failed at a location.
    /// </summary>
    /// <remarks>
    /// Every adapter, JSON Patch's and this package's, gives a message when it fails to get, add or remove a value. The
    /// visitor gives none for a location under a <see langword="null"/> or under a dictionary key it does not hold, which
    /// takes JSON Patch's own message for it.
    /// </remarks>
    /// <param name="operation">The operation.</param>
    /// <param name="root">The object the document patches.</param>
    /// <param name="message">The message of the step, or <see langword="null"/>.</param>
    /// <param name="location">The location.</param>
    private void Fail(Operation operation, object root, string? message, string location)
        => _report(
            new JsonPatchError(
                root,
                operation,
                message ?? $"For operation '{operation.op}', the target location specified by path '{location}' was not found."),
            location);
}
