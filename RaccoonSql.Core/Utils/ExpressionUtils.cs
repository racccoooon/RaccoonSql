using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace RaccoonSql.Core.Utils;

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
            if (node is not null)
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

    public static Expression<T> RenameParams<T>(Expression<T> expr, Dictionary<ParameterExpression, string> newNames)
    {
        return new ParameterRenameVisitor(newNames).VisitAndConvert(expr, null);
    }

    private class ParameterRenameVisitor(IReadOnlyDictionary<ParameterExpression, string> newNames) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (newNames.TryGetValue(node, out var value))
            {
                return Expression.Parameter(node.Type, value);
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

    public static List<List<Expression>> NormalizeDnf(Expression expr)
    {
        var boolTree = TransformToBool(expr);
        var dnfTree = new BooleanExpressionDnfTransformer()
            .Visit(boolTree);

        Queue<BooleanExpressionNode> orMembers = new([dnfTree]);
        var complete = false;
        while (!complete)
        {
            complete = true;
            Queue<BooleanExpressionNode> orMembersNew = new();
            while (orMembers.Count != 0)
            {
                var node = orMembers.Dequeue();
                if (node is not BooleanExpressionOr or)
                {
                    orMembersNew.Enqueue(node);
                }
                else
                {
                    orMembersNew.Enqueue(or.Left);
                    orMembersNew.Enqueue(or.Right);
                    complete = false;
                }
            }

            orMembers = orMembersNew;
        }

        List<List<Expression>> result = [];
        foreach (var orMember in orMembers)
        {
            Queue<BooleanExpressionNode> andMembers = new([orMember]);
            complete = false;
            while (!complete)
            {
                complete = true;
                Queue<BooleanExpressionNode> andMembersNew = new();
                while (andMembers.Count != 0)
                {
                    var node = andMembers.Dequeue();
                    if (node is not BooleanExpressionAnd and)
                    {
                        andMembersNew.Enqueue(node);
                    }
                    else
                    {
                        andMembersNew.Enqueue(and.Left);
                        andMembersNew.Enqueue(and.Right);
                        complete = false;
                    }
                }

                andMembers = andMembersNew;
            }

            List<Expression> list = [];
            foreach (var node in andMembers)
            {
                switch (node)
                {
                    case BooleanExpressionBox box:
                        list.Add(box.Expression);
                        break;
                    case BooleanExpressionNot { Expr: BooleanExpressionBox box2 }:
                        list.Add(Expression.Not(box2.Expression));
                        break;
                    default:
                        throw new UnreachableException("invalid normalized DNF");
                }
            }

            result.Add(list);
        }

        return result;
    }

    private static BooleanExpressionNode TransformToBool(Expression expr) =>
        expr switch
        {
            BinaryExpression { NodeType: ExpressionType.AndAlso, Left: { } left, Right: { } right } =>
                new BooleanExpressionAnd { Left = TransformToBool(left), Right = TransformToBool(right) },
            BinaryExpression { NodeType: ExpressionType.OrElse, Left: { } left, Right: { } right } =>
                new BooleanExpressionOr { Left = TransformToBool(left), Right = TransformToBool(right) },
            UnaryExpression { NodeType: ExpressionType.Not } unaryExpression when unaryExpression.Type == typeof(bool)
                => new BooleanExpressionNot { Expr = TransformToBool(unaryExpression.Operand), },
            _ => new BooleanExpressionBox { Expression = expr }
        };

    private class BooleanExpressionDnfTransformer : BooleanExpressionNodeVisitor
    {
        protected override BooleanExpressionNode VisitNot(BooleanExpressionNot not)
        {
            if (not.Expr is BooleanExpressionNot not2)
            {
                return base.Visit(not2.Expr);
            }

            if (not.Expr is BooleanExpressionOr or)
            {
                return base.Visit(new BooleanExpressionAnd
                {
                    Left = new BooleanExpressionNot { Expr = or.Left },
                    Right = new BooleanExpressionNot { Expr = or.Right },
                });
            }

            if (not.Expr is BooleanExpressionAnd and)
            {
                return base.Visit(new BooleanExpressionOr
                {
                    Left = new BooleanExpressionNot { Expr = and.Left },
                    Right = new BooleanExpressionNot { Expr = and.Right },
                });
            }

            return base.VisitNot(not);
        }

        protected override BooleanExpressionNode VisitAnd(BooleanExpressionAnd and)
        {
            if (and.Left is BooleanExpressionOr or1)
            {
                return base.Visit(new BooleanExpressionOr
                {
                    Left = new BooleanExpressionAnd
                    {
                        Left = or1.Left,
                        Right = and.Right,
                    },
                    Right = new BooleanExpressionAnd
                    {
                        Left = or1.Right,
                        Right = and.Right
                    }
                });
            }

            if (and.Right is BooleanExpressionOr or2)
            {
                return base.Visit(new BooleanExpressionOr
                {
                    Left = new BooleanExpressionAnd
                    {
                        Left = and.Left,
                        Right = or2.Left,
                    },
                    Right = new BooleanExpressionAnd
                    {
                        Left = and.Left,
                        Right = or2.Right
                    }
                });
            }

            return base.VisitAnd(and);
        }
    }

    private class BooleanExpressionNodeVisitor
    {
        public virtual BooleanExpressionNode Visit(BooleanExpressionNode node) =>
            node switch
            {
                BooleanExpressionAnd and => VisitAnd(and),
                BooleanExpressionOr or => VisitOr(or),
                BooleanExpressionNot not => VisitNot(not),
                BooleanExpressionBox box => VisitBox(box),
                _ => throw new ArgumentException("Unsupported node type", nameof(node))
            };

        protected virtual BooleanExpressionNode VisitAnd(BooleanExpressionAnd and)
        {
            Visit(and.Left);
            Visit(and.Right);
            return and;
        }


        protected virtual BooleanExpressionNode VisitOr(BooleanExpressionOr or)
        {
            Visit(or.Left);
            Visit(or.Right);
            return or;
        }


        protected virtual BooleanExpressionNode VisitNot(BooleanExpressionNot not)
        {
            Visit(not.Expr);
            return not;
        }


        protected virtual BooleanExpressionNode VisitBox(BooleanExpressionBox box)
        {
            return box;
        }
    }

    private class BooleanExpressionNode;

    private class BooleanExpressionBox : BooleanExpressionNode
    {
        public required Expression Expression { get; init; }
    }

    private class BooleanExpressionBinary : BooleanExpressionNode
    {
        public required BooleanExpressionNode Left { get; init; }
        public required BooleanExpressionNode Right { get; init; }
    }

    private class BooleanExpressionAnd : BooleanExpressionBinary;

    private class BooleanExpressionOr : BooleanExpressionBinary;

    private class BooleanExpressionUnary : BooleanExpressionNode
    {
        public required BooleanExpressionNode Expr { get; init; }
    }

    private class BooleanExpressionNot : BooleanExpressionUnary;
}

public class ExpressionIsNotPropertyAccessorException : Exception;