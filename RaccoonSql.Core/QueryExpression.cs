using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using RaccoonSql.Core.Utils;

namespace RaccoonSql.Core;

public abstract class QueryExpression
{
    public abstract class Visitor
    {
        public virtual QueryExpression Visit(QueryExpression expression) =>
            expression switch
            {
                And and => VisitAnd(and),
                Or or => VisitOr(or),
                Not not => VisitNot(not),
                Relation relation => VisitRelation(relation),
                Range range => VisitRange(range),
                Box box => VisitBox(box),
                _ => throw new ArgumentOutOfRangeException(nameof(expression))
            };

        protected virtual QueryExpression VisitAnd(And and)
        {
            var changed = false;
            List<QueryExpression> newTerms = [];
            foreach (var term in and.Terms)
            {
                var newTerm = Visit(term);
                if (newTerm != term)
                {
                    changed = true;
                }

                newTerms.Add(newTerm);
            }

            return changed
                ? new And
                {
                    Terms = newTerms,
                }
                : and;
        }

        protected virtual QueryExpression VisitOr(Or or)
        {
            var changed = false;
            List<QueryExpression> newTerms = [];
            foreach (var term in or.Terms)
            {
                var newTerm = Visit(term);
                if (newTerm != term)
                {
                    changed = true;
                }

                newTerms.Add(newTerm);
            }

            return changed
                ? new Or
                {
                    Terms = newTerms,
                }
                : or;
        }

        protected virtual QueryExpression VisitNot(Not not)
        {
            var newTerm = Visit(not.Term);
            return newTerm != not.Term
                ? new Not
                {
                    Term = newTerm,
                }
                : not;
        }

        protected virtual QueryExpression VisitRelation(Relation relation)
        {
            return relation;
        }

        protected virtual QueryExpression VisitRange(Range range)
        {
            return range;
        }

        protected virtual QueryExpression VisitBox(Box box)
        {
            return box;
        }
    }

    public class NormalizeVisitor : Visitor
    {
        protected override QueryExpression VisitNot(Not not)
        {
            return not.Term switch
            {
                Not innerNot => base.Visit(innerNot),
                Or or => base.Visit(new And
                {
                    Terms = or.Terms.Select(x => new Not { Term = x, }).ToList(),
                }),
                And and => base.Visit(new Or
                {
                    Terms = and.Terms.Select(x => new Not { Term = x, }).ToList(),
                }),
                _ => base.VisitNot(not)
            };
        }

        protected override QueryExpression VisitAnd(And and)
        {
            var orTerms = and.Terms.Select(x =>
                {
                    if (x is Or or) return or.Terms;
                    return [x];
                })
                .ToList();
            var crossProduct = orTerms.CrossProduct<QueryExpression>();

            Debug.Assert(crossProduct.Count != 0);

            if (crossProduct.Count == 1)
            {
                return new And
                {
                    Terms = and.Terms.Select(x => base.Visit(x)).ToList()
                };
            }

            return base.Visit(new Or
            {
                Terms = crossProduct.Select(x => new And
                {
                    Terms = x
                }).ToList(),
            });
        }
    }
    public static QueryExpression FromPredicateExpression<TModel>(Expression<Func<TModel, bool>> expression)
    {
        return ConvertExpression(expression.Body, expression.Parameters[0])
            .Normalize();
    }

    private static QueryExpression ConvertExpression(Expression expression, ParameterExpression modelParam)
    {
        switch (expression)
        {
            case BinaryExpression { NodeType: ExpressionType.AndAlso } e:
                return new And
                {
                    Terms = [ConvertExpression(e.Left, modelParam), ConvertExpression(e.Right, modelParam)]
                };
            case BinaryExpression { NodeType: ExpressionType.OrElse } e:
                return new Or
                {
                    Terms = [ConvertExpression(e.Left, modelParam), ConvertExpression(e.Right, modelParam)]
                };
            case BinaryExpression e:
                var type = Relation.MatchExpressionType(e.NodeType);
                if (type == null) break;

                var left = ExpressionUtils.ExecutePartially(e.Left);
                var right = ExpressionUtils.ExecutePartially(e.Right);
                ConstantExpression? value = null;
                QueryExpressionModelField? field = null;

                switch (left, right)
                {
                    case (ConstantExpression l, MemberExpression r):
                        value = l;
                        field = QueryExpressionModelField.FromExpression(r, modelParam);
                        type = Relation.Mirror(type.Value);
                        break;
                    case (MemberExpression l, ConstantExpression r):
                        value = r;
                        field = QueryExpressionModelField.FromExpression(l, modelParam);
                        break;
                }

                if (field == null) break;

                return new Relation
                {
                    Field = field,
                    Value = new QueryExpressionValue.Constant
                    {
                        Type = value!.Type,
                        Value = value.Value,
                    },
                    Type = type!.Value,
                };

            case UnaryExpression { NodeType: ExpressionType.Not } e when e.Type == typeof(bool):
                return new Not { Term = ConvertExpression(e.Operand, modelParam) };
        }

        return new Box
        {
            BoxedExpression = Expression.Lambda(ExpressionUtils.ExecutePartially(expression), [modelParam])
        };
    }

