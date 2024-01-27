using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using RaccoonSql.CoreRework.Internal.Utils;

namespace RaccoonSql.CoreRework.Internal.Querying;

public abstract class QueryExpression : IEquatable<QueryExpression>
{
    public abstract class Visitor
    {
        public virtual QueryExpression Visit(QueryExpression expression) =>
            expression switch
            {
                And and => VisitAnd(and),
                Or or => VisitOr(or),
                Not not => VisitNot(not),
                Range range => VisitRange(range),
                Box box => VisitBox(box),
                IsNull isNull => VisitIsNull(isNull),
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

        protected virtual QueryExpression VisitRange(Range range)
        {
            return range;
        }

        protected virtual QueryExpression VisitBox(Box box)
        {
            return box;
        }

        protected virtual QueryExpression VisitIsNull(IsNull isNull)
        {
            return isNull;
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
                Not innerNot => base.Visit(innerNot.Term),
                Or or => base.Visit(new And
                {
                    Terms = or.Terms.Select(x => new Not { Term = x, }).ToList(),
                }),
                And and => base.Visit(new Or
                {
                    Terms = and.Terms.Select(x => new Not { Term = x, }).ToList(),
                }),
                Box { BoxedExpression: LambdaExpression { Body: ConstantExpression constantExpression } lambda }
                    when constantExpression.Type == typeof(bool)
                    => new Box
                    {
                        BoxedExpression = Expression.Lambda(Expression.Constant(!(bool)constantExpression.Value!),
                            lambda.Parameters),
                    },
                Range range => range.Invert(),
                _ => not
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
                return base.Visit(new Or
                {
                    Terms = newTerms
                });


            newTerms = new List<QueryExpression>(or.Terms.Count);

            foreach (var term in or.Terms)
            {
                switch (term)
                {
                    case Not notTerm when or.Terms.Any(term2 => term != term2 && notTerm.Term.Equals(term2)):
                        return new Box { BoxedExpression = Expression.Lambda(Expression.Constant(true)) };
                    case Box { BoxedExpression.Body: ConstantExpression constantExpression } box
                        when constantExpression.Type == typeof(bool):
                    {
                        var value = (bool)constantExpression.Value!;
                        if (value)
                        {
                            return box;
                        }

                        continue;
                    }
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
                return new Box { BoxedExpression = Expression.Lambda(Expression.Constant(false)) };
            }

            newTerms = newTerms.Distinct().ToList();

            if (newTerms.Count != or.Terms.Count)
            {
                return base.Visit(new Or { Terms = newTerms });
            }

            var terms = or.Terms.ToList();
            newTerms = new List<QueryExpression>(or.Terms.Count);
            while (terms.Count != 0)
            {
                var term1 = terms[0];
                terms.RemoveAt(0);

                if (term1 is Range range1)
                {
                    List<QueryExpression> removed = [];
                    foreach (var term2 in terms)
                    {
                        if (term2 is not Range range2) continue;
                        if (term1 == term2) continue;
                        if (!range1.Field.Equals(range2.Field)) continue;

                        if (Range.TryUnion(range1, range2, out var union))
                        {
                            removed.Add(range2);
                            range1 = union;
                        }
                    }
                    terms = terms.Except(removed).ToList();
                    newTerms.Add(range1);
                }
                else
                {
                    newTerms.Add(term1);
                }
            }

            if (newTerms.Count != or.Terms.Count)
            {
                return base.Visit(new Or { Terms = newTerms });
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
                return base.Visit(new And
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
                return base.Visit(new Or
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
                        return new Box { BoxedExpression = Expression.Lambda(Expression.Constant(false)) };
                    case Box { BoxedExpression.Body: ConstantExpression constantExpression } box
                        when constantExpression.Type == typeof(bool):
                    {
                        var value = (bool)constantExpression.Value!;
                        if (value == false)
                        {
                            return box;
                        }

                        continue;
                    }
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
                return new Box { BoxedExpression = Expression.Lambda(Expression.Constant(true)) };
            }

            newTerms = newTerms.Distinct().ToList();

            if (newTerms.Count != and.Terms.Count)
            {
                return base.Visit(new And { Terms = newTerms });
            }

            var terms = and.Terms.ToList();
            newTerms = new List<QueryExpression>(and.Terms.Count);
            while (terms.Count != 0)
            {
                var term1 = terms[0];
                terms.RemoveAt(0);

                if (term1 is Range range1)
                {
                    List<QueryExpression> removed = [];
                    foreach (var term2 in terms)
                    {
                        if (term2 is not Range range2) continue;
                        if (term1 == term2) continue;
                        if (!range1.Field.Equals(range2.Field)) continue;

                        if (Range.TryIntersect(range1, range2, out var intersection))
                        {
                            range1 = intersection;
                            removed.Add(range2);
                        }
                        else
                        {
                            return Box.False();
                        }
                    }

                    if (removed.Count != 0)
                    {
                        terms = terms.Except(removed).ToList();
                    }
                    newTerms.Add(range1);
                }
                else
                {
                    newTerms.Add(term1);
                }
            }

            if (newTerms.Count != and.Terms.Count)
            {
                return base.Visit(new And { Terms = newTerms });
            }

            return and;
        }

        protected override QueryExpression VisitRange(Range range)
        {
            if (range.From is null && range.To is null)
            {
                return Box.True();
            }

            return base.VisitRange(range);
        }
    }

    public static QueryExpression FromPredicateExpression<TModel>(Expression<Func<TModel, bool>> expression)
    {
        return ConvertExpression(ExpressionUtils.ExecutePartially(expression.Body), expression.Parameters[0])
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
                ConstantExpression? value = null;
                QueryExpressionModelField? field = null;
                var mirrored = false;

                switch (e.Left, e.Right)
                {
                    case (ConstantExpression l, MemberExpression r):
                        value = l;
                        field = QueryExpressionModelField.FromExpression(r, modelParam);
                        mirrored = true;
                        break;
                    case (MemberExpression l, ConstantExpression r):
                        value = r;
                        field = QueryExpressionModelField.FromExpression(l, modelParam);
                        break;
                }

                if (field is null || value is null) break;

                if (value.Value == null)
                {
                    switch (e.NodeType)
                    {
                        case ExpressionType.Equal:
                            return new IsNull { Field = field };
                        case ExpressionType.NotEqual:
                            return new Not { Term = new IsNull { Field = field } };
                    }
                }

                if (!field.PropertyInfo.PropertyType.IsAssignableTo(typeof(IComparable))) break;

                switch (e.NodeType)
                {
                    case ExpressionType.Equal:
                        return new Range
                        {
                            From = (IComparable)value.Value!,
                            To = (IComparable)value.Value!,
                            FromInclusive = true,
                            ToInclusive = true,
                            Field = field
                        };
                    case ExpressionType.NotEqual:
                        return new Or
                        {
                            Terms =
                            [
                                new Range
                                {
                                    From = null,
                                    To = (IComparable)value.Value!,
                                    FromInclusive = false,
                                    ToInclusive = false,
                                    Field = field
                                },
                                new Range
                                {
                                    From = (IComparable)value.Value!,
                                    To = null,
                                    FromInclusive = false,
                                    ToInclusive = false,
                                    Field = field
                                }
                            ]
                        };
                    case ExpressionType.GreaterThan when !mirrored:
                    case ExpressionType.LessThan when mirrored:
                        return new Range
                        {
                            From = (IComparable)value.Value!,
                            To = null,
                            FromInclusive = false,
                            ToInclusive = false,
                            Field = field
                        };
                    case ExpressionType.GreaterThanOrEqual when !mirrored:
                    case ExpressionType.LessThanOrEqual when mirrored:
                        return new Range
                        {
                            From = (IComparable)value.Value!,
                            To = null,
                            FromInclusive = true,
                            ToInclusive = false,
                            Field = field
                        };
                    case ExpressionType.LessThan when !mirrored:
                    case ExpressionType.GreaterThan when mirrored:
                        return new Range
                        {
                            From = null,
                            To = (IComparable)value.Value!,
                            FromInclusive = false,
                            ToInclusive = false,
                            Field = field
                        };
                    case ExpressionType.LessThanOrEqual when !mirrored:
                    case ExpressionType.GreaterThanOrEqual when mirrored:
                        return new Range
                        {
                            From = null,
                            To = (IComparable)value.Value!,
                            FromInclusive = false,
                            ToInclusive = true,
                            Field = field
                        };
                }

                break;

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
            return "(" + string.Join(" || ", Terms) + ")";
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

    public class IsNull : QueryExpression
    {
        public required QueryExpressionModelField Field { get; init; }

        public override bool Equals(object? obj)
        {
            if (obj is not IsNull otherIsNull) return false;
            return Field.Equals(otherIsNull.Field);
        }

        public override int GetHashCode()
        {
            return 0; // TODO: make an actually useful hashcode
        }

        public override bool Equals(QueryExpression? other)
        {
            return Equals((object?)other);
        }

        public override string ToString()
        {
            return $"{Field.PropertyInfo.Name} is null";
        }
    }

    public abstract class FieldExpression : QueryExpression
    {
        public required QueryExpressionModelField Field { get; init; }
    }

    public class Range : FieldExpression, IComparable<Range>
    {
        public required IComparable? From { get; init; }
        public required IComparable? To { get; init; }

        public required bool FromInclusive { get; init; }
        public required bool ToInclusive { get; init; }

        public QueryExpression Invert()
        {
            return (From, To) switch
            {
                (null, null) => throw new UnreachableException(),
                (_, null) => new Range
                {
                    From = null,
                    To = From,
                    FromInclusive = false,
                    ToInclusive = !FromInclusive,
                    Field = Field,
                },
                (null, _) => new Range
                {
                    From = To,
                    To = null,
                    FromInclusive = !ToInclusive,
                    ToInclusive = false,
                    Field = Field,
                },
                _ => new Or
                {
                    Terms =
                    [
                        new Range
                        {
                            From = null,
                            To = From,
                            FromInclusive = false,
                            ToInclusive = !FromInclusive,
                            Field = Field,
                        },
                        new Range
                        {
                            From = To,
                            To = null,
                            FromInclusive = !ToInclusive,
                            ToInclusive = false,
                            Field = Field,
                        }
                    ]
                }
            };
        }

        private static void OrderSwap(ref Range r1, ref Range r2)
        {
            if (r1.CompareTo(r2) > 0)
            {
                (r1, r2) = (r2, r1);
            }
        }

        private static int CompareEndpoints(IComparable? p1, bool p1Inclusive, bool p1IsTo, IComparable? p2,
            bool p2Inclusive, bool p2IsTo)
        {
            switch (p1, p2)
            {
                case (null, null): return p1IsTo.CompareTo(p2IsTo);
                case (null, _): return p1IsTo ? 1 : -1;
                case (_, null): return p2IsTo ? -1 : 1;
            }

            var cmp = p1.CompareTo(p2);
            if (cmp != 0) return cmp;

            return (p1IsTo, p2IsTo) switch
            {
                (true, false) => p1Inclusive && p2Inclusive ? 0 : -1,
                (false, true) => p1Inclusive && p2Inclusive ? 0 : 1,
                (true, true) => p1Inclusive.CompareTo(p2Inclusive),
                (false, false) => -p1Inclusive.CompareTo(p2Inclusive),
            };
        }

        public static bool TryIntersect(Range r1, Range r2, [NotNullWhen(true)] out Range? result)
        {
            if (!r1.Field.Equals(r2.Field))
            {
                throw new InvalidOperationException();
            }

            var field = r1.Field;
            OrderSwap(ref r1, ref r2);

            var cmp1 = CompareEndpoints(r1.To, r1.ToInclusive, true, r2.From, r2.FromInclusive, false);
            var cmp2 = CompareEndpoints(r1.To, r1.ToInclusive, true, r2.To, r2.ToInclusive, true);

            switch (cmp: cmp1, cmp2)
            {
                case (< 0, _):
                {
                    result = null;
                    return false;
                }
                case (> 0, > 0):
                    result = new Range
                    {
                        Field = field,
                        From = r2.From,
                        To = r2.To,
                        FromInclusive = r2.FromInclusive,
                        ToInclusive = r2.ToInclusive,
                    };
                    return true;
                default:
                    result = new Range
                    {
                        Field = field,
                        From = r2.From,
                        To = r1.To,
                        FromInclusive = r2.FromInclusive,
                        ToInclusive = r1.ToInclusive,
                    };
                    return true;
            }
        }

        public static bool TryUnion(Range r1, Range r2, [NotNullWhen(true)] out Range? result)
        {
            if (!r1.Field.Equals(r2.Field))
            {
                throw new InvalidOperationException();
            }

            var field = r1.Field;
            OrderSwap(ref r1, ref r2);

            var cmp1 = CompareEndpoints(r1.To, r1.ToInclusive, true, r2.From, r2.FromInclusive, false);
            var cmp2 = CompareEndpoints(r1.To, r1.ToInclusive, true, r2.To, r2.ToInclusive, true);

            switch (cmp: cmp1, cmp2)
            {
                case (< 0, _):
                {
                    result = null;
                    return false;
                }
                case (_, > 0):
                    result = new Range
                    {
                        Field = field,
                        From = r1.From,
                        To = r1.To,
                        FromInclusive = r1.FromInclusive,
                        ToInclusive = r1.ToInclusive,
                    };
                    return true;
                default:
                    result = new Range
                    {
                        Field = field,
                        From = r1.From,
                        To = r2.To,
                        FromInclusive = r1.FromInclusive,
                        ToInclusive = r2.ToInclusive,
                    };
                    return true;
            }
        }


        public override string ToString()
        {
            StringBuilder builder = new();
            builder.Append('[');
            if (From is not null)
            {
                builder.Append(From);
                builder.Append(FromInclusive ? " <= " : " < ");
            }

            builder.Append(Field);
            if (To is not null)
            {
                builder.Append(ToInclusive ? " <= " : " < ");
                builder.Append(To);
            }

            builder.Append(']');
            return builder.ToString();
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Range otherRange) return false;
            return Field.Equals(otherRange.Field) && Equals(From, otherRange.From) &&
                   Equals(To, otherRange.To) && FromInclusive == otherRange.FromInclusive &&
                   ToInclusive == otherRange.ToInclusive;
        }

        public override int GetHashCode()
        {
            return 0; // TODO: make an actually useful hashcode
        }

        public override bool Equals(QueryExpression? other)
        {
            return Equals((object?)other);
        }

        public int CompareTo(Range? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (other is null) return 1;
            var cmp = CompareEndpoints(From, FromInclusive, false, other.From, other.FromInclusive, false);
            if (cmp != 0) return cmp;
            return CompareEndpoints(To, ToInclusive, true, other.To, other.ToInclusive, true);
        }
    }

    public class Box : QueryExpression
    {
        public required LambdaExpression BoxedExpression { get; init; }

        public override string ToString()
        {
            return BoxedExpression.Body.ToString();
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Box otherBox) return false;
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
            return new Box { BoxedExpression = Expression.Lambda(Expression.Constant(true)) };
        }

        public static Box False()
        {
            return new Box { BoxedExpression = Expression.Lambda(Expression.Constant(false)) };
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
            PropertyInfo = member
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