using FluentAssertions;
using RaccoonSql.CoreRework.Internal.Utils;

namespace RaccoonSql.CoreRework.Test.Internal.Utils;

public class StreamUtilsTest
{
    public static IEnumerable<object[]> GetArrays()
    {
        yield return [Array.Empty<byte>()];
        yield return [new byte[]{1}];
        yield return [new byte[]{1, 1, 1, 1}];
        yield return [new byte[]{1, 2, 3, 4, 5}];
        yield return [new byte[]{255, 2, 1, 4, 0}];
    }
    
    [Theory]
    [MemberData(nameof(GetArrays))]
    public void ToArray_(byte[] data)
    {
        // arrange
        Stream stream = new MemoryStream(data);
        
        // act
        var result = stream.ToArray();
        
        // assert
        result.Should().BeEquivalentTo(data);
    }
}