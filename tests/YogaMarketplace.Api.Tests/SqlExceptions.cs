using System.Reflection;
using Microsoft.Data.SqlClient;

namespace YogaMarketplace.Api.Tests;

/// <summary>
/// Builds a <see cref="SqlException"/> the way SqlClient does; its constructors are internal. Number 0 with
/// "Operation cancelled by user." is what SqlClient throws when a running command's token is cancelled.
/// </summary>
internal static class SqlExceptions
{
    public static SqlException Cancelled() => Create(0, 11, "Operation cancelled by user.");

    public static SqlException Create(int number, byte errorClass, string message)
    {
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        var constructor = typeof(SqlError).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .OrderBy(c => c.GetParameters().Length)
            .First();
        var arguments = constructor.GetParameters().Select(p => p.Name switch
        {
            "infoNumber" => number,
            "errorClass" => errorClass,
            "errorMessage" => message,
            _ when p.ParameterType == typeof(string) => "",
            _ when p.ParameterType.IsValueType => Activator.CreateInstance(p.ParameterType),
            _ => null
        }).ToArray();
        var error = (SqlError)constructor.Invoke(arguments);
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(errors, [error]);
        var create = typeof(SqlException).GetMethod(
            "CreateException",
            BindingFlags.NonPublic | BindingFlags.Static,
            [typeof(SqlErrorCollection), typeof(string)])!;
        return (SqlException)create.Invoke(null, [errors, "16.0.0"])!;
    }
}
