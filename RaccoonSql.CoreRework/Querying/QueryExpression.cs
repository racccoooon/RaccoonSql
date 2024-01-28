using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using RaccoonSql.CoreRework.Indexes;
using RaccoonSql.CoreRework.Internal.Utils;

namespace RaccoonSql.CoreRework.Querying;

public abstract class QueryExpression : IEquatable<QueryExpression>
{
    public abstract class Visitor
    {
        public QueryExpression Visit(QueryExpression expression) =>
            expression switch
            {
                And and => VisitAnd(and),
                Or or => VisitOr(or),
                Not not => VisitNot(not),
                IndexQueryExpression queryExpression => VisitIndexQueryExpression(queryExpression),
                Box box => VisitBox(box),
                _ => throw new ArgumentOutOfRangeException(nameof(expression)),
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

        protected virtual QueryExpression VisitIndexQueryExpression(IndexQueryExpression queryExpression)
        {
            return queryExpression;
        }

        protected virtual QueryExpression VisitBox(Box box)
        {
            return box;
        }
    }

    private class NormalizeVisitor : Visitor
    {
        protected override QueryExpression VisitNot(Not not)
        {
            var transformedNot = base.VisitNot(not);
            if (transformedNot is not Not not2)
            {
                return transformedNot;
            }

            not = not2;

            return not.Term switch
            {
                Not innerNot => Visit(innerNot.Term),
                Or or => Visit(new And
                {
                    Terms = or.Terms.Select(x => new Not
                        {
                            Term = x,
                        })
                        .ToList(),
                }),
                And and => Visit(new Or
                {
                    Terms = and.Terms.Select(x => new Not
                        {
                            Term = x,
                        })
                        .ToList(),
                }),
                Box { IsTrue: true } => Box.False(),
                Box { IsFalse: true } => Box.True(),
                IndexQueryExpression indexQuery => indexQuery.TryInvert(out var inverted) ? Visit(inverted) : indexQuery,
                _ => not,
            };
        }

        protected override QueryExpression VisitOr(Or or)
        {
            var transformedOr = base.VisitOr(or);
            if (transformedOr is not Or or2)
            {
                return transformedOr;
            }

            or = or2;

            if (or.Terms.Count == 1)
            {
                return or.Terms[0];
            }


            var newTerms = or.Terms
                .SelectMany(x => x is Or o ? o.Terms : [x])
                .ToList();
            if (newTerms.Count != or.Terms.Count)
                return Visit(new Or
                {
                    Terms = newTerms
                });


            newTerms = new List<QueryExpression>(or.Terms.Count);

            foreach (var term in or.Terms)
            {
                switch (term)
                {
                    case Not notTerm when or.Terms.Any(term2 => term != term2 && notTerm.Term.Equals(term2)):
                        return Box.True();
                    case Box {IsTrue: true} box:
                        return box;
                    default:
                        newTerms.Add(term);
                        break;
                }
            }

            if (newTerms.Count == 0)
            {
                // we return a box with a lambda that takes no params
                // such a lambda would be invalid in the context of a query expression,
                // but it will be normalised away before the query expression is returned
                return Box.False();
            }

            newTerms = newTerms.Distinct().ToList();

            if (newTerms.Count != or.Terms.Count)
            {
                return Visit(new Or
                {
                    Terms = newTerms
                });
            }

            foreach (var term1 in or.Terms)
            {
                foreach (var term2 in or.Terms)
                {
                    if (term1 == term2) continue;
                    if (term1 is not IndexQueryExpression indexQuery1 || term2 is not IndexQueryExpression indexQuery2) continue;
                    if (!indexQuery1.Field.Equals(indexQuery2.Field)) continue;
                    if (indexQuery1.TryUnion(indexQuery2, out var union))
                    {
                        return Visit(new Or
                        {
                            Terms = or.Terms.Except([indexQuery1, indexQuery2]).Append(union).ToList(),
                        });
                    }
                }
            }

            return or;
        }

