namespace YogaMarketplace.Api.Tests;

/// <summary>
/// A test that needs a real SQL Server (migrations are SQL Server only). Set YOGA_TEST_SQLSERVER to a connection string
/// without a database name; each test creates and drops its own database. Skipped when the variable is not set.
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public const string Variable = "YOGA_TEST_SQLSERVER";

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"Set {Variable} to run SQL Server migration tests.";
    }

    public static string ConnectionFor(string database) =>
        new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(Environment.GetEnvironmentVariable(Variable))
        {
            InitialCatalog = database
        }.ConnectionString;
}
