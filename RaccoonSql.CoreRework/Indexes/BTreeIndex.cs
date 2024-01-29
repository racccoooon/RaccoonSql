using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using RaccoonSql.CoreRework.Querying;

namespace RaccoonSql.CoreRework.Indexes;

public class BTreeIndex(PropertyInfo propertyInfo) : IndexBase(propertyInfo)
{
    public override bool SupportsOrderedScan => true;

    private bool Nullable { get; }
    
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
                    result = new BTreeIndexEquality(Nullable)
                    {
                        Field = field,
                        Inverted = false,
                        Value = null,
                    };
                    return true;
                case ExpressionType.NotEqual:
                    result = new BTreeIndexEquality(Nullable)
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
                result = new BTreeIndexEquality(Nullable)
                {
                    Value = (IComparable)value.Value!,
                    Inverted = false,
                    Field = field,
                };
                return true;
            case ExpressionType.NotEqual:
                result = new BTreeIndexEquality(Nullable)
                {
                    Value = (IComparable)value.Value!,
                    Inverted = true,
                    Field = field,
                };
                return true;
            case ExpressionType.GreaterThan when !mirrored:
            case ExpressionType.LessThan when mirrored:
                result = new BTreeIndexRange(Nullable)
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
                result = new BTreeIndexRange(Nullable)
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
                result = new BTreeIndexRange(Nullable)
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
                result = new BTreeIndexRange(Nullable)
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

public class BTreeIndexEquality(bool nullable) : IndexQueryExpression
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

    public override bool TryUnion(IndexQueryExpression other, [NotNullWhen(true)] out QueryExpression? result)
    {
        if (other is BTreeIndexRange otherRange)
        {
            return otherRange.TryUnion(this, out result);
        }

        if (other is not BTreeIndexEquality otherEquality) throw new ArgumentException(null, nameof(other));

        switch (Value, otherEquality.Value)
        {
            case (null, null) when Inverted == otherEquality.Inverted:
                result = this;
                return true;
            case (null, null):
                result = Box.True();
                return true;
            case (null, not null) when Inverted:
                result = this;
                return true;
            case (not null, null) when otherEquality.Inverted:
                result = otherEquality;
                return true;
            case (not null, null):
            case (null, not null):
                result = null;
                return false;
        }

        var cmp = Value.CompareTo(otherEquality.Value);

        switch (cmp, Inverted, otherEquality.Inverted)
        {
            case (0, true, true):
            case (0, false, false):
            case (not 0, true, false):
                result = this;
                return true;
            case (not 0, false, true):
                result = otherEquality;
                return true;
            case (0, _, _):
            case (not 0, true, true):
                result = new BTreeIndexRange(nullable)
                {
                    Field = Field,
                    From = null,
                    To = null,
                    FromInclusive = false,
                    ToInclusive = false,
                };
                return true;
            case (not 0, false, false):
                result = null;
                return false;
        }

    }

    public override bool TryIntersect(IndexQueryExpression other, [NotNullWhen(true)] out QueryExpression? result)
    {
        if (other is BTreeIndexRange otherRange)
        {
            return otherRange.TryIntersect(this, out result);
        }

        if (other is not BTreeIndexEquality otherEquality) throw new ArgumentException(null, nameof(other));
        switch (Value, otherEquality.Value)
        {
            case (null, null):
                if (Inverted == otherEquality.Inverted)
                {
                    result = this;
                    return true;
                }
                result = null;
                return false;
            case (null, not null):
                switch (Inverted, otherEquality.Inverted)
                {
                    case (false, _):
                        result = null;
                        return false;
                    case (true, _):
                        result = otherEquality;
                        return true;
                }
            case (not null, null):
                switch (Inverted, otherEquality.Inverted)
                {
                    case (_, false):
                        result = null;
                        return false;
                    case (_, true):
                        result = this;
                        return true;
                }
            case var (aVal, bVal):
                var cmp = aVal.CompareTo(bVal);
                switch (cmp, Inverted, otherEquality.Inverted)
                {
                    case (0, true, false):
                    case (0, false, true):
                        result = null;
                        return false;
                    case (0, true, true):
                    case (0, false, false):
                        result = this;
                        return true;

                    case (_, false, false):
                        result = null;
                        return false;
                    case (_, false, true):
                        result = this;
                        return true;
                    case (_, true, false):
                        result = otherEquality;
                        return true;
                    case (< 0, true, true):
                        result = new Or
                        {
                            Terms =
                            [
                                new BTreeIndexRange(nullable)
                                {
                                    Field = Field,
                                    From = null,
                                    FromInclusive = false,
                                    To = Value,
                                    ToInclusive = false,
                                },
                                new BTreeIndexRange(nullable)
                                {
                                    Field = Field,
                                    From = Value,
                                    FromInclusive = false,
                                    To = otherEquality.Value,
                                    ToInclusive = false,
                                },
                                new BTreeIndexRange(nullable)
                                {
                                    Field = Field,
                                    From = otherEquality.Value,
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
                                new BTreeIndexRange(nullable)
                                {
                                    Field = Field,
                                    From = null,
                                    FromInclusive = false,
                                    To = otherEquality.Value,
                                    ToInclusive = false,
                                },
                                new BTreeIndexRange(nullable)
                                {
                                    Field = Field,
                                    From = otherEquality.Value,
                                    FromInclusive = false,
                                    To = Value,
                                    ToInclusive = false,
                                },
                                new BTreeIndexRange(nullable)
                                {
                                    Field = Field,
                                    From = Value,
                                    FromInclusive = false,
                                    To = null,
                                    ToInclusive = false,
                                },
                            ],
                        };
                        return true;
                }
        }
    }

    public override bool TryInvert([NotNullWhen(true)] out QueryExpression? result)
    {
        result = new BTreeIndexEquality(nullable)
        {
            Field = Field,
            Inverted = !Inverted,
            Value = Value,
        };
        return true;
    }

    public override bool TrySimplify([NotNullWhen(true)] out QueryExpression? result)
    {
        if (Value is null && !nullable)
        {
            result = Inverted ? Box.True() : Box.False();
            return true;
        }
        result = null;
        return false;
    }

    public override Expression ToExpression(ParameterExpression propertyParameter)
    {
        return Inverted
            ? Expression.NotEqual(propertyParameter, Expression.Constant(Value))
            : Expression.Equal(propertyParameter, Expression.Constant(Value));
    }
}

public class BTreeIndexRange(bool nullable) : IndexQueryExpression, IComparable<BTreeIndexRange>
{

