using FluentAssertions;
using RaccoonSql.CoreRework.Serializer;

namespace RaccoonSql.CoreRework.Test.Serializer;

public class ValueArraySerializerTest
{
    struct Demo(Guid guid)
    {
        public required int Foo;
        public required int Bar;
        public required float FooBar;
        private Guid _guid = guid;
    }

    [Fact]
    public void SerializeStructArray()
    {
        // arrange 
        var serializer = RaccSerializer.GetValueArraySerializer<Demo>();
        var ms = new MemoryStream();
        var guid1 = new Guid();
        var guid2 = new Guid();
        var guid3 = new Guid();
        Guid[] guids = [guid1, guid2, guid3];
        Demo[] array =
        [
            new Demo(guid1) { Foo = 1, Bar = 2, FooBar = 3 },
            new Demo(guid2) { Foo = 3, Bar = 20, FooBar = 12 },
            new Demo(guid3) { Foo = 42, Bar = 69, FooBar = 123 },
        ];

        // act
        serializer.Serialize(ms, array);

        // assert
        var bytes = ms.ToArray();
        byte[] expected =
        [
            ..BitConverter.GetBytes(3),
            ..array.Zip(guids).SelectMany<(Demo, Guid), byte>(x =>
            [
                ..BitConverter.GetBytes(x.Item1.Foo),
                ..BitConverter.GetBytes(x.Item1.Bar),
                ..BitConverter.GetBytes(x.Item1.FooBar),
                ..x.Item2.ToByteArray(),
            ]),
        ];
        bytes.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void SerializeBoolArray()
    {
        // arrange 
        var serializer = RaccSerializer.GetValueArraySerializer<bool>();
        var ms = new MemoryStream();

        // act
        serializer.Serialize(ms, [true, false]);

        // assert
        var bytes = ms.ToArray();
        byte[] expected =
        [
            ..BitConverter.GetBytes(2),
            ..BitConverter.GetBytes(true),
            ..BitConverter.GetBytes(false),
        ];
        bytes.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void SerializeIntArray()
    {
        // arrange 
        var serializer = RaccSerializer.GetValueArraySerializer<int>();
        var ms = new MemoryStream();

        // act
        serializer.Serialize(ms, [0, 1, -1, 255, int.MaxValue, int.MinValue]);

        // assert
        var bytes = ms.ToArray();
        byte[] expected =
        [
            ..BitConverter.GetBytes(6),
            ..BitConverter.GetBytes(0),
            ..BitConverter.GetBytes(1),
            ..BitConverter.GetBytes(-1),
            ..BitConverter.GetBytes(255),
            ..BitConverter.GetBytes(int.MaxValue),
            ..BitConverter.GetBytes(int.MinValue),
        ];
        bytes.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void SerializeLongArray()
    {
        // arrange 
        var serializer = RaccSerializer.GetValueArraySerializer<long>();
        var ms = new MemoryStream();

        // act
        serializer.Serialize(ms, [0, 1, -1, 255, long.MaxValue, long.MinValue]);

        // assert
        var bytes = ms.ToArray();
        byte[] expected =
        [
            ..BitConverter.GetBytes(6),
            ..BitConverter.GetBytes((long)0),
            ..BitConverter.GetBytes((long)1),
            ..BitConverter.GetBytes((long)-1),
            ..BitConverter.GetBytes((long)255),
            ..BitConverter.GetBytes(long.MaxValue),
            ..BitConverter.GetBytes(long.MinValue),
        ];
        bytes.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void DeserializeStructArray()
    {
        // arrange 
        var serializer = RaccSerializer.GetValueArraySerializer<Demo>();
        var guid1 = new Guid();
        var guid2 = new Guid();
        var guid3 = new Guid();
        Guid[] guids = [guid1, guid2, guid3];
        Demo[] array =
        [
            new Demo(guid1) { Foo = 1, Bar = 2, FooBar = 3 },
            new Demo(guid2) { Foo = 3, Bar = 20, FooBar = 12 },
            new Demo(guid3) { Foo = 42, Bar = 69, FooBar = 123 },
        ];
        var ms = new MemoryStream([
            ..BitConverter.GetBytes(3),
            ..array.Zip(guids).SelectMany<(Demo, Guid), byte>(x =>
            [
                ..BitConverter.GetBytes(x.Item1.Foo),
                ..BitConverter.GetBytes(x.Item1.Bar),
                ..BitConverter.GetBytes(x.Item1.FooBar),
                ..x.Item2.ToByteArray(),
            ]),
        ]);

        // act
        var result = serializer.Deserialize(ms);

        // assert
        result.Should().BeEquivalentTo(array);
    }

    [Fact]
    public void DeserializeBoolArray()
    {
        // arrange 
        var serializer = RaccSerializer.GetValueArraySerializer<bool>();
        var ms = new MemoryStream([
            ..BitConverter.GetBytes(2),
            ..BitConverter.GetBytes(true),
            ..BitConverter.GetBytes(false),
        ]);

        // act
        var result = serializer.Deserialize(ms);

        // assert
        result.Should().BeEquivalentTo([true, false]);
    }

    [Fact]
    public void DeserializeIntArray()
    {
        // arrange 
        var serializer = RaccSerializer.GetValueArraySerializer<int>();
        var ms = new MemoryStream([
            ..BitConverter.GetBytes(6),
            ..BitConverter.GetBytes(0),
            ..BitConverter.GetBytes(1),
            ..BitConverter.GetBytes(-1),
            ..BitConverter.GetBytes(255),
            ..BitConverter.GetBytes(int.MaxValue),
            ..BitConverter.GetBytes(int.MinValue),
        ]);

        // act
        var result = serializer.Deserialize(ms);

        // assert
        result.Should().BeEquivalentTo([0, 1, -1, 255, int.MaxValue, int.MinValue]);
    }

    [Fact]
    public void DeserializeLongArray()
    {
        // arrange 
        var serializer = RaccSerializer.GetValueArraySerializer<long>();
        var ms = new MemoryStream([
            ..BitConverter.GetBytes(6),
            ..BitConverter.GetBytes((long)0),
            ..BitConverter.GetBytes((long)1),
            ..BitConverter.GetBytes((long)-1),
            ..BitConverter.GetBytes((long)255),
            ..BitConverter.GetBytes(long.MaxValue),
            ..BitConverter.GetBytes(long.MinValue),
        ]);

        // act
        var result = serializer.Deserialize(ms);

        // assert
        result.Should().BeEquivalentTo([0, 1, -1, 255, long.MaxValue, long.MinValue]);
    }
}