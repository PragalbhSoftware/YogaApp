namespace YogaMarketplace.Api.Tests;

/// <summary>The trial rebook flows on SQL Server, where the filtered unique index on consumed trials really applies.</summary>
public class TrialClassSqlServerTests : IClassFixture<SqlServerYogaApiFactory>
{
    private readonly TrialClassTests _tests;

    public TrialClassSqlServerTests(SqlServerYogaApiFactory factory)
    {
        _tests = new TrialClassTests(factory);
    }

    [SqlServerFact]
    public Task Rebook_after_instructor_decline_and_after_admin_cancel_passes_the_unique_trial_index() =>
        _tests.Instructor_decline_or_admin_cancel_gives_the_trial_back();
}
