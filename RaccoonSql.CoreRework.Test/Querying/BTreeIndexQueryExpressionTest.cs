using System.Diagnostics;
using FluentAssertions;
using RaccoonSql.CoreRework.Indexes;
using RaccoonSql.CoreRework.Querying;

namespace RaccoonSql.CoreRework.Test.Querying;

public class BTreeIndexQueryExpressionTest
{
    public int FakeProp { get; set; }

    private static QueryExpression ParseExpressionString(string input, bool nullable)
    {
        var field = new QueryExpressionModelField
        {
            PropertyInfo = typeof(BTreeIndexQueryExpressionTest).GetProperty(nameof(FakeProp))!
        };

        var inputs = input.Split(';');

        List<QueryExpression> queries = new(inputs.Length);

        foreach (var s in inputs)
        {
            switch (s)
            {
                case "":
                    queries.Add(QueryExpression.Box.True());
                    break;
                case "!":
                    queries.Add(QueryExpression.Box.False());
                    break;
                case "_":
                    queries.Add(new BTreeIndexEquality(nullable)
                    {
                        Field = field,
                        Inverted = false,
                        Value = null,
                    });
                    break;
                case "!_":
                    queries.Add(new BTreeIndexEquality(nullable)
                    {
                        Field = field,
                        Inverted = true,
                        Value = null,
                    });
                    break;
                default:
                {
                    if ("[(,)]".Any(c => s.Contains(c)))
                    {
                        Debug.Assert(s.Length >= 5);
                        Debug.Assert(s[0] == '[' || s[0] == '(');
                        Debug.Assert(s[^1] == ']' || s[^1] == ')');

                        var commaPos = s.IndexOf(',');
                        Debug.Assert(commaPos > 1 && commaPos < s.Length - 2);
                        int? from, to;

                        if (s[1] == '_')
                        {
                            from = null;
                            Debug.Assert(commaPos == 2);
                        }
                        else
                        {
                            from = Convert.ToInt32(s.Substring(1, commaPos - 1));
                        }

                        var fromInclusive = from is not null && s[0] == '[';

                        if (s[commaPos + 1] == '_')
                        {
                            to = null;
                            Debug.Assert(s.Length == commaPos + 3);
                        }
                        else
                        {
                            to = Convert.ToInt32(s[(commaPos + 1)..^1]);
                        }

                        var toInclusive = to is not null && s[^1] == ']';

                        if (from is not null && to is not null)
                        {
                            Debug.Assert(to >= from);
                        }

                        queries.Add(new BTreeIndexRange(nullable)
                        {
                            Field = field,
                            From = from,
                            To = to,
                            FromInclusive = fromInclusive,
                            ToInclusive = toInclusive,
                        });
                    }
                    else
                    {
                        Debug.Assert(s.Length >= 1);
                        bool invert = false;
                        if (s[0] == '!')
                        {
                            Debug.Assert(s.Length >= 2);
                            invert = true;
                        }
                        queries.Add(new BTreeIndexEquality(nullable)
                        {
                            Field = field,
                            Inverted = invert,
                            Value = Convert.ToInt32(invert ? s[1..] : s),
                        });
                    }
                    break;
                }
            }
        }

        if (queries.Count == 1)
        {
            return queries[0];
        }
        return new QueryExpression.Or
        {
            Terms = queries,
        };

    }

    public static IEnumerable<object[]> GetCasesNullable()
    {
        foreach (var objects in GetCases())
        {
            var comment = objects[0] as string;
            yield return [$"[nullable] {comment}", true, ..objects[1..]];
        }
    }
    public static IEnumerable<object[]> GetCasesNotNullable()
    {
        foreach (var objects in GetCases())
        {
            var comment = objects[0] as string;
            yield return [$"[not nullable] {comment}", false, ..objects[1..]];
        }
    }
    
    
    
