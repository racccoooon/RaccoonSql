using System.Linq.Expressions;
using System.Reflection;
using RaccoonSql.CoreRework.Indexes;
using RaccoonSql.CoreRework.Internal.Utils;
using RaccoonSql.CoreRework.Querying;

namespace RaccoonSql.CoreRework;

public interface IQuery<TModel>
    where TModel : ModelBase
{
    IQuery<TModel> Where(Expression<Func<TModel, bool>> predicate);

    IQuery<TModel> OrderBy<TProperty>(Expression<Func<TModel, TProperty>> accessor)
        where TProperty : IComparable, IComparable<TProperty>;

    IQuery<TModel> OrderBy<TProperty>(Expression<Func<TModel, TProperty>> accessor, bool descending)
        where TProperty : IComparable, IComparable<TProperty>;

    IQuery<TModel> OrderByDescending<TProperty>(Expression<Func<TModel, TProperty>> accessor)
        where TProperty : IComparable, IComparable<TProperty>;

    IQuery<TModel> Take(int take);
    IQuery<TModel> Skip(int skip);
    
    
    QuerySelectResult<TModel> Get();
    QueryDeleteResult<TModel> Delete();
    QueryUpdateResult<TModel> Update(Expression<Action<TModel>> updater);
}

public class Query<TModel> : IQuery<TModel> where TModel : ModelBase
{
    private List<Expression<Func<TModel, bool>>> _whereClauses = [];
    private PropertyInfo? _orderByProperty;
    private bool _orderByDescending;
    private int? _take;
    private int _skip;


    public IQuery<TModel> Where(Expression<Func<TModel, bool>> predicate)
    {
        _whereClauses.Add(predicate);
        return this;
    }

    public IQuery<TModel> OrderBy<TProperty>(Expression<Func<TModel, TProperty>> accessor)
        where TProperty : IComparable, IComparable<TProperty>
    {
        return OrderBy(accessor, false);
    }

    public IQuery<TModel> OrderByDescending<TProperty>(Expression<Func<TModel, TProperty>> accessor)
        where TProperty : IComparable, IComparable<TProperty>
    {
        return OrderBy(accessor, true);
    }

    public IQuery<TModel> OrderBy<TProperty>(Expression<Func<TModel, TProperty>> accessor, bool descending)
        where TProperty : IComparable, IComparable<TProperty>
    {
        _orderByDescending = descending;
        _orderByProperty = ExpressionUtils.GetPropertyFromAccessor(accessor);
        return this;
    }


    public IQuery<TModel> Take(int take)
    {
        _take = take;
        return this;
    }

    public IQuery<TModel> Skip(int skip)
    {
        _skip = skip;
        return this;
    }



    void Plan()
    {
        if (_whereClauses.Count != 0)
        {
            ParameterExpression newParam = Expression.Parameter(typeof(TModel), typeof(TModel).Name);
            Dictionary<ParameterExpression, ParameterExpression> ParamDict(ParameterExpression param)
            {
                var paramDictionary = new Dictionary<ParameterExpression, ParameterExpression>
                {
                    [param] = newParam,
                };
                return paramDictionary;
            }
            
            var combinedBody = _whereClauses
                .Select(x => ExpressionUtils.ReplaceParams(x, ParamDict(x.Parameters[0])))
                .Select(x => x.Body)
                .Aggregate(Expression.AndAlso);

            var indices = new List<IndexBase>(); // TODO
            var whereExpr = QueryExpression.FromPredicateExpression(Expression.Lambda<Func<TModel,bool>>(combinedBody, [newParam]), indices);
            Console.WriteLine(whereExpr);
        }
    }
    

    public QuerySelectResult<TModel> Get()
    {
        Plan();
        throw new NotImplementedException();
    }

    public QueryDeleteResult<TModel> Delete()
    {
        throw new NotImplementedException();
    }

    public QueryUpdateResult<TModel> Update(Expression<Action<TModel>> updater)
    {
        throw new NotImplementedException();
    }
}