        protected override QueryExpression VisitAnd(And and)
        {
            var transformedAnd = base.VisitAnd(and);
            if (transformedAnd is not And and2)
            {
                return transformedAnd;
            }

            and = and2;


            if (and.Terms.Count == 1)
            {
                return and.Terms[0];
            }

            var newTerms = and.Terms
                .SelectMany(x => x is And a ? a.Terms : [x])
                .ToList();

            if (newTerms.Count != and.Terms.Count)
            {
                return Visit(new And
                {
                    Terms = newTerms
                });
            }

            var orTerms = and.Terms.Select(x =>
                {
                    if (x is Or or) return or.Terms;
                    return [x];
                })
                .ToList();
            var crossProduct = orTerms.CrossProduct();

            Debug.Assert(crossProduct.Count != 0);

            if (crossProduct.Count != 1)
                return Visit(new Or
                {
                    Terms = crossProduct.Select(x => new And
                    {
                        Terms = x
                    }).ToList(),
                });


            newTerms = new List<QueryExpression>(and.Terms.Count);

            foreach (var term in and.Terms)
            {
                switch (term)
                {
                    case Not notTerm when and.Terms.Any(term2 => term2 != term && notTerm.Term.Equals(term2)):
                        return Box.False();
                    case Box {IsFalse: true} box:
                        return box;
                    default:
                        newTerms.Add(term);
                        break;
                }
            }

            if (newTerms.Count == 0)
            {
                // we return a box with a lambda that takes no params
                // such a lambda would be invalid in the context of a query expression,
                // but it will be normalised away before the query expression is returned
                return new Box
                {
                    BoxedExpression = Expression.Lambda(Expression.Constant(true))
                };
            }

            newTerms = newTerms.Distinct().ToList();

            if (newTerms.Count != and.Terms.Count)
            {
                return Visit(new And
                {
                    Terms = newTerms
                });
            }
            
            foreach (var term1 in and.Terms)
            {
                foreach (var term2 in and.Terms)
                {
                    if (term1 == term2) continue;
                    if (term1 is not IndexQueryExpression indexQuery1 || term2 is not IndexQueryExpression indexQuery2) continue;
                    if (!indexQuery1.Field.Equals(indexQuery2.Field)) continue;
                    if (indexQuery1.TryIntersect(indexQuery2, out var intersection))
                    {
                        return Visit(new And
                        {
                            Terms = and.Terms.Except([indexQuery1, indexQuery2]).Append(intersection).ToList(),
                        });
                    }
                    return Box.False();
                }
            }

            return and;
        }

        protected override QueryExpression VisitIndexQueryExpression(IndexQueryExpression queryExpression)
        {
            if (queryExpression.IsTriviallyTrue)
            {
                return Box.True();
            }
            if (queryExpression.IsTriviallyFalse)
            {
                return Box.False();
            }
            return base.VisitIndexQueryExpression(queryExpression);
        }
    }

    public static QueryExpression FromPredicateExpression<TModel>(Expression<Func<TModel, bool>> expression, List<IndexBase> indices)
    {
        return ConvertExpression(ExpressionUtils.ExecutePartially(expression.Body), expression.Parameters[0], indices)
            .Normalize();
    }

    private static QueryExpression ConvertExpression(Expression expression, ParameterExpression modelParam, List<IndexBase> indices)
    {
        foreach (var index in indices)
        {
            if (index.TryConvertExpression(expression, modelParam, out var indexQueryExpression))
            {
                return indexQueryExpression;
            }
        }

        switch (expression)
        {
            case BinaryExpression { NodeType: ExpressionType.AndAlso } e:
                return new And
                {
                    Terms = [ConvertExpression(e.Left, modelParam, indices), ConvertExpression(e.Right, modelParam, indices)]
                };
            case BinaryExpression { NodeType: ExpressionType.OrElse } e:
                return new Or
                {
                    Terms = [ConvertExpression(e.Left, modelParam, indices), ConvertExpression(e.Right, modelParam, indices)],
                };
            case UnaryExpression { NodeType: ExpressionType.Not } e when e.Type == typeof(bool):
                return new Not
                {
                    Term = ConvertExpression(e.Operand, modelParam, indices)
                };
        }

        return new Box
        {
            BoxedExpression = Expression.Lambda(ExpressionUtils.ExecutePartially(expression), [modelParam]),
        };
    }

