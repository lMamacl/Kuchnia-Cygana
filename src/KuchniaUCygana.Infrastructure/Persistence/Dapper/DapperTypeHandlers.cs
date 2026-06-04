using System.Data;
using Dapper;

namespace KuchniaUCygana.Infrastructure.Persistence.TypeHandlers;

public static class DapperTypeHandlers
{
    private static bool registered;

    /// <summary>
    /// Ensures the custom Dapper type handler for DateTimeOffset is registered; safe to call multiple times.
    /// </summary>
    public static void Register()
    {
        if (registered)
        {
            return;
        }

        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
        registered = true;
    }

    private sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        /// <summary>
        /// Converts a database value into a <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <param name="value">The value to convert; expected to be a <see cref="DateTimeOffset"/> or a <see cref="DateTime"/>.</param>
        /// <returns>The resulting <see cref="DateTimeOffset"/>. If a <see cref="DateTime"/> is provided, it is treated as UTC.</returns>
        /// <exception cref="DataException">Thrown when <paramref name="value"/> is not a supported type.</exception>
        public override DateTimeOffset Parse(object value) => value switch
        {
            DateTimeOffset dto => dto,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => throw new DataException($"Cannot convert {value.GetType().Name} to DateTimeOffset."),
        };

        /// <summary>
        /// Sets the given database parameter to represent the specified <see cref="DateTimeOffset"/> as a UTC <see cref="DateTime"/> and marks it as <see cref="DbType.DateTime2"/>.
        /// </summary>
        /// <param name="parameter">The database parameter to configure; its <see cref="IDbDataParameter.DbType"/> will be set to <see cref="DbType.DateTime2"/> and its <see cref="IDbDataParameter.Value"/> to the UTC <see cref="DateTime"/>.</param>
        /// <param name="value">The <see cref="DateTimeOffset"/> value to store; its UTC <see cref="DateTime"/> portion will be used.</param>
        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.DbType = DbType.DateTime2;
            parameter.Value = value.UtcDateTime;
        }
    }
}