    private static IEnumerable<object[]> GetCases()
    {
        var comment = "overlap, no nulls";
        yield return [comment, "(10,30]", "[20,40)", "[20,30]", "(10,40)"];
        yield return [comment, "(10,30]", "(20,40)", "(20,30]", "(10,40)"];
        yield return [comment, "(10,30)", "[20,40)", "[20,30)", "(10,40)"];
        yield return [comment, "(10,30)", "(20,40)", "(20,30)", "(10,40)"];
        yield return [comment, "[10,30]", "[20,40)", "[20,30]", "[10,40)"];
        yield return [comment, "[10,30]", "(20,40)", "(20,30]", "[10,40)"];
        yield return [comment, "[10,30)", "[20,40)", "[20,30)", "[10,40)"];
        yield return [comment, "[10,30)", "(20,40)", "(20,30)", "[10,40)"];
        yield return [comment, "(10,30]", "[20,40]", "[20,30]", "(10,40]"];
        yield return [comment, "(10,30]", "(20,40]", "(20,30]", "(10,40]"];
        yield return [comment, "(10,30)", "[20,40]", "[20,30)", "(10,40]"];
        yield return [comment, "(10,30)", "(20,40]", "(20,30)", "(10,40]"];
        yield return [comment, "[10,30]", "[20,40]", "[20,30]", "[10,40]"];
        yield return [comment, "[10,30]", "(20,40]", "(20,30]", "[10,40]"];
        yield return [comment, "[10,30)", "[20,40]", "[20,30)", "[10,40]"];
        yield return [comment, "[10,30)", "(20,40]", "(20,30)", "[10,40]"];

        comment = "contain, no nulls";
        yield return [comment, "(20,50)", "(30,40)", "(30,40)", "(20,50)"];
        yield return [comment, "(20,50)", "[30,40)", "[30,40)", "(20,50)"];
        yield return [comment, "(20,50)", "(30,40]", "(30,40]", "(20,50)"];
        yield return [comment, "(20,50)", "[30,40]", "[30,40]", "(20,50)"];
        yield return [comment, "[20,50)", "(30,40)", "(30,40)", "[20,50)"];
        yield return [comment, "[20,50)", "[30,40)", "[30,40)", "[20,50)"];
        yield return [comment, "[20,50)", "(30,40]", "(30,40]", "[20,50)"];
        yield return [comment, "[20,50)", "[30,40]", "[30,40]", "[20,50)"];
        yield return [comment, "(20,50]", "(30,40)", "(30,40)", "(20,50]"];
        yield return [comment, "(20,50]", "[30,40)", "[30,40)", "(20,50]"];
        yield return [comment, "(20,50]", "(30,40]", "(30,40]", "(20,50]"];
        yield return [comment, "(20,50]", "[30,40]", "[30,40]", "(20,50]"];
        yield return [comment, "[20,50]", "(30,40)", "(30,40)", "[20,50]"];
        yield return [comment, "[20,50]", "[30,40)", "[30,40)", "[20,50]"];
        yield return [comment, "[20,50]", "(30,40]", "(30,40]", "[20,50]"];
        yield return [comment, "[20,50]", "[30,40]", "[30,40]", "[20,50]"];

        comment = "contain same start, no nulls";
        yield return [comment, "(20,50)", "(20,30)", "(20,30)", "(20,50)"];
        yield return [comment, "(20,50)", "[20,30)", "(20,30)", "[20,50)"];
        yield return [comment, "[20,50)", "(20,30)", "(20,30)", "[20,50)"];
        yield return [comment, "[20,50)", "[20,30)", "[20,30)", "[20,50)"];
        yield return [comment, "(20,50]", "(20,30)", "(20,30)", "(20,50]"];
        yield return [comment, "(20,50]", "[20,30)", "(20,30)", "[20,50]"];
        yield return [comment, "[20,50]", "(20,30)", "(20,30)", "[20,50]"];
        yield return [comment, "[20,50]", "[20,30)", "[20,30)", "[20,50]"];

        comment = "contain same end, no nulls";
        yield return [comment, "(20,50)", "(30,50)", "(30,50)", "(20,50)"];
        yield return [comment, "(20,50]", "(30,50)", "(30,50)", "(20,50]"];
        yield return [comment, "(20,50)", "(30,50]", "(30,50)", "(20,50]"];
        yield return [comment, "(20,50]", "(30,50]", "(30,50]", "(20,50]"];
        yield return [comment, "[20,50)", "(30,50)", "(30,50)", "[20,50)"];
        yield return [comment, "[20,50]", "(30,50)", "(30,50)", "[20,50]"];
        yield return [comment, "[20,50)", "(30,50]", "(30,50)", "[20,50]"];
        yield return [comment, "[20,50]", "(30,50]", "(30,50]", "[20,50]"];

        comment = "same, no nulls";
        yield return [comment, "(10,20)", "(10,20)", "(10,20)", "(10,20)"];
        yield return [comment, "[10,20)", "(10,20)", "(10,20)", "[10,20)"];
        yield return [comment, "(10,20]", "(10,20)", "(10,20)", "(10,20]"];
        yield return [comment, "[10,20]", "(10,20)", "(10,20)", "[10,20]"];
        yield return [comment, "(10,20)", "[10,20)", "(10,20)", "[10,20)"];
        yield return [comment, "[10,20)", "[10,20)", "[10,20)", "[10,20)"];
        yield return [comment, "(10,20]", "[10,20)", "(10,20)", "[10,20]"];
        yield return [comment, "[10,20]", "[10,20)", "[10,20)", "[10,20]"];
        yield return [comment, "(10,20)", "(10,20]", "(10,20)", "(10,20]"];
        yield return [comment, "[10,20)", "(10,20]", "(10,20)", "[10,20]"];
        yield return [comment, "(10,20]", "(10,20]", "(10,20]", "(10,20]"];
        yield return [comment, "[10,20]", "(10,20]", "(10,20]", "[10,20]"];
        yield return [comment, "(10,20)", "[10,20]", "(10,20)", "[10,20]"];
        yield return [comment, "[10,20)", "[10,20]", "[10,20)", "[10,20]"];
        yield return [comment, "(10,20]", "[10,20]", "(10,20]", "[10,20]"];
        yield return [comment, "[10,20]", "[10,20]", "[10,20]", "[10,20]"];

        comment = "barely overlap, no nulls";
        yield return [comment, "(10,20]", "[20, 30)", "[20,20]", "(10,30)"];
        yield return [comment, "(10,20]", "[20, 30]", "[20,20]", "(10,30]"];
        yield return [comment, "[10,20]", "[20, 30)", "[20,20]", "[10,30)"];
        yield return [comment, "[10,20]", "[20, 30]", "[20,20]", "[10,30]"];

        comment = "overlap, null start";
        yield return [comment, "(_,30]", "[20,40)", "[20,30]", "(_,40)"];
        yield return [comment, "(_,30)", "[20,40)", "[20,30)", "(_,40)"];
        yield return [comment, "(_,30]", "(20,40)", "(20,30]", "(_,40)"];
        yield return [comment, "(_,30)", "(20,40)", "(20,30)", "(_,40)"];
        yield return [comment, "(_,30]", "[20,40]", "[20,30]", "(_,40]"];
        yield return [comment, "(_,30)", "[20,40]", "[20,30)", "(_,40]"];
        yield return [comment, "(_,30]", "(20,40]", "(20,30]", "(_,40]"];
        yield return [comment, "(_,30)", "(20,40]", "(20,30)", "(_,40]"];

        comment = "overlap, null end";
        yield return [comment, "[30,_)", "(20,40]", "[30,40]", "(20,_)"];
        yield return [comment, "(30,_)", "(20,40]", "(30,40]", "(20,_)"];
        yield return [comment, "[30,_)", "(20,40)", "[30,40)", "(20,_)"];
        yield return [comment, "(30,_)", "(20,40)", "(30,40)", "(20,_)"];
        yield return [comment, "[30,_)", "[20,40]", "[30,40]", "[20,_)"];
        yield return [comment, "(30,_)", "[20,40]", "(30,40]", "[20,_)"];
        yield return [comment, "[30,_)", "[20,40)", "[30,40)", "[20,_)"];
        yield return [comment, "(30,_)", "[20,40)", "(30,40)", "[20,_)"];

        comment = "overlap, null start and end";
        yield return [comment, "(_,40)", "(20,_)", "(20,40)", "(_,_)"];
        yield return [comment, "(_,40]", "(20,_)", "(20,40]", "(_,_)"];
        yield return [comment, "(_,40)", "[20,_)", "[20,40)", "(_,_)"];
        yield return [comment, "(_,40]", "[20,_)", "[20,40]", "(_,_)"];

        comment = "overlap, null start and end both";
        yield return [comment, "(_,_)", "(_,_)", "(_,_)", "(_,_)"];

        comment = "contain, null start";
        yield return [comment, "(_,50)", "(10,20)", "(10,20)", "(_,50)"];
        yield return [comment, "(_,50)", "[10,20)", "[10,20)", "(_,50)"];
        yield return [comment, "(_,50)", "(10,20]", "(10,20]", "(_,50)"];
        yield return [comment, "(_,50)", "[10,20]", "[10,20]", "(_,50)"];
        yield return [comment, "(_,50]", "(10,20)", "(10,20)", "(_,50]"];
        yield return [comment, "(_,50]", "[10,20)", "[10,20)", "(_,50]"];
        yield return [comment, "(_,50]", "(10,20]", "(10,20]", "(_,50]"];
        yield return [comment, "(_,50]", "[10,20]", "[10,20]", "(_,50]"];

        comment = "contain, null end";
        yield return [comment, "(-10,_)", "(10,20)", "(10,20)", "(-10,_)"];
        yield return [comment, "(-10,_)", "[10,20)", "[10,20)", "(-10,_)"];
        yield return [comment, "(-10,_)", "(10,20]", "(10,20]", "(-10,_)"];
        yield return [comment, "(-10,_)", "[10,20]", "[10,20]", "(-10,_)"];
        yield return [comment, "(-10,_]", "(10,20)", "(10,20)", "(-10,_]"];
        yield return [comment, "(-10,_]", "[10,20)", "[10,20)", "(-10,_]"];
        yield return [comment, "(-10,_]", "(10,20]", "(10,20]", "(-10,_]"];
        yield return [comment, "(-10,_]", "[10,20]", "[10,20]", "(-10,_]"];

        comment = "contain, null start both";
        yield return [comment, "(_,30]", "(_,40)", "(_,30]", "(_,40)"];
        yield return [comment, "(_,30)", "(_,40)", "(_,30)", "(_,40)"];
        yield return [comment, "(_,30]", "(_,40]", "(_,30]", "(_,40]"];
        yield return [comment, "(_,30)", "(_,40]", "(_,30)", "(_,40]"];

        comment = "contain, null end both";
        yield return [comment, "[30,_)", "(20,_]", "[30,_)", "(20,_)"];
        yield return [comment, "(30,_)", "(20,_]", "(30,_)", "(20,_)"];
        yield return [comment, "[30,_)", "[20,_]", "[30,_)", "[20,_)"];
        yield return [comment, "(30,_)", "[20,_]", "(30,_)", "[20,_)"];

        comment = "no intersect, no nulls";
        yield return [comment, "[10,30]", "[40,60]", null, null];
        yield return [comment, "(10,30]", "[40,60]", null, null];
        yield return [comment, "[10,30)", "[40,60]", null, null];
        yield return [comment, "(10,30)", "[40,60]", null, null];
        yield return [comment, "[10,30]", "(40,60]", null, null];
        yield return [comment, "(10,30]", "(40,60]", null, null];
        yield return [comment, "[10,30)", "(40,60]", null, null];
        yield return [comment, "(10,30)", "(40,60]", null, null];
        yield return [comment, "[10,30]", "[40,60)", null, null];
        yield return [comment, "(10,30]", "[40,60)", null, null];
        yield return [comment, "[10,30)", "[40,60)", null, null];
        yield return [comment, "(10,30)", "[40,60)", null, null];
        yield return [comment, "[10,30]", "(40,60)", null, null];
        yield return [comment, "(10,30]", "(40,60)", null, null];
        yield return [comment, "[10,30)", "(40,60)", null, null];
        yield return [comment, "(10,30)", "(40,60)", null, null];

        comment = "no intersect, null start";
        yield return [comment, "(_,40)", "(50,70)", null, null];
        yield return [comment, "(_,40]", "(50,70)", null, null];
        yield return [comment, "(_,40)", "[50,70)", null, null];
        yield return [comment, "(_,40]", "[50,70)", null, null];
        yield return [comment, "(_,40)", "(50,70]", null, null];
        yield return [comment, "(_,40]", "(50,70]", null, null];
        yield return [comment, "(_,40)", "[50,70]", null, null];
        yield return [comment, "(_,40]", "[50,70]", null, null];

        comment = "no intersect, null end";
        yield return [comment, "(40,60)", "(80,_)", null, null];
        yield return [comment, "[40,60)", "(80,_)", null, null];
        yield return [comment, "(40,60]", "(80,_)", null, null];
        yield return [comment, "[40,60]", "(80,_)", null, null];
        yield return [comment, "(40,60)", "[80,_)", null, null];
        yield return [comment, "[40,60)", "[80,_)", null, null];
        yield return [comment, "(40,60]", "[80,_)", null, null];
        yield return [comment, "[40,60]", "[80,_)", null, null];

        comment = "barely no overlap, no nulls";
        yield return [comment, "(10,20)", "(20,30)", null, null];
        yield return [comment, "(10,20)", "[20,30)", null, null];
        yield return [comment, "(10,20]", "(20,30)", null, null];
        yield return [comment, "[10,20)", "(20,30)", null, null];
        yield return [comment, "[10,20)", "[20,30)", null, null];
        yield return [comment, "[10,20]", "(20,30)", null, null];
        yield return [comment, "(10,20)", "(20,30]", null, null];
        yield return [comment, "(10,20)", "[20,30]", null, null];
        yield return [comment, "(10,20]", "(20,30]", null, null];
        yield return [comment, "[10,20)", "(20,30]", null, null];
        yield return [comment, "[10,20)", "[20,30]", null, null];
        yield return [comment, "[10,20]", "(20,30]", null, null];

        comment = "barely no overlap, null start";
        yield return [comment, "(_,10)", "(10,20)", null, null];
        yield return [comment, "(_,10]", "(10,20)", null, null];
        yield return [comment, "(_,10)", "[10,20)", null, null];
        yield return [comment, "(_,10)", "(10,20]", null, null];
        yield return [comment, "(_,10]", "(10,20]", null, null];
        yield return [comment, "(_,10)", "[10,20]", null, null];

        comment = "barely no overlap, null end";
        yield return [comment, "(20,40)", "(40,_)", null, null];
        yield return [comment, "(20,40)", "[40,_)", null, null];
        yield return [comment, "(20,40]", "(40,_)", null, null];
        yield return [comment, "[20,40)", "(40,_)", null, null];
        yield return [comment, "[20,40)", "[40,_)", null, null];
        yield return [comment, "[20,40]", "(40,_)", null, null];

        comment = "(in)equality nodes, same value";
        yield return [comment, "20", "20", "20", "20"];
        yield return [comment, "20", "!20", null, "(_,_)"];
        yield return [comment, "!20", "20", null, "(_,_)"];
        yield return [comment, "!20", "!20", "!20", "!20"];

        comment = "(in)equality nodes, null";
        yield return [comment, "_", "_", "_", "_"];
        yield return [comment, "_", "!_", null, ""];
        yield return [comment, "!_", "_", null, ""];
        yield return [comment, "!_", "!_", "!_", "!_"];

        comment = "(in)equality nodes, different value";
        yield return [comment, "20", "30", null, null];
        yield return [comment, "20", "!30", "20", "!30"];
        yield return [comment, "!20", "30", "30", "!20"];
        yield return [comment, "!20", "!30", "(_,20);(20,30);(30,_)", "(_,_)"];

        comment = "(in)equality nodes, null and value";
        yield return [comment, "20", "_", null, null];
        yield return [comment, "20", "!_", "20", "!_"];
        yield return [comment, "!20", "_", null, null];
        yield return [comment, "!20", "!_", "!20", "!_"];

        comment = "equality and range, contain";
        yield return [comment, "(20,40)", "30", "30", "(20,40)"];
        yield return [comment, "[20,40)", "30", "30", "[20,40)"];
        yield return [comment, "(20,40]", "30", "30", "(20,40]"];
        yield return [comment, "[20,40]", "30", "30", "[20,40]"];

        comment = "inequality and range, contain";
        yield return [comment, "(20,40)", "!30", "(20,30);(30,40)", "(_,_)"];
        yield return [comment, "[20,40)", "!30", "[20,30);(30,40)", "(_,_)"];
        yield return [comment, "(20,40]", "!30", "(20,30);(30,40]", "(_,_)"];
        yield return [comment, "[20,40]", "!30", "[20,30);(30,40]", "(_,_)"];

        comment = "equality and range, boundary contain";
        yield return [comment, "(20,40]", "40", "40", "(20,40]"];
        yield return [comment, "[20,40]", "40", "40", "[20,40]"];

        comment = "equality and range, boundary not contain";
        yield return [comment, "(20,40)", "40", null, "(20,40]"];
        yield return [comment, "[20,40)", "40", null, "[20,40]"];

        comment = "inequality and range, boundary contain";
        yield return [comment, "(20,40]", "!40", "(20,40)", "(_,_)"];
        yield return [comment, "[20,40]", "!40", "[20,40)", "(_,_)"];

        comment = "inequality and range, boundary not contain";
        yield return [comment, "(20,40)", "!40", "(20,40)", "!40"];
        yield return [comment, "[20,40)", "!40", "[20,40)", "!40"];

        comment = "equality and range, no contain";
        yield return [comment, "(10,50)", "70", null, null];
        yield return [comment, "[10,50)", "70", null, null];
        yield return [comment, "(10,50]", "70", null, null];
        yield return [comment, "[10,50]", "70", null, null];

        comment = "inequality and range, no contain";
        yield return [comment, "(10,50)", "!70", "(10,50)", "!70"];
        yield return [comment, "[10,50)", "!70", "[10,50)", "!70"];
        yield return [comment, "(10,50]", "!70", "(10,50]", "!70"];
        yield return [comment, "[10,50]", "!70", "[10,50]", "!70"];

        comment = "null equality and range";
        yield return [comment, "(10,50)", "_", null, null];
        yield return [comment, "(10,50]", "_", null, null];
        yield return [comment, "[10,50)", "_", null, null];
        yield return [comment, "[10,50]", "_", null, null];
        yield return [comment, "(10,_)", "_", null, null];
        yield return [comment, "[10,_)", "_", null, null];
        yield return [comment, "(_,10)", "_", null, null];
        yield return [comment, "(_,10]", "_", null, null];
        yield return [comment, "(_,_)", "_", null, null];
        
        comment = "null inequality and range";
        yield return [comment, "(10,50)", "!_", "(10,50)", "!_"];
        yield return [comment, "(10,50]", "!_", "(10,50]", "!_"];
        yield return [comment, "[10,50)", "!_", "[10,50)", "!_"];
        yield return [comment, "[10,50]", "!_", "[10,50]", "!_"];
        yield return [comment, "(10,_)", "!_", "(10,_)", "!_"];
        yield return [comment, "[10,_)", "!_", "[10,_)", "!_"];
        yield return [comment, "(_,10)", "!_", "(_,10)", "!_"];
        yield return [comment, "(_,10]", "!_", "(_,10]", "!_"];
        yield return [comment, "(_,_)", "!_", "(_,_)", "!_"];

    }

