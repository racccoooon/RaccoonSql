using FluentAssertions;
using RaccoonSql.CoreRework.Internal.Utils;

namespace RaccoonSql.CoreRework.Test.Internal.Utils;

public class GuidExtensionsTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1238559)]
    [InlineData(4213)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(0xFFFFFFFF)]
    [InlineData(0x12345678)]
    public void GetUint3(uint expected)
    {
        // arrange
        var bytes = BitConverter.GetBytes(expected);
        var guid = new Guid(0, 0, 0, 0, 0, 0, 0, bytes[0], bytes[1], bytes[2], bytes[3]);
        
        // act
        var result = guid.GetUint3();
        
        // assert
        result.Should().Be(expected);
    }
}