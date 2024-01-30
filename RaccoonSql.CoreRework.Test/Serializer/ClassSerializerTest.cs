using FluentAssertions;
using RaccoonSql.CoreRework.Serializer;

namespace RaccoonSql.CoreRework.Test.Serializer;

public class ClassSerializerTest
{
    public class Demo
    {
        public required string Foo { get; set; }
        public required int Bar { get; set; }
        public required byte[] Image { get; set; }
    }

    [Theory]
    [InlineData("Hello", 27, new byte[] { 0, 1, 2, 3, 4 })]
    public void Serialize(string foo, int bar, byte[] image)
    {
        // arrange
        var demo = new Demo
        {
            Foo = foo,
            Bar = bar,
            Image = image,
        };
        var serializer = new ClassSerializer<Demo>();
        var ms = new MemoryStream();

        // act
        serializer.Serialize(ms, demo);

        // assert
        var stringBytes = foo
            .Select(BitConverter.GetBytes)
            .SelectMany(x => x)
            .ToArray();
        ms.ToArray().Should().BeEquivalentTo([
            ..BitConverter.GetBytes(stringBytes.Length),
            ..stringBytes,
            ..BitConverter.GetBytes(bar),
            ..BitConverter.GetBytes(image.Length),
            ..image,
        ]);
    }

    [Theory]
    [InlineData("Hello", 27, new byte[] { 0, 1, 2, 3, 4 })]
    public void Deserialize(string foo, int bar, byte[] image)
    {
        // arrange
        var demo = new Demo
        {
            Foo = foo,
            Bar = bar,
            Image = image,
        };
        var serializer = new ClassSerializer<Demo>();
        var stringBytes = foo
            .Select(BitConverter.GetBytes)
            .SelectMany(x => x)
            .ToArray();
        var ms = new MemoryStream([
            ..BitConverter.GetBytes(stringBytes.Length),
            ..stringBytes,
            ..BitConverter.GetBytes(bar),
            ..BitConverter.GetBytes(image.Length),
            ..image,
        ]);

        // act
        var result = serializer.Deserialize(ms);

        // assert
        result.Should().BeEquivalentTo(demo);
    }
}