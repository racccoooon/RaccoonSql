using FluentAssertions;
using RaccoonSql.CoreRework.Serializer;

namespace RaccoonSql.CoreRework.Test.Serializer;

public class ValueSerializerTest
{
    public enum CuteAnimal
    {
        Raccoon, Frog, Shark, Fox, 
    }
    
    struct Demo(Guid guid)
    {
        public required int Foo;
        public required int Bar;
        public required float FooBar;
        private Guid _guid = guid;
    }
    
    [Theory]
    [InlineData(CuteAnimal.Raccoon)]
    [InlineData(CuteAnimal.Fox)]
    [InlineData(CuteAnimal.Frog)]
    [InlineData(CuteAnimal.Shark)]

    public void SerializeEnum(CuteAnimal animal)
    {
        // arrange
        var serializer = RaccSerializer.GetValueSerializer<CuteAnimal>();
        var ms = new MemoryStream();
        
        // act
        serializer.Serialize(ms, animal);
        
        // assert
        ms.ToArray().Should().BeEquivalentTo([
            ..BitConverter.GetBytes((int)animal),
        ]);
    }

    [Theory]
    [InlineData(10, 27, 23.45f)]
    public void SerializeStruct(int foo, int bar, float foobar)
    {
        // arrange
        var guid = new Guid();
        var demo = new Demo(guid)
        {
            Foo = foo,
            Bar = bar,
            FooBar = foobar,
        };
        var serializer = RaccSerializer.GetValueSerializer<Demo>();
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
    [InlineData(CuteAnimal.Raccoon)]
    [InlineData(CuteAnimal.Fox)]
    [InlineData(CuteAnimal.Frog)]
    [InlineData(CuteAnimal.Shark)]

    public void DeserializeEnum(CuteAnimal animal)
    {
        // arrange
        var serializer = RaccSerializer.GetValueSerializer<CuteAnimal>();
        var ms = new MemoryStream([
            ..BitConverter.GetBytes((int)animal),
        ]);
        
        // act
        var result = serializer.Deserialize(ms);
        
        // assert
        result.Should().Be(animal);
    }

    [Theory]
    [InlineData(10, 27, 23.45f)]
    public void DeserializeStruct(int foo, int bar, float foobar)
    {
        // arrange
        var guid = new Guid();
        var demo = new Demo(guid)
        {
            Foo = foo,
            Bar = bar,
            FooBar = foobar,
        };
        var serializer = RaccSerializer.GetValueSerializer<Demo>();
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
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SerializeBool(bool b)
    {
        // arrange
        var serializer = RaccSerializer.GetValueSerializer<bool>();
        var ms = new MemoryStream();
        
        // act
        serializer.Serialize(ms, b);
        
        // assert
        var bytes = ms.ToArray();
        bytes.Should().BeEquivalentTo(BitConverter.GetBytes(b));
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(0x255)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void SerializeInt(int i)
    {
        // arrange
        var serializer = RaccSerializer.GetValueSerializer<int>();
        var ms = new MemoryStream();
        
        // act
        serializer.Serialize(ms, i);
        
        // assert
        var bytes = ms.ToArray();
        bytes.Should().BeEquivalentTo(BitConverter.GetBytes(i));
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(0x255)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void SerializeLong(long l)
    {
        // arrange
        var serializer = RaccSerializer.GetValueSerializer<long>();
        var ms = new MemoryStream();
        
        // act
        serializer.Serialize(ms, l);
        
        // assert
        var bytes = ms.ToArray();
        bytes.Should().BeEquivalentTo(BitConverter.GetBytes(l));
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(0x255)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void DeserializeInt(int i)
    {
        // arrange
        var serializer = RaccSerializer.GetValueSerializer<int>();
        var ms = new MemoryStream(BitConverter.GetBytes(i));
        
        // act
        var result = serializer.Deserialize(ms);
        
        // assert
        result.Should().Be(i);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(0x255)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void DeserializeLong(long l)
    {
        // arrange
        var serializer = RaccSerializer.GetValueSerializer<long>();
        var ms = new MemoryStream(BitConverter.GetBytes(l));
        
        // act
        var result = serializer.Deserialize(ms);
        
        // assert
        result.Should().Be(l);
    }
    
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DeserializeBool(bool b)
    {
        // arrange
        var serializer = RaccSerializer.GetValueSerializer<bool>();
        var ms = new MemoryStream(BitConverter.GetBytes(b));
        
        // act
        var result = serializer.Deserialize(ms);
        
        // assert
        result.Should().Be(b);
    }
}