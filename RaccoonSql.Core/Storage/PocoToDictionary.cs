using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace RaccoonSql.Core.Storage;

public static class PocoToDictionary
{
    private static readonly MethodInfo AddToDictionaryMethod = typeof(IDictionary<string, object>)
        .GetMethod("Add")!;

    private static readonly ConcurrentDictionary<Type, Func<object, IDictionary<string, object>>> Converters = new();

    private static readonly ConstructorInfo DictionaryConstructor = 
        typeof(Dictionary<string, object?>)
            .GetConstructors()
            .FirstOrDefault(c => c.IsPublic && c.GetParameters().Length == 0)!;

    public static IDictionary<string, object?> ToDictionary(object obj) =>
        obj == default
            ? new Dictionary<string, object?>()
            : Converters.GetOrAdd(obj.GetType(), o =>
            {
                var outputType = typeof(IDictionary<string, object>);
                var inputType = obj.GetType();
                var inputExpression = Expression.Parameter(typeof(object), "input");
                var typedInputExpression = Expression.Convert(inputExpression, inputType);
                var outputVariable = Expression.Variable(outputType, "output");
                var returnTarget = Expression.Label(outputType);
                var body = new List<Expression>
                {
                    Expression.Assign(outputVariable, Expression.New(DictionaryConstructor))
                };
                body.AddRange(
                    from prop in inputType.GetProperties(BindingFlags.Instance | BindingFlags.Public |
                                                         BindingFlags.FlattenHierarchy)
                    where prop.CanRead && (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string))
                    let getExpression = Expression.Property(typedInputExpression, prop.GetMethod)
                    let convertExpression = Expression.Convert(getExpression, typeof(object))
                    select Expression.Call(outputVariable, AddToDictionaryMethod, Expression.Constant(prop.Name),
                        convertExpression));
                body.Add(Expression.Return(returnTarget, outputVariable));
                body.Add(Expression.Label(returnTarget, Expression.Constant(null, outputType)));

                var lambdaExpression = Expression.Lambda<Func<object, IDictionary<string, object>>>(
                    Expression.Block(new[] { outputVariable }, body),
                    inputExpression);

                return lambdaExpression.Compile();
            })(obj);
}