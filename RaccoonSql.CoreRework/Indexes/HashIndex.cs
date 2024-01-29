using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using RaccoonSql.CoreRework.Querying;

namespace RaccoonSql.CoreRework.Indexes;

public class HashIndex(PropertyInfo propertyInfo) : IndexBase(propertyInfo)
{
    public override bool SupportsOrderedScan => false;
    
    public override bool TryConvertExpression(
        Expression expression,
        ParameterExpression modelParam,
        [NotNullWhen(true)] out IndexQueryExpression? result)
    {
        result = null;

        if (expression is not BinaryExpression { NodeType: ExpressionType.Equal } eq) 
            return false;
        
        ConstantExpression c;
        MemberExpression m;
        
        switch (eq.Left, eq.Right)
        {
            case (ConstantExpression l, MemberExpression r):
                c = l;
                m = r;
                break;
            case (MemberExpression l, ConstantExpression r):
                c = r;
                m = l;
                break;
            default:
                return false;
        }
            
        if (c.Value == null) return false;

        var field = QueryExpressionModelField.FromExpression(m, modelParam);
        
        if (field is null || !field.PropertyInfo.Equals(PropertyInfo))
            return false;
        
        result = new HashIndexEquality
        {
            Field = field,
            Value = c.Value,
        };
        
        return true;
    }

    public override IEnumerable<ModelBase> Scan(
        List<IndexQueryExpression> query, 
        IndexScanOrder? order)
    {
        if (order.HasValue) throw new IndexDoesNotSupportOrderedScan();

        throw new NotImplementedException();
    }
}

public class HashIndexEquality : IndexQueryExpression
{
    public required object Value { get; init; }
    
    public override bool Equals(QueryExpression? other)
    {
        if (other is not HashIndexEquality otherEquality) return false;
        if (!Field.Equals(otherEquality.Field)) return false;
        return Value == otherEquality.Value;
    }

    public override bool TrySimplify([NotNullWhen(true)] out QueryExpression? result)
    {
        // TODO: if the hash index knows about whether the data type is null then it can check for that and simplify accordingly
        result = null;
        return false;
    }

    public override bool TryIntersect(
        IndexQueryExpression other,
        [NotNullWhen(true)] out QueryExpression? result)
    {
        if (Equals(other))
        {
            result = this;
            return true;
        }

        result = null;
        return false;
    }

    public override bool TryUnion(
        IndexQueryExpression other, 
        [NotNullWhen(true)] out QueryExpression? result)
    {
        if (Equals(other))
        {
            result = this;
            return true;
        }

        result = null;
        return false;
    }

    public override bool TryInvert(
        [NotNullWhen(true)] out QueryExpression? result)
    {
        result = null;
        return false;
    }

    public override Expression ToExpression(ParameterExpression propertyParameter)
    {
        return Expression.Equal(propertyParameter, Expression.Constant(Value));
    }
}