    public required IComparable? From { get; init; }
    public required IComparable? To { get; init; }

    public required bool FromInclusive { get; init; }
    public required bool ToInclusive { get; init; }


    public override bool TryIntersect(IndexQueryExpression? other, [NotNullWhen(true)] out QueryExpression? result)
    {
        return other switch
        {
            BTreeIndexEquality otherEquality => TryIntersect(otherEquality, out result),
            BTreeIndexRange otherRange => TryIntersect(this, otherRange, nullable, out result),
            _ => throw new ArgumentException(null, nameof(other)),
        };

    }

    private bool TryIntersect(BTreeIndexEquality other, [NotNullWhen(true)] out QueryExpression? result)
    {
        if (other.Value is null)
        {
            if (other.Inverted)
            {
                result = this;
                return true;
            }
            result = null;
            return false;
        }

        var cmpFrom = CompareEndpoints(From, FromInclusive, false, other.Value, true, false);
        var cmpTo = CompareEndpoints(To, ToInclusive, true, other.Value, true, true);

        if (!other.Inverted)
        {
            switch (cmpFrom, cmpTo)
            {
                case (0, > 0) when FromInclusive: // from = value < to
                case (< 0, 0) when ToInclusive: // from < value = to
                case (< 0, > 0): // from < value < to
                    result = other;
                    return true;
                default:
                    result = null;
                    return false;
            }
        }
        switch (cmpFrom, cmpTo)
        {
            case (0, > 0) when FromInclusive: // from = value < to
                result = new BTreeIndexRange(nullable)
                {
                    Field = Field,
                    From = From,
                    FromInclusive = false,
                    To = To,
                    ToInclusive = ToInclusive,
                };
                return true;
            case (< 0, 0) when ToInclusive: // from < value = to
                result = new BTreeIndexRange(nullable)
                {
                    Field = Field,
                    From = From,
                    FromInclusive = FromInclusive,
                    To = To,
                    ToInclusive = false,
                };
                return true;
            case (< 0, > 0): // from < value < to
                result = new Or
                {
                    Terms =
                    [
                        new BTreeIndexRange(nullable)
                        {
                            Field = Field,
                            From = From,
                            FromInclusive = FromInclusive,
                            To = other.Value,
                            ToInclusive = false,
                        },
                        new BTreeIndexRange(nullable)
                        {
                            Field = Field,
                            From = other.Value,
                            FromInclusive = false,
                            To = To,
                            ToInclusive = ToInclusive,
                        },
                    ],
                };
                return true;
            default:
                result = this;
                return true;
        }
    }