    [Theory]
    [MemberData(nameof(GetCasesNullable))]
    [MemberData(nameof(GetCasesNotNullable))]
    public void Intersect(string comment, bool nullable, string q1, string q2, string? intersectionStr, string? unionStr)
    {
        // arrange 

        var query1 = ParseExpressionString(q1, nullable);
        Debug.Assert(query1 is IndexQueryExpression);
        var query2 = ParseExpressionString(q2, nullable);
        Debug.Assert(query2 is IndexQueryExpression);
        var success = intersectionStr != null;
        var expectedIntersection = intersectionStr == null ? null : ParseExpressionString(intersectionStr, nullable).Normalize();

        // act

        var result = (query1 as IndexQueryExpression)!.TryIntersect(query2 as IndexQueryExpression, out var intersection);
        intersection = intersection?.Normalize();
        var resultInverted = (query2 as IndexQueryExpression)!.TryIntersect(query1 as IndexQueryExpression, out var intersectionInverted);
        intersectionInverted = intersectionInverted?.Normalize();

        // assert

        result.Should().Be(success);
        resultInverted.Should().Be(success);
        intersection.Should().BeEquivalentTo(expectedIntersection, options => options.RespectingRuntimeTypes());
        intersectionInverted.Should().BeEquivalentTo(expectedIntersection, options => options.RespectingRuntimeTypes());
    }

