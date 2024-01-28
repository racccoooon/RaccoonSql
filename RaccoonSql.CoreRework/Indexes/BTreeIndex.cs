using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using RaccoonSql.CoreRework.Querying;

namespace RaccoonSql.CoreRework.Indexes;

public class BTreeIndex(PropertyInfo propertyInfo) : IndexBase(propertyInfo)
{
    public override bool SupportsOrderedScan => true;

    public override bool TryConvertExpression(Expression expression, ParameterExpression modelParam, [NotNullWhen(true)] out IndexQueryExpression? result)
    {
        if (expression is not BinaryExpression binaryExpression)
        {
            result = null;
            return false;
        }
        ConstantExpression? value = null;
        QueryExpressionModelField? field = null;
        var mirrored = false;

        switch (binaryExpression.Left, binaryExpression.Right)
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
        if (field is null || value is null || !field.PropertyInfo.Equals(PropertyInfo))
        {
            result = null;
            return false;
        }

        if (value.Value == null)
        {
            switch (binaryExpression.NodeType)
            {
                case ExpressionType.Equal:
                    result = new BTreeIndexEquality()
                    {
                        Field = field,
                        Inverted = false,
                        Value = null,
                    };
                    return true;
                case ExpressionType.NotEqual:
                    result = new BTreeIndexEquality()
                    {
                        Field = field,
                        Inverted = true,
                        Value = null,
                    };
                    return true;
            }
        }

        switch (binaryExpression.NodeType)
        {
            case ExpressionType.Equal:
                result = new BTreeIndexEquality()
                {
                    Value = (IComparable)value.Value!,
                    Inverted = false,
                    Field = field,
                };
                return true;
            case ExpressionType.NotEqual:
                result = new BTreeIndexEquality()
                {
                    Value = (IComparable)value.Value!,
                    Inverted = true,
                    Field = field,
                };
                return true;
            case ExpressionType.GreaterThan when !mirrored:
            case ExpressionType.LessThan when mirrored:
                result = new BTreeIndexRange()
                {
                    From = (IComparable)value.Value!,
                    To = null,
                    FromInclusive = false,
                    ToInclusive = false,
                    Field = field
                };
                return true;
            case ExpressionType.GreaterThanOrEqual when !mirrored:
            case ExpressionType.LessThanOrEqual when mirrored:
                result = new BTreeIndexRange
                {
                    From = (IComparable)value.Value!,
                    To = null,
                    FromInclusive = true,
                    ToInclusive = false,
                    Field = field
                };
                return true;
            case ExpressionType.LessThan when !mirrored:
            case ExpressionType.GreaterThan when mirrored:
                result = new BTreeIndexRange
                {
                    From = null,
                    To = (IComparable)value.Value!,
                    FromInclusive = false,
                    ToInclusive = false,
                    Field = field
                };
                return true;
            case ExpressionType.LessThanOrEqual when !mirrored:
            case ExpressionType.GreaterThanOrEqual when mirrored:
                result = new BTreeIndexRange
                {
                    From = null,
                    To = (IComparable)value.Value!,
                    FromInclusive = false,
                    ToInclusive = true,
                    Field = field
                };
                return true;
        }

        result = null;
        return false;
    }

    public override IEnumerable<ModelBase> Scan(List<IndexQueryExpression> queries, IndexScanOrder? order)
    {
        throw new NotImplementedException();
    }
}

public class BTreeIndexEquality : IndexQueryExpression
{

    public required bool Inverted { get; init; }
    public required IComparable? Value { get; init; }

    public override bool Equals(QueryExpression? other)
    {
        if (other is not BTreeIndexEquality otherEquality) return false;
        if (!Field.Equals(otherEquality.Field)) return false;
        if (Inverted != otherEquality.Inverted) return false;
        if (Value is null || otherEquality.Value is null) return Value is null && otherEquality.Value is null;
        return Value.CompareTo(otherEquality.Value) == 0;
    }

    public override bool IsTriviallyTrue => false;
    public override bool IsTriviallyFalse => false;