    public QueryExpression Normalize()
    {
        return new NormalizeVisitor().Visit(this);
    }

    public class And : QueryExpression
    {
        public required IReadOnlyList<QueryExpression> Terms { get; init; }

        public override string ToString()
        {
            return "(" + string.Join(" AND ", Terms) + ")";
        }


        public override bool Equals(object? obj)
        {
            if (obj is not And otherAnd) return false;
            if (Terms.Count != otherAnd.Terms.Count) return false;
            return Terms.All(x => otherAnd.Terms.Contains(x));
        }

        public override int GetHashCode()
        {
            return 0; // TODO: make an actually useful hashcode
        }

        public override bool Equals(QueryExpression? other)
        {
            return Equals((object?)other);
        }
    }

    public class Or : QueryExpression
    {
        public required IReadOnlyList<QueryExpression> Terms { get; init; }

        public override string ToString()
        {
            return "(" + string.Join(" OR ", Terms) + ")";
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Or otherOr) return false;
            if (Terms.Count != otherOr.Terms.Count) return false;
            return Terms.All(x => otherOr.Terms.Contains(x));
        }

        public override int GetHashCode()
        {
            return 0; // TODO: make an actually useful hashcode
        }

        public override bool Equals(QueryExpression? other)
        {
            return Equals((object?)other);
        }
    }

    public class Not : QueryExpression
    {
        public required QueryExpression Term { get; init; }

        public override string ToString()
        {
            return "!(" + Term + ")";
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Not otherNot) return false;
            return Term.Equals(otherNot.Term);
        }

        public override int GetHashCode()
        {
            return 0; // TODO: make an actually useful hashcode
        }

        public override bool Equals(QueryExpression? other)
        {
            return Equals((object?)other);
        }
    }

    public class Box : QueryExpression
    {
        public bool IsTrue => BoxedExpression.Body is ConstantExpression constantExpression && constantExpression.Value as bool? == true;
        public bool IsFalse => BoxedExpression.Body is ConstantExpression constantExpression && constantExpression.Value as bool? == false;
        
        public required LambdaExpression BoxedExpression { get; init; }

        public override string ToString()
        {
            return BoxedExpression.Body.ToString();
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Box otherBox) return false;

            if (IsTrue) return otherBox.IsTrue;
            if (IsFalse) return otherBox.IsFalse;
            
            // sadly expressions don't implement Equals so all we get here is ref equality
            return BoxedExpression.Equals(otherBox.BoxedExpression);
        }

        public override int GetHashCode()
        {
            return 0; // TODO: make an actually useful hashcode
        }

        public override bool Equals(QueryExpression? other)
        {
            return Equals((object?)other);
        }

        public static Box True()
        {
            return new Box
            {
                BoxedExpression = Expression.Lambda(Expression.Constant(true))
            };
        }

        public static Box False()
        {
            return new Box
            {
                BoxedExpression = Expression.Lambda(Expression.Constant(false))
            };
        }
    }

    public abstract bool Equals(QueryExpression? other);
}

public class QueryExpressionModelField : IEquatable<QueryExpressionModelField>
{
    public required PropertyInfo PropertyInfo { get; init; }

    public static QueryExpressionModelField? FromExpression(MemberExpression expression, ParameterExpression modelParam)
    {
        if (expression.Expression != modelParam) return null;
        if (expression.Member is not PropertyInfo member) return null;
        return new QueryExpressionModelField
        {
            PropertyInfo = member,
        };
    }

    public override string ToString()
    {
        return PropertyInfo.Name;
    }

    public bool Equals(QueryExpressionModelField? other)
    {
        return Equals((object?)other);
    }


    public override bool Equals(object? obj)
    {
        if (obj is not QueryExpressionModelField otherField) return false;
        return PropertyInfo.Equals(otherField.PropertyInfo);
    }

    public override int GetHashCode()
    {
        return PropertyInfo.GetHashCode();
    }
}