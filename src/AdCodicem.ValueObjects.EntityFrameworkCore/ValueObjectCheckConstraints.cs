using System.Buffers;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Reads the code of the rule a CHECK constraint written by <c>AddValueObjectCheckConstraints</c> refused a row under,
/// from the error the database provider throws.
/// </summary>
/// <remarks>
/// <para>
/// Each constraint the convention writes is named after its rule, <c>CK_{table}_{column}_{rule}</c>, or
/// <c>CK_{table}_{column}_{rule}_{hash}</c> when the name was shortened to the provider's limit or kept apart from
/// another, and the rule maps to the code <c>Validate</c> reports for it: <c>MinLength</c> to
/// <see cref="ValueObjectErrorCodes.TooShort"/>, <c>Minimum</c> and <c>Maximum</c> to
/// <see cref="ValueObjectErrorCodes.OutOfRange"/>, <c>KnownValues</c> to
/// <see cref="ValueObjectErrorCodes.NotAKnownValue"/> and <c>Pattern</c> to
/// <see cref="ValueObjectErrorCodes.InvalidFormat"/>. The name is read off the error, never off the model: a compiled
/// model holds no check constraint.
/// </para>
/// <para>
/// Only an error reporting a violated CHECK constraint is read: PostgreSQL's SQL state <c>23514</c>, whose constraint
/// Npgsql names in <see cref="Exception.Data"/>; SQL Server's error 547, whose number SqlClient states in
/// <see cref="Exception.Data"/> in every language, the name read off its message, quoted or not; SQLite's
/// <c>CHECK constraint failed</c>. An error that names a constraint for another reason, such as the "There is already an
/// object named …" of a migration, carries no code. A constraint declared by hand under a name of the same shape is read
/// as the convention's, since its name says the same rule; a name holding a space or a quotation mark is read on
/// PostgreSQL only, which names it apart from its message.
/// </para>
/// <para>
/// The code is the only thing carried: the column is in the constraint's name unless it was shortened, in SQL Server's
/// message, and, for <c>SaveChanges</c>, the entity is in <c>DbUpdateException.Entries</c>; neither the value object's
/// type, which no name holds, nor the value is.
/// </para>
/// </remarks>
public static class ValueObjectCheckConstraints
{
    /// <summary>The rule of a minimum length, which a constraint's name ends with.</summary>
    internal const string MinLength = "MinLength";

    /// <summary>The rule of a lower bound, which a constraint's name ends with.</summary>
    internal const string Minimum = "Minimum";

    /// <summary>The rule of an upper bound, which a constraint's name ends with.</summary>
    internal const string Maximum = "Maximum";

    /// <summary>The rule of a closed value set, which a constraint's name ends with.</summary>
    internal const string KnownValues = "KnownValues";

    /// <summary>The rule of a pattern, which a constraint's name ends with.</summary>
    internal const string Pattern = "Pattern";

    /// <summary>The punctuation a message may put right after a name it does not quote, as SQL Server's Czech or Dutch.</summary>
    private const string SentencePunctuation = ".,;:!?。，、";

    /// <summary>The quotation marks a provider puts around a name, in the languages it writes its messages in.</summary>
    private static readonly SearchValues<char> Quotes = SearchValues.Create("\"'`«»‹›‘’‚“”„「」『』＂");

    /// <summary>The digits of the hash a shortened name ends with, which <c>Convert.ToHexString</c> writes.</summary>
    private static readonly SearchValues<char> Hexadecimal = SearchValues.Create("0123456789ABCDEF");

