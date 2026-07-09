using Dapper;

namespace Declaration.API;

public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override void SetValue(System.Data.IDbDataParameter parameter, Guid value) 
        => parameter.Value = value.ToString();

    public override Guid Parse(object value) 
        => Guid.Parse(value.ToString()!);
}
