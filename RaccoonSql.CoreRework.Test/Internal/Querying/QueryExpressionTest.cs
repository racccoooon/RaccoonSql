using System.Diagnostics;
using FluentAssertions;
using RaccoonSql.CoreRework.Internal.Querying;

namespace RaccoonSql.CoreRework.Test.Internal.Querying;

public class QueryExpressionRangeTest
{
    public int FakeProp { get; set; }


    private static QueryExpression.Range ParseRangeString(string input)
    {
        var field = new QueryExpressionModelField { PropertyInfo = typeof(QueryExpressionRangeTest).GetProperty(nameof(FakeProp))! };

        Debug.Assert(input.Length >= 5);
        Debug.Assert(input[0] == '[' || input[0] == '(');
        Debug.Assert(input[^1] == ']' || input[^1] == ')');

        var commaPos = input.IndexOf(',');
        Debug.Assert(commaPos > 1 && commaPos < input.Length - 2);
        int? from, to;

        if (input[1] == '_')
        {
            from = null;
            Debug.Assert(commaPos == 2);
        }
        else
        {
            from = Convert.ToInt32(input.Substring(1, commaPos - 1));
        }


        var fromInclusive = from is not null && input[0] == '[';

        if (input[commaPos + 1] == '_')
        {
            to = null;
            Debug.Assert(input.Length == commaPos + 3);
        }
        else
        {
            to = Convert.ToInt32(input[(commaPos + 1)..^1]);
        }

        var toInclusive = to is not null && input[^1] == ']';

        if (from is not null && to is not null)
        {
            Debug.Assert(to >= from);
        }

        return new QueryExpression.Range
        {
            Field = field,
            From = from,
            To = to,
            FromInclusive = fromInclusive,
            ToInclusive = toInclusive,
        };
    }

