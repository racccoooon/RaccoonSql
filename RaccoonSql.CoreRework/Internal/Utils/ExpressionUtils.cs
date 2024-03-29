using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace RaccoonSql.CoreRework.Internal.Utils;

public static class ExpressionUtils
{
    public static Expression ExecutePartially(Expression expr)
    {
        return new PartialExecutionVisitor().Visit(expr)!;
    }

    public static Expression<T> ExecutePartially<T>(Expression<T> expr)
    {
        return Expression.Lambda<T>(ExecutePartially(expr.Body), expr.Parameters);
    }

    private class PartialExecutionVisitor : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node)
        {
            if (node is not null && node is not ConstantExpression)
            {
                var containsParametersVisitor = new ContainsParametersVisitor();
                containsParametersVisitor.Visit(node);
                if (!containsParametersVisitor.ContainsParameters)
                {
                    var valueGetter = Expression.Lambda<Func<object>>(
                        Expression.Convert(node, typeof(object)), []).Compile();
                    var value = valueGetter();
                    return Expression.Constant(value, node.Type);
                }
            }


            return base.Visit(node);
        }


        private class ContainsParametersVisitor : ExpressionVisitor
        {
            public bool ContainsParameters { get; private set; }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                ContainsParameters = true;
                return base.VisitParameter(node);
            }
        }
    }

    public static Expression<T> ReplaceParams<T>(Expression<T> expression, Dictionary<ParameterExpression, ParameterExpression> replacements)
    {
        return (Expression<T>)Expression.Lambda(
            new ReplaceParameterVisitor(replacements).VisitAndConvert(expression.Body, null),
            expression.Parameters
                .Select(x => replacements.GetValueOrDefault(x, x))
                .ToList());
    }

    private class ReplaceParameterVisitor(IReadOnlyDictionary<ParameterExpression, ParameterExpression> newNames)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (newNames.TryGetValue(node, out var param))
            {
                return param;
            }

            return base.VisitParameter(node);
        }
    }

    public static PropertyInfo GetPropertyFromAccessor<T1, T2>(Expression<Func<T1, T2>> accessor)
    {
        if (accessor.Body is MemberExpression memberExpression)
        {
            if (memberExpression.Expression is ParameterExpression parameterExpression)
            {
                if (parameterExpression == accessor.Parameters[0])
                {
                    if (memberExpression.Member is PropertyInfo propertyInfo)
                    {
                        return propertyInfo;
                    }
                }
            }
        }

        throw new ExpressionIsNotPropertyAccessorException();
    }
}

public class ExpressionIsNotPropertyAccessorException : Exception;