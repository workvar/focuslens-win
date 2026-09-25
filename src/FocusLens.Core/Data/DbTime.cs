using System.Data;
using System.Globalization;
using Dapper;

namespace FocusLens.Core.Data;

/// <summary>
/// DateTime values are stored as UTC text ("yyyy-MM-dd HH:mm:ss.fff"), the same
/// shape GRDB writes on macOS, so SQL date functions work directly on them.
/// </summary>
public static class DbTime
{
    private const string Format = "yyyy-MM-dd HH:mm:ss.fff";

    public static string ToDb(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
        return utc.ToString(Format, CultureInfo.InvariantCulture);
    }

    public static DateTime FromDb(object value)
    {
        if (value is DateTime dt) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return DateTime.SpecifyKind(
            DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            DateTimeKind.Utc);
    }

    private static int _registered;

    /// <summary>Registers Dapper handlers once per process.</summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1) return;
        SqlMapper.AddTypeHandler(new DateTimeHandler());
        SqlMapper.AddTypeHandler(new NullableDateTimeHandler());
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private sealed class DateTimeHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override DateTime Parse(object value) => FromDb(value);

        public override void SetValue(IDbDataParameter parameter, DateTime value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = ToDb(value);
        }
    }

    private sealed class NullableDateTimeHandler : SqlMapper.TypeHandler<DateTime?>
    {
        public override DateTime? Parse(object value) =>
            value is null or DBNull ? null : FromDb(value);

        public override void SetValue(IDbDataParameter parameter, DateTime? value)
        {
            parameter.DbType = DbType.String;
            parameter.Value = value is null ? DBNull.Value : ToDb(value.Value);
        }
    }
}