    private static bool TryIntersect(BTreeIndexRange r1, BTreeIndexRange r2, bool nullable, [NotNullWhen(true)] out QueryExpression? result)
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
                result = new BTreeIndexRange(nullable)
                {
                    Field = field,
                    From = r2.From,
                    To = r2.To,
                    FromInclusive = r2.FromInclusive,
                    ToInclusive = r2.ToInclusive,
                };
                return true;
            default:
                result = new BTreeIndexRange(nullable)
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
            if (TryUnion(this, otherRange, nullable, out var res))
            {
                result = res.Normalize();
                return true;
            }
            result = null;
            return false;
        }
        if (other is BTreeIndexEquality otherEquality)
        {
            if (TryUnion(otherEquality, out var res))
            {
                result = res.Normalize();
                return true;
            }
            result = null;
            return false;
        }
        throw new InvalidOperationException();
    }

    private bool TryUnion(BTreeIndexEquality other, [NotNullWhen(true)] out QueryExpression? result)
    {
        if (other.Value is null)
        {
            if (other.Inverted)
            {
                result = other;
                return true;
            }
            result = null;
            return false;
        }

        if (Contains(other.Value))
        {
            if (!other.Inverted)
            {
                result = this;
                return true;
            }
            result = new BTreeIndexRange(nullable)
            {
                Field = Field,
                From = null,
                FromInclusive = false,
                To = null,
                ToInclusive = false,
            };
            return true;
        }
        if (other.Inverted)
        {
            result = other;
            return true;
        }
        if (From is not null && From.CompareTo(other.Value) == 0)
        {
            result = new BTreeIndexRange(nullable)
            {
                Field = Field,
                From = From,
                To = To,
                FromInclusive = true,
                ToInclusive = ToInclusive,
            };
            return true;
        }
        if (To is not null && To.CompareTo(other.Value) == 0)
        {
            result = new BTreeIndexRange(nullable)
            {
                Field = Field,
                From = From,
                To = To,
                FromInclusive = FromInclusive,
                ToInclusive = true,
            };
            return true;
        }
        result = null;
        return false;
    }

    private static bool TryUnion(BTreeIndexRange r1, BTreeIndexRange r2, bool nullable, [NotNullWhen(true)] out QueryExpression? result)
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
                result = new BTreeIndexRange(nullable)
                {
                    Field = field,
                    From = r1.From,
                    To = r1.To,
                    FromInclusive = r1.FromInclusive,
                    ToInclusive = r1.ToInclusive,
                };
                return true;
            default:
                result = new BTreeIndexRange(nullable)
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

    public override bool TryInvert([NotNullWhen(true)] out QueryExpression? result)
    {
        result = (From, To) switch
        {
            (null, null) => null,
            (not null, null) => new BTreeIndexRange(nullable)
            {
                From = null,
                To = From,
                FromInclusive = false,
                ToInclusive = !FromInclusive,
                Field = Field,
            },
            (null, not null) => new BTreeIndexRange(nullable)
            {
                From = To,
                To = null,
                FromInclusive = !ToInclusive,
                ToInclusive = false,
                Field = Field,
            },
            (not null, not null) => new Or
            {
                Terms =
                [
                    new BTreeIndexRange(nullable)
                    {
                        From = null,
                        To = From,
                        FromInclusive = false,
                        ToInclusive = !FromInclusive,
                        Field = Field,
                    },
                    new BTreeIndexRange(nullable)
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

    public override bool TrySimplify([NotNullWhen(true)] out QueryExpression? result)
    {
        if (From == null && To == null)
        {
            result = new BTreeIndexEquality(nullable)
            {
                Field = Field,
                Inverted = true,
                Value = null,
            };
            return true;
        }
        result = null;
        return false;
    }

    public override Expression ToExpression(ParameterExpression propertyParameter)
    {
        var fromExpr = (From, FromInclusive) switch
        {
            (null, _) => null,
            (not null, true) => Expression.GreaterThanOrEqual(propertyParameter, Expression.Constant(From)),
            (not null, false) => Expression.GreaterThan(propertyParameter, Expression.Constant(From)),
        };
        var toExpr = (To, ToInclusive) switch
        {
            (null, _) => null,
            (not null, true) => Expression.LessThanOrEqual(propertyParameter, Expression.Constant(To)),
            (not null, false) => Expression.LessThan(propertyParameter, Expression.Constant(To)),
        };
        return (fromExpr, toExpr) switch
        {
            (null, null) => Expression.Constant(true),
            (var from, null) => from,
            (null, var to) => to,
            var (from, to) => Expression.AndAlso(from, to),
        };
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
            case (null, not null): return p1IsTo ? 1 : -1;
            case (not null, null): return p2IsTo ? -1 : 1;
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

    private bool Contains(IComparable value)
    {
        var fromCmp = value.CompareTo(From);
        var toCmp = value.CompareTo(To);
        return (From, fromCmp, FromInclusive, To, toCmp, ToInclusive) switch
        {
            (null, _, _, null, _, _) => true, // -inf < value < +inf
            (null, _, _, not null, <= 0, true) => true, // -inf < value <= to
            (null, _, _, not null, < 0, false) => true, // -inf < value < to
            (not null, >= 0, true, null, _, _) => true, // from <= value < +inf
            (not null, > 0, false, null, _, _) => true, // from < value < +inf
            (not null, >= 0, true, not null, <= 0, true) => true, // from <= value <= to
            (not null, >= 0, true, not null, < 0, false) => true, // from <= value < to
            (not null, > 0, false, not null, <= 0, true) => true, // from < value <= to
            (not null, > 0, false, not null, < 0, false) => true, // from < value < to
            _ => false,
        };
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

    public override int GetHashCode()
    {
        return 0; // TODO: make an actually useful hashcode
    }

    public override bool Equals(object? obj)
    {
        if (obj is not BTreeIndexRange otherRange) return false;
        return Field.Equals(otherRange.Field) && Equals(From, otherRange.From) &&
               Equals(To, otherRange.To) && FromInclusive == otherRange.FromInclusive &&
               ToInclusive == otherRange.ToInclusive;
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