using FluentAssertions;
using RaccoonSql.CoreRework.Internal.Utils;

namespace RaccoonSql.CoreRework.Test.Internal.Utils;

public class AutoMapperTest
{
    [Fact]
    public void Clone()
    {
        // arrange
        var source = new AutoMapperTestTarget
        {
            Integer = 127,
        };

        // act
        var clone = AutoMapper.Clone(source);

        // assert
        clone.Should().NotBeNull();
        clone.Should().BeEquivalentTo(source);
    }

    [Fact]
    public void Map()
    {
        // arrange
        var source = new AutoMapperTestTarget
        {
            Integer = 69,
            String = "nice",
        };

        var target = new AutoMapperTestTarget
        {
            Integer = 420,
        };

        // act
        AutoMapper.Map(source, target);

        // assert
        target.Should().BeEquivalentTo(source);
    }

    public static IEnumerable<object[]> GetChanges()
    {
        yield return
        [
            new Dictionary<string, object?>(),
            new AutoMapperTestTarget { Integer = 13, String = "original" },
        ];
        yield return
        [
            new Dictionary<string, object?> { { nameof(AutoMapperTestTarget.String), "changed" } },
            new AutoMapperTestTarget { Integer = 13, String = "changed" },
        ];
        yield return
        [
            new Dictionary<string, object?> { { nameof(AutoMapperTestTarget.String), null } },
            new AutoMapperTestTarget { Integer = 13, String = null },
        ];
        yield return
        [
            new Dictionary<string, object?> { { nameof(AutoMapperTestTarget.Integer), 27 } },
            new AutoMapperTestTarget { Integer = 27, String = "original" },
        ];
    }

    [Theory]
    [MemberData(nameof(GetChanges))]
    public void ApplyChanges(Dictionary<string, object?> changes, AutoMapperTestTarget expected)
    {
        // arange
        var target = new AutoMapperTestTarget
        {
            Integer = 13,
            String = "original",
        };

        // act
        AutoMapper.ApplyChanges(target, changes);

        // assert
        target.Should().BeEquivalentTo(expected);
    }

    public class AutoMapperTestTarget
    {
        public required int Integer { get; init; }
        public string? String { get; set; }
    }
}