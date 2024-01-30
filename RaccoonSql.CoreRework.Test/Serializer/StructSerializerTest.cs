using FluentAssertions;
using RaccoonSql.CoreRework.Serializer;

namespace RaccoonSql.CoreRework.Test.Serializer;

public class StructSerializerTest
{
    struct Demo(Guid guid)
    {
        public required int Foo;
        public required int Bar;
        public required float FooBar;
        private Guid _guid = guid;
    }

    [Theory]
    [InlineData(10, 27, 23.45f)]
    public void Serialize(int foo, int bar, float foobar)
    {
        // arrange
        var guid = new Guid();
        var demo = new Demo(guid)
        {
            Foo = foo,
            Bar = bar,
            FooBar = foobar,
        };
        var serializer = new ValueStructSerializer<Demo>();
        var ms = new MemoryStream();

        // act
        serializer.Serialize(ms, demo);

        // assert
        ms.ToArray().Should().BeEquivalentTo([
            ..BitConverter.GetBytes(foo),
            ..BitConverter.GetBytes(bar),
            ..BitConverter.GetBytes(foobar),
            ..guid.ToByteArray(),
        ]);
    }

    [Theory]
    [InlineData(10, 27, 23.45f)]
    public void Deserialize(int foo, int bar, float foobar)
    {
        // arrange
        var guid = new Guid();
        var demo = new Demo(guid)
        {
            Foo = foo,
            Bar = bar,
            FooBar = foobar,
        };
        var serializer = new ValueStructSerializer<Demo>();
        var ms = new MemoryStream([
            ..BitConverter.GetBytes(foo),
            ..BitConverter.GetBytes(bar),
            ..BitConverter.GetBytes(foobar),
            ..guid.ToByteArray(),
        ]);

        // act
        var result = serializer.Deserialize(ms);

        // assert
        result.Should().BeEquivalentTo(demo);
    }
}