    private static bool TryUnion(BTreeIndexEquality a, BTreeIndexEquality b, [NotNullWhen(true)] out QueryExpression? result)
    {
        switch (a.Value, b.Value)
        {
            case (null, null) when a.Inverted == b.Inverted:
                result = a;
                return true;
            case (null, null):
                result = Box.True();
                return true;
            case (null, _) when a.Inverted:
                result = b;
                return true;
            case (_, null) when b.Inverted:
                result = a;
                return true;
            case (_, null):
            case (null, _):
                result = null;
                return false;
        }

        var cmp = a.Value.CompareTo(b.Value);

        switch (cmp, a.Inverted, b.Inverted)
        {
            case (0, true, true):
            case (0, false, false):
                result = a;
                return true;
            case (0, _, _):
                result = Box.True();
                return true;
            case (_, false, false):
                result = null;
                return false;
            case (_, false, true):
                result = b;
                return true;
            case (_, true, false):
                result = a;
                return true;
            case (_, true, true):
                result = Box.True();
                return true;
        }
    }

    public override bool TryUnion(IndexQueryExpression other, [NotNullWhen(true)] out QueryExpression? result)
    {
        switch (other)
        {
            case BTreeIndexRange otherRange:
                return otherRange.TryUnion(this, out result);
            case BTreeIndexEquality otherEquality when TryUnion(this, otherEquality, out var res):
                result = res.Normalize();
                return true;
            default:
                result = null;
                return false;
        }

    }

    private static bool TryIntersect(BTreeIndexEquality a, BTreeIndexEquality b, [NotNullWhen(true)] out QueryExpression? result)
    {
        switch (a.Value, b.Value)
        {
            case (null, null):
                if (a.Inverted == b.Inverted)
                {
                    result = a;
                    return true;
                }
                result = null;
                return false;

            case (null, _):
                switch (a.Inverted, b.Inverted)
                {
                    case (false, false):
                        result = null;
                        return false;
                    case (false, true):
                        result = a;
                        return true;
                    case (true, _):
                        result = b;
                        return true;
                }
            case (_, null):
                switch (a.Inverted, b.Inverted)
                {
                    case (false, false):
                        result = null;
                        return false;
                    case (true, false):
                        result = b;
                        return true;
                    case (_, true):
                        result = a;
                        return true;
                }
            case var (aVal, bVal):
                var cmp = aVal.CompareTo(bVal);

                switch (cmp, a.Inverted, b.Inverted)
                {
                    case (0, true, false):
                    case (0, false, true):
                        result = null;
                        return false;
                    case (0, true, true):
                    case (0, false, false):
                        result = a;
                        return true;

                    case (_, false, false):
                        result = null;
                        return false;
                    case (_, false, true):
                        result = a;
                        return true;
                    case (_, true, false):
                        result = b;
                        return true;
                    case (< 0, true, true):
                        result = new Or
                        {
                            Terms =
                            [
                                new BTreeIndexRange
                                {
                                    Field = a.Field,
                                    From = null,
                                    FromInclusive = false,
                                    To = a.Value,
                                    ToInclusive = false,
                                },
                                new BTreeIndexRange
                                {
                                    Field = a.Field,
                                    From = a.Value,
                                    FromInclusive = false,
                                    To = b.Value,
                                    ToInclusive = false,
                                },
                                new BTreeIndexRange
                                {
                                    Field = a.Field,
                                    From = b.Value,
                                    FromInclusive = false,
                                    To = null,
                                    ToInclusive = false,
                                },
                            ],
                        };
                        return true;
                    case (> 0, true, true):
                        result = new Or
                        {
                            Terms =
                            [
                                new BTreeIndexRange
                                {
                                    Field = a.Field,
                                    From = null,
                                    FromInclusive = false,
                                    To = b.Value,
                                    ToInclusive = false,
                                },
                                new BTreeIndexRange
                                {
                                    Field = a.Field,
                                    From = b.Value,
                                    FromInclusive = false,
                                    To = a.Value,
                                    ToInclusive = false,
                                },
                                new BTreeIndexRange
                                {
                                    Field = a.Field,
                                    From = a.Value,
                                    FromInclusive = false,
                                    To = null,
                                    ToInclusive = false,
                                },
                            ],
                        };
                        return true;
                }

                break;
        }
    }

    public override bool TryIntersect(IndexQueryExpression other, [NotNullWhen(true)] out QueryExpression? result)
    {
        var (success, res) = other switch
        {
            BTreeIndexEquality otherEquality => (TryIntersect(this, otherEquality, out var r), r),
            BTreeIndexRange otherRange => (otherRange.TryIntersect(this, out var r), r),
            _ => throw new InvalidOperationException(),
        };
        result = res?.Normalize();
        return success;
    }