    private QueryExpression Normalize()
    {
        return new NormalizeVisitor().Visit(this);
    }

    public class And : QueryExpression
    {
        public required IReadOnlyList<QueryExpression> Terms { get; init; }

        public override string ToString()
        {
            return "(" + string.Join(" && ", Terms) + ")";
        }
    }

    public class Or : QueryExpression
    {
        public required IReadOnlyList<QueryExpression> Terms { get; init; }

        public override string ToString()
        {
            return "(" + string.Join(" || ", Terms) + ")";
        }
    }

    public class Not : QueryExpression
    {
        public required QueryExpression Term { get; init; }

        public override string ToString()
        {
            return "!(" + Term + ")";
        }
    }

    public class Relation : QueryExpression
    {
        public enum RelationType
        {
            Equal,
            NotEqual,
            LessThan,
            GreaterThan,
            LessThanOrEqual,
            GreaterThanOrEqual
        }

        public static RelationType? MatchExpressionType(ExpressionType t)
        {
            return t switch
            {
                ExpressionType.GreaterThan => RelationType.GreaterThan,
                ExpressionType.GreaterThanOrEqual => RelationType.GreaterThanOrEqual,
                ExpressionType.LessThan => RelationType.LessThan,
                ExpressionType.LessThanOrEqual => RelationType.LessThanOrEqual,
                ExpressionType.NotEqual => RelationType.NotEqual,
                ExpressionType.Equal => RelationType.Equal,
                _ => null
            };
        }

        public required RelationType Type { get; init; }
        public required QueryExpressionModelField Field { get; init; }
        public required QueryExpressionValue Value { get; init; }

        public static RelationType? Mirror(RelationType type)
        {
            return type switch
            {
                RelationType.LessThan => RelationType.GreaterThan,
                RelationType.GreaterThan => RelationType.LessThan,
                RelationType.LessThanOrEqual => RelationType.GreaterThanOrEqual,
                RelationType.GreaterThanOrEqual => RelationType.LessThanOrEqual,
                _ => type
            };
        }

        public override string ToString()
        {
            return "(" + Field + " " + Type switch
            {
                RelationType.Equal => "==",
                RelationType.NotEqual => "!=",
                RelationType.LessThan => "<",
                RelationType.GreaterThan => ">",
                RelationType.LessThanOrEqual => "<=",
                RelationType.GreaterThanOrEqual => ">=",
                _ => throw new ArgumentOutOfRangeException()
            } + " " + Value + ")";
        }
    }


    public class Range : QueryExpression
    {
        public required QueryExpressionModelField Field { get; set; }
        public required QueryExpressionValue From { get; set; }
        public required QueryExpressionValue To { get; set; }

        public override string ToString()
        {
            return "[" + From + " < " + Field + " < " + To + "]";
        }
    }

    public class Box : QueryExpression
    {
        public required LambdaExpression BoxedExpression { get; set; }

        public override string ToString()
        {
            return BoxedExpression.ToString();
        }
    }
}

public abstract class QueryExpressionValue
{
    public required Type Type { get; init; }

    public class Constant : QueryExpressionValue
    {
        public required object? Value { get; init; }

        public override string ToString()
        {
            return Value?.ToString() ?? "null";
        }
    }

    public class Parameter : QueryExpressionValue
    {
        public required object Identifier { get; init; }

        public override string ToString()
        {
            return "$Param(" + Identifier + ")";
        }
    }
}

public class QueryExpressionModelField
{
    public required PropertyInfo PropertyInfo { get; init; }

    public static QueryExpressionModelField? FromExpression(MemberExpression expression, ParameterExpression modelParam)
    {
        if (expression.Expression != modelParam) return null;
        if (expression.Member is not PropertyInfo member) return null;
        return new QueryExpressionModelField
        {
            PropertyInfo = member
        };
    }

    public override string ToString()
    {
        return PropertyInfo.Name;
    }
}


public static class ListExtensions
{
    public static List<List<T>> CrossProduct<T>(this IEnumerable<IReadOnlyList<T>> terms)
    {
        List<List<T>> crossProduct = [[]];
        foreach (var list in terms)
        {
            switch (list.Count)
            {
                case 0:
                    return []; // if any of the input lists has no elements then the cross product is empty
                case 1:
                {
                    foreach (var list1 in crossProduct)
                    {
                        list1.Add(list[0]);
                    }

                    break;
                }
                default:
                {
                    List<List<T>> crossProductNew = [];
                    foreach (var list1 in crossProduct)
                    {
                        foreach (var val in list)
                        {
                            crossProductNew.Add([..list1, val]);
                        } 
                    }

                    crossProduct = crossProductNew;
                    break;
                }
            }
        }

        return crossProduct;
    }
}