    public static IEnumerable<object[]> GetRanges()
    {
        var comment = "overlap, no nulls";
        yield return [comment,"(10,30]", "[20,40)", true, "[20,30]", "(10,40)"];
        yield return [comment,"(10,30]", "(20,40)", true, "(20,30]", "(10,40)"];
        yield return [comment,"(10,30)", "[20,40)", true, "[20,30)", "(10,40)"];
        yield return [comment,"(10,30)", "(20,40)", true, "(20,30)", "(10,40)"];
        yield return [comment,"[10,30]", "[20,40)", true, "[20,30]", "[10,40)"];
        yield return [comment,"[10,30]", "(20,40)", true, "(20,30]", "[10,40)"];
        yield return [comment,"[10,30)", "[20,40)", true, "[20,30)", "[10,40)"];
        yield return [comment,"[10,30)", "(20,40)", true, "(20,30)", "[10,40)"];
        yield return [comment,"(10,30]", "[20,40]", true, "[20,30]", "(10,40]"];
        yield return [comment,"(10,30]", "(20,40]", true, "(20,30]", "(10,40]"];
        yield return [comment,"(10,30)", "[20,40]", true, "[20,30)", "(10,40]"];
        yield return [comment,"(10,30)", "(20,40]", true, "(20,30)", "(10,40]"];
        yield return [comment,"[10,30]", "[20,40]", true, "[20,30]", "[10,40]"];
        yield return [comment,"[10,30]", "(20,40]", true, "(20,30]", "[10,40]"];
        yield return [comment,"[10,30)", "[20,40]", true, "[20,30)", "[10,40]"];
        yield return [comment,"[10,30)", "(20,40]", true, "(20,30)", "[10,40]"];

        comment = "contain, no nulls";
        yield return [comment,"(20,50)", "(30,40)", true, "(30,40)", "(20,50)"];
        yield return [comment,"(20,50)", "[30,40)", true, "[30,40)", "(20,50)"];
        yield return [comment,"(20,50)", "(30,40]", true, "(30,40]", "(20,50)"];
        yield return [comment,"(20,50)", "[30,40]", true, "[30,40]", "(20,50)"];
        yield return [comment,"[20,50)", "(30,40)", true, "(30,40)", "[20,50)"];
        yield return [comment,"[20,50)", "[30,40)", true, "[30,40)", "[20,50)"];
        yield return [comment,"[20,50)", "(30,40]", true, "(30,40]", "[20,50)"];
        yield return [comment,"[20,50)", "[30,40]", true, "[30,40]", "[20,50)"];
        yield return [comment,"(20,50]", "(30,40)", true, "(30,40)", "(20,50]"];
        yield return [comment,"(20,50]", "[30,40)", true, "[30,40)", "(20,50]"];
        yield return [comment,"(20,50]", "(30,40]", true, "(30,40]", "(20,50]"];
        yield return [comment,"(20,50]", "[30,40]", true, "[30,40]", "(20,50]"];
        yield return [comment,"[20,50]", "(30,40)", true, "(30,40)", "[20,50]"];
        yield return [comment,"[20,50]", "[30,40)", true, "[30,40)", "[20,50]"];
        yield return [comment,"[20,50]", "(30,40]", true, "(30,40]", "[20,50]"];
        yield return [comment,"[20,50]", "[30,40]", true, "[30,40]", "[20,50]"];

        comment = "contain same start, no nulls";
        yield return [comment, "(20,50)", "(20,30)", true, "(20,30)", "(20,50)"];
        yield return [comment, "(20,50)", "[20,30)", true, "(20,30)", "[20,50)"];
        yield return [comment, "[20,50)", "(20,30)", true, "(20,30)", "[20,50)"];
        yield return [comment, "[20,50)", "[20,30)", true, "[20,30)", "[20,50)"];
        yield return [comment, "(20,50]", "(20,30)", true, "(20,30)", "(20,50]"];
        yield return [comment, "(20,50]", "[20,30)", true, "(20,30)", "[20,50]"];
        yield return [comment, "[20,50]", "(20,30)", true, "(20,30)", "[20,50]"];
        yield return [comment, "[20,50]", "[20,30)", true, "[20,30)", "[20,50]"];

        comment = "contain same end, no nulls";
        yield return [comment, "(20,50)", "(30,50)", true, "(30,50)", "(20,50)"];
        yield return [comment, "(20,50]", "(30,50)", true, "(30,50)", "(20,50]"];
        yield return [comment, "(20,50)", "(30,50]", true, "(30,50)", "(20,50]"];
        yield return [comment, "(20,50]", "(30,50]", true, "(30,50]", "(20,50]"];
        yield return [comment, "[20,50)", "(30,50)", true, "(30,50)", "[20,50)"];
        yield return [comment, "[20,50]", "(30,50)", true, "(30,50)", "[20,50]"];
        yield return [comment, "[20,50)", "(30,50]", true, "(30,50)", "[20,50]"];
        yield return [comment, "[20,50]", "(30,50]", true, "(30,50]", "[20,50]"];

        comment = "same, no nulls";
        yield return [comment, "(10,20)", "(10,20)", true, "(10,20)", "(10,20)"];
        yield return [comment, "[10,20)", "(10,20)", true, "(10,20)", "[10,20)"];
        yield return [comment, "(10,20]", "(10,20)", true, "(10,20)", "(10,20]"];
        yield return [comment, "[10,20]", "(10,20)", true, "(10,20)", "[10,20]"];
        yield return [comment, "(10,20)", "[10,20)", true, "(10,20)", "[10,20)"];
        yield return [comment, "[10,20)", "[10,20)", true, "[10,20)", "[10,20)"];
        yield return [comment, "(10,20]", "[10,20)", true, "(10,20)", "[10,20]"];
        yield return [comment, "[10,20]", "[10,20)", true, "[10,20)", "[10,20]"];
        yield return [comment, "(10,20)", "(10,20]", true, "(10,20)", "(10,20]"];
        yield return [comment, "[10,20)", "(10,20]", true, "(10,20)", "[10,20]"];
        yield return [comment, "(10,20]", "(10,20]", true, "(10,20]", "(10,20]"];
        yield return [comment, "[10,20]", "(10,20]", true, "(10,20]", "[10,20]"];
        yield return [comment, "(10,20)", "[10,20]", true, "(10,20)", "[10,20]"];
        yield return [comment, "[10,20)", "[10,20]", true, "[10,20)", "[10,20]"];
        yield return [comment, "(10,20]", "[10,20]", true, "(10,20]", "[10,20]"];
        yield return [comment, "[10,20]", "[10,20]", true, "[10,20]", "[10,20]"];

        comment = "barely overlap, no nulls";
        yield return [comment, "(10,20]", "[20, 30)", true, "[20,20]", "(10,30)"];
        yield return [comment, "(10,20]", "[20, 30]", true, "[20,20]", "(10,30]"];
        yield return [comment, "[10,20]", "[20, 30)", true, "[20,20]", "[10,30)"];
        yield return [comment, "[10,20]", "[20, 30]", true, "[20,20]", "[10,30]"];

        comment = "overlap, null start";
        yield return [comment, "(_,30]", "[20,40)", true, "[20,30]", "(_,40)"];
        yield return [comment, "(_,30)", "[20,40)", true, "[20,30)", "(_,40)"];
        yield return [comment, "(_,30]", "(20,40)", true, "(20,30]", "(_,40)"];
        yield return [comment, "(_,30)", "(20,40)", true, "(20,30)", "(_,40)"];
        yield return [comment, "(_,30]", "[20,40]", true, "[20,30]", "(_,40]"];
        yield return [comment, "(_,30)", "[20,40]", true, "[20,30)", "(_,40]"];
        yield return [comment, "(_,30]", "(20,40]", true, "(20,30]", "(_,40]"];
        yield return [comment, "(_,30)", "(20,40]", true, "(20,30)", "(_,40]"];

        comment = "overlap, null end";
        yield return [comment, "[30,_)", "(20,40]", true, "[30,40]", "(20,_)"];
        yield return [comment, "(30,_)", "(20,40]", true, "(30,40]", "(20,_)"];
        yield return [comment, "[30,_)", "(20,40)", true, "[30,40)", "(20,_)"];
        yield return [comment, "(30,_)", "(20,40)", true, "(30,40)", "(20,_)"];
        yield return [comment, "[30,_)", "[20,40]", true, "[30,40]", "[20,_)"];
        yield return [comment, "(30,_)", "[20,40]", true, "(30,40]", "[20,_)"];
        yield return [comment, "[30,_)", "[20,40)", true, "[30,40)", "[20,_)"];
        yield return [comment, "(30,_)", "[20,40)", true, "(30,40)", "[20,_)"];

        comment = "overlap, null start and end";
        yield return [comment, "(_,40)", "(20,_)", true, "(20,40)", "(_,_)"];
        yield return [comment, "(_,40]", "(20,_)", true, "(20,40]", "(_,_)"];
        yield return [comment, "(_,40)", "[20,_)", true, "[20,40)", "(_,_)"];
        yield return [comment, "(_,40]", "[20,_)", true, "[20,40]", "(_,_)"];

        comment = "overlap, null start and end both";
        yield return [comment, "(_,_)", "(_,_)", true, "(_,_)", "(_,_)"];

        comment = "contain, null start";
        yield return [comment, "(_,50)", "(10,20)", true, "(10,20)", "(_,50)"];
        yield return [comment, "(_,50)", "[10,20)", true, "[10,20)", "(_,50)"];
        yield return [comment, "(_,50)", "(10,20]", true, "(10,20]", "(_,50)"];
        yield return [comment, "(_,50)", "[10,20]", true, "[10,20]", "(_,50)"];
        yield return [comment, "(_,50]", "(10,20)", true, "(10,20)", "(_,50]"];
        yield return [comment, "(_,50]", "[10,20)", true, "[10,20)", "(_,50]"];
        yield return [comment, "(_,50]", "(10,20]", true, "(10,20]", "(_,50]"];
        yield return [comment, "(_,50]", "[10,20]", true, "[10,20]", "(_,50]"];

        comment = "contain, null end";
        yield return [comment, "(-10,_)", "(10,20)", true, "(10,20)", "(-10,_)"];
        yield return [comment, "(-10,_)", "[10,20)", true, "[10,20)", "(-10,_)"];
        yield return [comment, "(-10,_)", "(10,20]", true, "(10,20]", "(-10,_)"];
        yield return [comment, "(-10,_)", "[10,20]", true, "[10,20]", "(-10,_)"];
        yield return [comment, "(-10,_]", "(10,20)", true, "(10,20)", "(-10,_]"];
        yield return [comment, "(-10,_]", "[10,20)", true, "[10,20)", "(-10,_]"];
        yield return [comment, "(-10,_]", "(10,20]", true, "(10,20]", "(-10,_]"];
        yield return [comment, "(-10,_]", "[10,20]", true, "[10,20]", "(-10,_]"];

        comment = "contain, null start both";
        yield return [comment, "(_,30]", "(_,40)", true, "(_,30]", "(_,40)"];
        yield return [comment, "(_,30)", "(_,40)", true, "(_,30)", "(_,40)"];
        yield return [comment, "(_,30]", "(_,40]", true, "(_,30]", "(_,40]"];
        yield return [comment, "(_,30)", "(_,40]", true, "(_,30)", "(_,40]"];

        comment = "contain, null end both";
        yield return [comment, "[30,_)", "(20,_]", true, "[30,_)", "(20,_)"];
        yield return [comment, "(30,_)", "(20,_]", true, "(30,_)", "(20,_)"];
        yield return [comment, "[30,_)", "[20,_]", true, "[30,_)", "[20,_)"];
        yield return [comment, "(30,_)", "[20,_]", true, "(30,_)", "[20,_)"];

        comment = "no intersect, no nulls";
        yield return [comment, "[10,30]", "[40,60]", false, null, null];
        yield return [comment, "(10,30]", "[40,60]", false, null, null];
        yield return [comment, "[10,30)", "[40,60]", false, null, null];
        yield return [comment, "(10,30)", "[40,60]", false, null, null];
        yield return [comment, "[10,30]", "(40,60]", false, null, null];
        yield return [comment, "(10,30]", "(40,60]", false, null, null];
        yield return [comment, "[10,30)", "(40,60]", false, null, null];
        yield return [comment, "(10,30)", "(40,60]", false, null, null];
        yield return [comment, "[10,30]", "[40,60)", false, null, null];
        yield return [comment, "(10,30]", "[40,60)", false, null, null];
        yield return [comment, "[10,30)", "[40,60)", false, null, null];
        yield return [comment, "(10,30)", "[40,60)", false, null, null];
        yield return [comment, "[10,30]", "(40,60)", false, null, null];
        yield return [comment, "(10,30]", "(40,60)", false, null, null];
        yield return [comment, "[10,30)", "(40,60)", false, null, null];
        yield return [comment, "(10,30)", "(40,60)", false, null, null];

        comment = "no intersect, null start";
        yield return [comment, "(_,40)", "(50,70)", false, null, null];
        yield return [comment, "(_,40]", "(50,70)", false, null, null];
        yield return [comment, "(_,40)", "[50,70)", false, null, null];
        yield return [comment, "(_,40]", "[50,70)", false, null, null];
        yield return [comment, "(_,40)", "(50,70]", false, null, null];
        yield return [comment, "(_,40]", "(50,70]", false, null, null];
        yield return [comment, "(_,40)", "[50,70]", false, null, null];
        yield return [comment, "(_,40]", "[50,70]", false, null, null];

        comment = "no intersect, null end";
        yield return [comment, "(40,60)", "(80,_)", false, null, null];
        yield return [comment, "[40,60)", "(80,_)", false, null, null];
        yield return [comment, "(40,60]", "(80,_)", false, null, null];
        yield return [comment, "[40,60]", "(80,_)", false, null, null];
        yield return [comment, "(40,60)", "[80,_)", false, null, null];
        yield return [comment, "[40,60)", "[80,_)", false, null, null];
        yield return [comment, "(40,60]", "[80,_)", false, null, null];
        yield return [comment, "[40,60]", "[80,_)", false, null, null];

        comment = "barely no overlap, no nulls";
        yield return [comment, "(10,20)", "(20,30)", false, null, null];
        yield return [comment, "(10,20)", "[20,30)", false, null, null];
        yield return [comment, "(10,20]", "(20,30)", false, null, null];
        yield return [comment, "[10,20)", "(20,30)", false, null, null];
        yield return [comment, "[10,20)", "[20,30)", false, null, null];
        yield return [comment, "[10,20]", "(20,30)", false, null, null];
        yield return [comment, "(10,20)", "(20,30]", false, null, null];
        yield return [comment, "(10,20)", "[20,30]", false, null, null];
        yield return [comment, "(10,20]", "(20,30]", false, null, null];
        yield return [comment, "[10,20)", "(20,30]", false, null, null];
        yield return [comment, "[10,20)", "[20,30]", false, null, null];
        yield return [comment, "[10,20]", "(20,30]", false, null, null];

        comment = "barely no overlap, null start";
        yield return [comment, "(_,10)", "(10,20)", false, null, null];
        yield return [comment, "(_,10]", "(10,20)", false, null, null];
        yield return [comment, "(_,10)", "[10,20)", false, null, null];
        yield return [comment, "(_,10)", "(10,20]", false, null, null];
        yield return [comment, "(_,10]", "(10,20]", false, null, null];
        yield return [comment, "(_,10)", "[10,20]", false, null, null];

        comment = "barely no overlap, null end";
        yield return [comment, "(20,40)", "(40,_)", false, null, null];
        yield return [comment, "(20,40)", "[40,_)", false, null, null];
        yield return [comment, "(20,40]", "(40,_)", false, null, null];
        yield return [comment, "[20,40)", "(40,_)", false, null, null];
        yield return [comment, "[20,40)", "[40,_)", false, null, null];
        yield return [comment, "[20,40]", "(40,_)", false, null, null];
    }