    public override bool TryInvert([NotNullWhen(true)] out QueryExpression? result)
    {
        result = new BTreeIndexEquality
        {
            Field = Field,
            Inverted = !Inverted,
            Value = Value,
        };
        return true;
    }

    public override Expression ToExpression(ParameterExpression propertyParameter)
    {
        return Inverted
            ? Expression.NotEqual(propertyParameter, Expression.Constant(Value))
            : Expression.Equal(propertyParameter, Expression.Constant(Value));
    }
}

public class BTreeIndexRange : IndexQueryExpression, IComparable<BTreeIndexRange>
{
    public override bool IsTriviallyTrue => From is null && To is null;
    public override bool IsTriviallyFalse => false;

    public required IComparable? From { get; init; }
    public required IComparable? To { get; init; }

    public required bool FromInclusive { get; init; }
    public required bool ToInclusive { get; init; }


    public override bool TryInvert([NotNullWhen(true)] out QueryExpression? result)
    {
        result = (From, To) switch
        {
            (null, null) => null,
            (_, null) => new BTreeIndexRange
            {
                From = null,
                To = From,
                FromInclusive = false,
                ToInclusive = !FromInclusive,
                Field = Field,
            },
            (null, _) => new BTreeIndexRange
            {
                From = To,
                To = null,
                FromInclusive = !ToInclusive,
                ToInclusive = false,
                Field = Field,
            },
            (_, _) => new Or
            {
                Terms =
                [
                    new BTreeIndexRange
                    {
                        From = null,
                        To = From,
                        FromInclusive = false,
                        ToInclusive = !FromInclusive,
                        Field = Field,
                    },
                    new BTreeIndexRange
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
        return result is not null;
    }

    private static void OrderSwap(ref BTreeIndexRange r1, ref BTreeIndexRange r2)
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

    public override bool TryIntersect(IndexQueryExpression? other, [NotNullWhen(true)] out QueryExpression? result)
    {
        if (other is BTreeIndexEquality otherEquality)
        {
            if (TryIntersect(this, otherEquality, out var res))
            {
                result = res.Normalize();
                return true;
            }
            result = null;
            return false;
        }

        if (other is BTreeIndexRange otherRange)
        {
            if (TryIntersect(this, otherRange, out var res))
            {
                result = res.Normalize();
                return true;
            }
            result = null;
            return false;
        }
        throw new InvalidOperationException();
    }

    public override Expression ToExpression(ParameterExpression propertyParameter)
    {
        var fromExpr = (From, FromInclusive) switch
        {
            (null, _) => null,
            (_, true) => Expression.GreaterThanOrEqual(propertyParameter, Expression.Constant(From)),
            (_, false) => Expression.GreaterThan(propertyParameter, Expression.Constant(From)),
        };
        var toExpr = (To, ToInclusive) switch
        {
            (null, _) => null,
            (_, true) => Expression.LessThanOrEqual(propertyParameter, Expression.Constant(To)),
            (_, false) => Expression.LessThan(propertyParameter, Expression.Constant(To)),
        };
        return (fromExpr, toExpr) switch
        {
            (null, null) => Expression.Constant(true),
            (var from, null) => from,
            (null, var to) => to,
            var (from, to) => Expression.AndAlso(from, to),
        };
    }

    private static bool TryIntersect(BTreeIndexRange range, BTreeIndexEquality equality, [NotNullWhen(true)] out QueryExpression? result)
    {
        if (equality.Value is null)
        {
            if (equality.Inverted)
            {
                result = range;
                return true;
            }
            result = null;
            return false;
        }

        var cmpFrom = CompareEndpoints(range.From, range.FromInclusive, false, equality.Value, true, false);
        var cmpTo = CompareEndpoints(range.To, range.ToInclusive, true, equality.Value, true, true);

        if (!equality.Inverted)
        {
            switch (cmpFrom, cmpTo)
            {
                case (0, > 0) when range.FromInclusive: // from = value < to
                case (< 0, 0) when range.ToInclusive: // from < value = to
                case (< 0, > 0): // from < value < to
                    result = equality;
                    return true;
                default:
                    result = null;
                    return false;
            }
        }
        switch (cmpFrom, cmpTo)
        {
            case (0, > 0) when range.FromInclusive: // from = value < to
                result = new BTreeIndexRange
                {
                    Field = range.Field,
                    From = range.From,
                    FromInclusive = false,
                    To = range.To,
                    ToInclusive = range.ToInclusive,
                };
                return true;
            case (< 0, 0) when range.ToInclusive: // from < value = to
                result = new BTreeIndexRange
                {
                    Field = range.Field,
                    From = range.From,
                    FromInclusive = range.FromInclusive,
                    To = range.To,
                    ToInclusive = false,
                };
                return true;
            case (< 0, > 0): // from < value < to
                result = new Or
                {
                    Terms =
                    [
                        new BTreeIndexRange
                        {
                            Field = range.Field,
                            From = range.From,
                            FromInclusive = range.FromInclusive,
                            To = equality.Value,
                            ToInclusive = false,
                        },
                        new BTreeIndexRange
                        {
                            Field = range.Field,
                            From = equality.Value,
                            FromInclusive = false,
                            To = range.To,
                            ToInclusive = range.ToInclusive,
                        },
                    ],
                };
                return true;
            default:
                result = range;
                return true;
        }
    }

    private static bool TryIntersect(BTreeIndexRange r1, BTreeIndexRange r2, [NotNullWhen(true)] out QueryExpression? result)
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
                result = new BTreeIndexRange
                {
                    Field = field,
                    From = r2.From,
                    To = r2.To,
                    FromInclusive = r2.FromInclusive,
                    ToInclusive = r2.ToInclusive,
                };
                return true;
            default:
                result = new BTreeIndexRange
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


    public override bool TryUnion(IndexQueryExpression other, [NotNullWhen(true)] out QueryExpression? result)
    {
        if (other is BTreeIndexRange otherRange)
        {
            if (TryUnion(this, otherRange, out var res))
            {
                result = res.Normalize();
                return true;
            }
            result = null;
            return false;
        }
        if (other is BTreeIndexEquality otherEquality)
        {
            if (TryUnion(this, otherEquality, out var res))
            {
                result = res.Normalize();
                return true;
            }
            result = null;
            return false;
        }
        throw new InvalidOperationException();
    }

    private static bool TryUnion(BTreeIndexRange range, BTreeIndexEquality equality, [NotNullWhen(true)] out QueryExpression? result)
    {
        var cmpFrom = CompareEndpoints(range.From, range.FromInclusive, false, equality.Value, true, false);
        var cmpTo = CompareEndpoints(range.To, range.ToInclusive, true, equality.Value, true, true);
        // TODO: CompareEndpoints takes into account that to/from are exclusive so it is -1 even when the values are equal
        switch (cmpFrom, cmpTo)
        {
            case (0, > 0) when !range.FromInclusive: // from = value < to
                result = new BTreeIndexRange
                {
                    Field = range.Field,
                    From = range.From,
                    FromInclusive = true,
                    To = range.To,
                    ToInclusive = range.ToInclusive,
                };
                return true;
            case (<0,0) when !range.ToInclusive:
                result = new BTreeIndexRange
                {
                    Field = range.Field,
                    From = range.From,
                    FromInclusive = range.FromInclusive,
                    To = range.To,
                    ToInclusive = true,
                };
                return true;
            case (0, > 0) when range.FromInclusive: // from = value < to
            case (< 0, 0) when range.ToInclusive: // from < value = to
            case (< 0, > 0): // from < value < to
                if (!equality.Inverted)
                {
                    result = range;
                    return true;
                }
                result = Box.True();
                return true;
            default:
                if (!equality.Inverted)
                {
                    result = null;
                    return false;
                }
                result = equality;
                return true;
        }
    }

    private static bool TryUnion(BTreeIndexRange r1, BTreeIndexRange r2, [NotNullWhen(true)] out QueryExpression? result)
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
                result = new BTreeIndexRange
                {
                    Field = field,
                    From = r1.From,
                    To = r1.To,
                    FromInclusive = r1.FromInclusive,
                    ToInclusive = r1.ToInclusive,
                };
                return true;
            default:
                result = new BTreeIndexRange
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
        if (obj is not BTreeIndexRange otherRange) return false;
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

    public int CompareTo(BTreeIndexRange? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (other is null) return 1;
        var cmp = CompareEndpoints(From, FromInclusive, false, other.From, other.FromInclusive, false);
        if (cmp != 0) return cmp;
        return CompareEndpoints(To, ToInclusive, true, other.To, other.ToInclusive, true);
    }
}