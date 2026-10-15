using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Path = HotChocolate.Path;

namespace AdCodicem.ValueObjects.HotChocolate;

/// <summary>
/// Replaces the message of a value a scalar refused with the one the application's resource holds for the rule's code,
/// when <see cref="ValueObjectScalarOptions.UseResource{TResource}"/> names one.
/// </summary>
/// <remarks>
/// A scalar has no request to read a culture from: HotChocolate hands it the literal or the JSON value alone. The filter
/// runs in the request, after the scalar, with the culture <c>UseRequestLocalization()</c> set, for an argument, a
/// variable and a field alike. It quotes as <c>{0}</c> the scalar's name for a value refused in an argument or an input
/// field a predicate of <see cref="ValueObjectScalarOptions.QuoteScalarNameIn"/> selects, by the error's
/// <c>coordinate</c>, which HotChocolate sets to the innermost argument or input field it read the value under
/// (<c>IbanOperationFilterInput.eq</c> for the <c>eq</c> of a filter on an <c>Iban</c> member, whatever variable or list
/// holds it); else the argument, the input field or the variable the refusal concerns: the last name of the error's
/// <c>inputPath</c>, or else of its <c>path</c>, or else the scalar's name. Only the message changes: the code stays the
/// rule's.
/// </remarks>
internal sealed class ValueObjectErrorFilter : IErrorFilter
{
    private const string InputPath = "inputPath";

    private const string Coordinate = "coordinate";

    /// <summary>
    /// The name of the path HotChocolate reads a literal under when no argument or input field holds it, a variable's
    /// default value: <c>["root"]</c>.
    /// </summary>
    private const string ParserRoot = "root";

    private readonly ValueObjectScalarOptions _options;
    private readonly ISchemaDefinition _schema;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes the filter.
    /// </summary>
    /// <param name="options">The options of the schema's scalars.</param>
    /// <param name="schema">The schema, which the coordinate of an error is looked up in.</param>
    /// <param name="services">The application's services, which hold the localizer factory.</param>
    /// <exception cref="InvalidOperationException">
    /// The options name a resource, and the application's services hold no <c>IStringLocalizerFactory</c>.
    /// </exception>
    public ValueObjectErrorFilter(ValueObjectScalarOptions options, ISchemaDefinition schema, IServiceProvider services)
    {
        if (options.ResourceType is not null && services.GetService<IStringLocalizerFactory>() is null)
        {
            throw new InvalidOperationException(
                "UseResource() reads the messages of refused value objects from a resource, through the "
                + "IStringLocalizerFactory of the application's services, which this application does not register. Call "
                + "services.AddLocalization().");
        }

        _options = options;
        _schema = schema;
        _services = services;
    }

    /// <inheritdoc />
    public IError OnError(IError error)
    {
        if (_options.ResourceType is not { } resource || error.Exception is not ValueObjectScalarRefusal refusal)
        {
            return error;
        }

        var member = QuotesScalarName(error) ? refusal.ScalarName : MemberOf(error, refusal);
        return refusal.Localize(resource, _services, member) is { } message ? error.WithMessage(message) : error;
    }

    /// <summary>
    /// Tells whether the argument or the input field the error's coordinate names is one whose refused values the options
    /// quote by the scalar's name.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>
    /// <see langword="true"/> when a predicate of <see cref="ValueObjectScalarOptions.QuoteScalarNameIn"/> selects the
    /// argument or the input field of the error's coordinate.
    /// </returns>
    /// <remarks>
    /// A variable HotChocolate coerces before any field has no coordinate, and keeps its name.
    /// </remarks>
    private bool QuotesScalarName(IError error)
        => _options.QuotesScalarName
            && CoordinateOf(error) is { } coordinate
            && _schema.TryGetMember(coordinate, out var member)
            && member is IInputValueDefinition inputValue
            && _options.QuotesScalarNameIn(inputValue);

    /// <summary>
    /// Gets the coordinate of an error, which HotChocolate's error builder keeps as its text.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The coordinate, or <see langword="null"/> when the error has none it can read.</returns>
    private static SchemaCoordinate? CoordinateOf(IError error)
        => error.Extensions?.GetValueOrDefault(Coordinate) switch
        {
            SchemaCoordinate coordinate => coordinate,
            string text when SchemaCoordinate.TryParse(text, out var coordinate) => coordinate,
            _ => null,
        };

    /// <summary>
    /// Gets the argument, the input field or the variable a refusal concerns, quoted as <c>{0}</c>.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="refusal">The refusal it carries.</param>
    /// <returns>
    /// The last name of the error's input path, or else of its path, or else the scalar's name.
    /// </returns>
    /// <remarks>
    /// HotChocolate reads a variable's default value, a literal no argument or input field holds, under a path of its
    /// own, <c>["root"]</c>, which names nothing of the document: the scalar's name is quoted instead. A variable named
    /// <c>$root</c> is read from its JSON value, never from a literal, and keeps its name.
    /// </remarks>
    private static string MemberOf(IError error, ValueObjectScalarRefusal refusal)
    {
        if (NameOf(error.Extensions?.GetValueOrDefault(InputPath) as Path) is { } input)
        {
            return input.Name;
        }

        if (NameOf(error.Path) is not { } named || (refusal.Literal && IsParserRoot(named)))
        {
            return refusal.ScalarName;
        }

        return named.Name;
    }

    /// <summary>
    /// Tells whether a name is the first segment of a path, and HotChocolate's own for a literal no member holds.
    /// </summary>
    private static bool IsParserRoot(NamePathSegment segment) => segment.Parent.IsRoot && segment.Name == ParserRoot;

    /// <summary>
    /// Gets the last name of a path, skipping the indexes of list elements.
    /// </summary>
    /// <param name="path">The path, or <see langword="null"/>.</param>
    /// <returns>The segment of the name, or <see langword="null"/> when the path names nothing.</returns>
    private static NamePathSegment? NameOf(Path? path)
    {
        for (var segment = path; segment is { IsRoot: false }; segment = segment.Parent)
        {
            if (segment is NamePathSegment named)
            {
                return named;
            }
        }

        return null;
    }
}
