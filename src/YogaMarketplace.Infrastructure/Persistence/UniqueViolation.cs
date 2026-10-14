using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace YogaMarketplace.Infrastructure.Persistence;

/// <summary>Tells a duplicate-key save failure apart from every other one.</summary>
public static class UniqueViolation
{
    private const int SqlServerUniqueIndex = 2601;
    private const int SqlServerPrimaryKey = 2627;
    private const int SqliteConstraint = 19;

    public static bool Is(DbUpdateException exception) => exception.InnerException switch
    {
        SqlException sql => sql.Number is SqlServerUniqueIndex or SqlServerPrimaryKey,
        // The tests' SQLite provider isn't referenced here, so its error code is read by name.
        { } other when other.GetType().Name == "SqliteException" =>
            other.GetType().GetProperty("SqliteErrorCode")?.GetValue(other) is SqliteConstraint,
        _ => false
    };
}
