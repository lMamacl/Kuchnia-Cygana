using System.Data;
using Dapper;

namespace KuchniaUCygana.Infrastructure.Persistence.TypeHandlers;

public static class DapperTypeHandlers
{
    private static bool registered;

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
        public override DateTimeOffset Parse(object value) => value switch
        {
            DateTimeOffset dto => dto,
            DateTime dateTime => new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)),
            _ => throw new DataException($"Cannot convert {value.GetType().Name} to DateTimeOffset."),
        };

        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.DbType = DbType.DateTime2;
            parameter.Value = value.UtcDateTime;
        }
    }
}
