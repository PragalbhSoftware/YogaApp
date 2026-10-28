using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace YogaMarketplace.Infrastructure.Persistence;

/// <summary>Tells a foreign-key save failure apart from every other one.</summary>
public static class ForeignKeyViolation
{
    private const int SqlServerForeignKey = 547;
    private const int SqliteConstraint = 19;

    public static bool Is(DbUpdateException exception) => exception.InnerException switch
    {
        SqlException sql => sql.Number is SqlServerForeignKey,
        // The tests' SQLite provider isn't referenced here, so its error code is read by name.
        { } other when other.GetType().Name == "SqliteException" =>
            other.GetType().GetProperty("SqliteErrorCode")?.GetValue(other) is SqliteConstraint
            && other.Message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase),
        _ => false
    };
}