    /// <summary>
    /// Reads the code of the rule a CHECK constraint of <c>AddValueObjectCheckConstraints</c> refused, from the
    /// provider's error or from one an exception wraps.
    /// </summary>
    /// <param name="exception">The exception, as it was caught: the provider's, or the <c>DbUpdateException</c> around it.</param>
    /// <param name="code">The code, or <see langword="null"/> when no exception of the chain reports such a refusal.</param>
    /// <returns><see langword="true"/> when a code was found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Each exception is read in turn, from <paramref name="exception"/> down its <see cref="Exception.InnerException"/>
    /// chain, since Entity Framework Core fails <c>SaveChanges</c> with a <c>DbUpdateException</c> around the provider's
    /// error; the first <see cref="DbException"/> reporting a violated constraint of the convention gives the code.
    /// </para>
    /// <para>
    /// It reads the error whoever sent the command: Entity Framework Core without
    /// <see cref="ValueObjectDbContextOptionsBuilderExtensions.UseValueObjectCheckErrors(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder)"/>,
    /// Dapper, a bulk library or ADO.NET. With it, <see cref="ValueObjectErrors.TryGetCode"/> finds the same code, which
    /// the interceptor stores in the provider's error, but on an error Entity Framework Core hands no interceptor: on SQL
    /// Server, that of a raw SQL query writing rows it reads back, <c>SqlQuery</c> or <c>FromSql</c> over <c>OUTPUT</c>,
    /// which SqlClient reports while the rows are read. This reads the code there too.
    /// </para>
    /// </remarks>
    public static bool TryGetCode(Exception exception, [NotNullWhen(true)] out string? code)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return TryFind(exception, out _, out code);
    }

    /// <summary>
    /// Finds the provider's error reporting a violated CHECK constraint of the convention, in an exception or in one it
    /// wraps, and the code of the constraint's rule.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="refusal">The provider's error, or <see langword="null"/>.</param>
    /// <param name="code">The code, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when such an error was found.</returns>
    internal static bool TryFind(
        Exception exception,
        [NotNullWhen(true)] out DbException? refusal,
        [NotNullWhen(true)] out string? code)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException database && CodeOf(database) is { } found)
            {
                refusal = database;
                code = found;
                return true;
            }
        }

        refusal = null;
        code = null;
        return false;
    }

    /// <summary>
    /// Reads the code of the constraint a provider's error reports violated, if it reports one.
    /// </summary>
    private static string? CodeOf(DbException exception)
    {
        // PostgreSQL's check_violation, whose constraint Npgsql adds to the data of the exception.
        if (exception.SqlState == "23514")
        {
            return exception.Data["ConstraintName"] is string name ? CodeOfName(name) : CodeInMessage(exception.Message);
        }

        // SQL Server's error 547, which SqlClient states in the data in every language, and SQLite's own wording, which
        // it never translates.
        return exception.Data["HelpLink.EvtID"] is "547" || exception.Message.Contains("CHECK constraint failed: ", StringComparison.Ordinal)
            ? CodeInMessage(exception.Message)
            : null;
    }

    /// <summary>
    /// Reads the code of the first word of a message shaped as a name of the convention: a run of characters between white
    /// space and quotation marks, without the punctuation that may end a sentence after it.
    /// </summary>
    private static string? CodeInMessage(string message)
    {
        var start = 0;
        for (var index = 0; index <= message.Length; index++)
        {
            if (index < message.Length && !char.IsWhiteSpace(message[index]) && !Quotes.Contains(message[index]))
            {
                continue;
            }

            if (CodeOfName(message.AsSpan(start, index - start).TrimEnd(SentencePunctuation)) is { } code)
            {
                return code;
            }

            start = index + 1;
        }

        return null;
    }

    /// <summary>
    /// Reads the code of a name of the convention: <c>CK_</c>, the table and the column or what is left of them, the rule,
    /// and eight hexadecimal digits when the name was shortened or kept apart.
    /// </summary>
    private static string? CodeOfName(ReadOnlySpan<char> name)
    {
        if (!name.StartsWith("CK_", StringComparison.Ordinal))
        {
            return null;
        }

        var last = name.LastIndexOf('_');
        if (CodeOfRule(name[(last + 1)..]) is null && name.Length - last == 9 && !name[(last + 1)..].ContainsAnyExcept(Hexadecimal))
        {
            name = name[..last];
            last = name.LastIndexOf('_');
        }

        // The rule follows the table and the column, of which at least a character is left.
        return last > "CK_".Length ? CodeOfRule(name[(last + 1)..]) : null;
    }

    /// <summary>
    /// Maps a rule, as a constraint's name ends with it, to the code <c>Validate</c> reports for it.
    /// </summary>
    private static string? CodeOfRule(ReadOnlySpan<char> rule) => rule switch
    {
        MinLength => ValueObjectErrorCodes.TooShort,
        Minimum or Maximum => ValueObjectErrorCodes.OutOfRange,
        KnownValues => ValueObjectErrorCodes.NotAKnownValue,
        Pattern => ValueObjectErrorCodes.InvalidFormat,
        _ => null,
    };
}
