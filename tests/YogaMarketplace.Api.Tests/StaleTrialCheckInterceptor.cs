using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace YogaMarketplace.Api.Tests;

/// <summary>
/// Once armed, makes the next "has this customer used the trial?" query answer no, as if it ran just before another
/// request saved the first trial. SQLite serializes writers, so this is the only way a test can reach the unique index.
/// </summary>
internal sealed class StaleTrialCheckInterceptor : DbCommandInterceptor
{
    private int _armed;

    public bool Used { get; private set; }

    public void Arm() => Interlocked.Exchange(ref _armed, 1);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (!IsTrialCheck(command) || Interlocked.Exchange(ref _armed, 0) == 0)
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);

        Used = true;
        var notUsed = new DataTable();
        notUsed.Columns.Add("Value", typeof(bool));
        notUsed.Rows.Add(false);
        return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(notUsed.CreateDataReader()));
    }

    private static bool IsTrialCheck(DbCommand command) =>
        command.CommandText.TrimStart().StartsWith("SELECT EXISTS", StringComparison.OrdinalIgnoreCase)
        && command.CommandText.Contains("TrialConsumed", StringComparison.Ordinal);
}