    [Theory]
    [MemberData(nameof(GetRanges))]
    public void Intersect(string comment, string r1, string r2, bool success, string? intersectionStr, string? _)
    {
        // arrange 

        var range1 = ParseRangeString(r1);
        var range2 = ParseRangeString(r2);
        var expectedIntersection = intersectionStr == null ? null : ParseRangeString(intersectionStr);

        // act

        var result = QueryExpression.Range.TryIntersect(range1, range2, out var intersection);
        var resultInverted = QueryExpression.Range.TryIntersect(range2, range1, out var intersectionInverted);

        // assert

        result.Should().Be(success);
        resultInverted.Should().Be(success);
        intersection.Should().BeEquivalentTo(expectedIntersection);
        intersectionInverted.Should().BeEquivalentTo(expectedIntersection);
    }

    [Theory]
    [MemberData(nameof(GetRanges))]
    public void Union(string comment, string r1, string r2, bool success, string? _, string? unionStr)
    {
        // arrange 

        var range1 = ParseRangeString(r1);
        var range2 = ParseRangeString(r2);
        var expectedUnion = unionStr == null ? null : ParseRangeString(unionStr);

        // act

        var result = QueryExpression.Range.TryUnion(range1, range2, out var union);
        var resultInverted = QueryExpression.Range.TryUnion(range2, range1, out var unionInverted);

        // assert

        result.Should().Be(success);
        resultInverted.Should().Be(success);
        union.Should().BeEquivalentTo(expectedUnion);
        unionInverted.Should().BeEquivalentTo(expectedUnion);
    }
}