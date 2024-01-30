using FluentAssertions;
using RaccoonSql.CoreRework.Serializer;

namespace RaccoonSql.CoreRework.Test.Serializer;

public class StringSerializerTest
{
    [Theory]
    [InlineData("")]
    [InlineData("Raccoon")]
    [InlineData("!!!")]
    public void Deserialize(string s)
    {
        // arrange
        var serializer = new StringSerializer();
        var bytes = s
            .Select(BitConverter.GetBytes)
            .SelectMany(x => x)
            .ToArray();
        var ms = new MemoryStream([
            ..BitConverter.GetBytes(bytes.Length),
            ..bytes,
        ]);

        // act
        var result = serializer.Deserialize(ms);

        // assert
        result.Should().Be(s);
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("Raccoon")]
    [InlineData("!!!")]
    public void Serialize(string s)
    {
        // arrange
        var serializer = new StringSerializer();
        var ms = new MemoryStream();

        // act
        serializer.Serialize(ms, s);

        // assert
        var bytes = s
            .Select(BitConverter.GetBytes)
            .SelectMany(x => x)
            .ToArray();
        ms.ToArray().Should().BeEquivalentTo([
            ..BitConverter.GetBytes(bytes.Length),
            ..bytes,
        ]);
    }
}