    [Theory]
    [MemberData(nameof(GetCasesNullable))]
    [MemberData(nameof(GetCasesNotNullable))]
    public void Union(string comment,bool nullable, string q1, string q2, string? intersectionStr, string? unionStr)
    {
        // arrange 

        var query1 = ParseExpressionString(q1, nullable);
        Debug.Assert(query1 is IndexQueryExpression);
        var query2 = ParseExpressionString(q2, nullable);
        Debug.Assert(query2 is IndexQueryExpression);
        var success = unionStr != null;
        var expectedUnion = unionStr == null ? null : ParseExpressionString(unionStr, nullable).Normalize();

        // act

        var result = (query1 as IndexQueryExpression)!.TryUnion((query2 as IndexQueryExpression)!, out var union);
        union = union?.Normalize();
        var resultInverted = (query2 as IndexQueryExpression)!.TryUnion((query1 as IndexQueryExpression)!, out var unionInverted);
        unionInverted = unionInverted?.Normalize();

        // assert

        result.Should().Be(success);
        resultInverted.Should().Be(success);
        union.Should().BeEquivalentTo(expectedUnion, options => options.RespectingRuntimeTypes());
        unionInverted.Should().BeEquivalentTo(expectedUnion, options => options.RespectingRuntimeTypes());
    }
}