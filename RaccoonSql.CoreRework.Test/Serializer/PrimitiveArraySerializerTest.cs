using FluentAssertions;
using RaccoonSql.CoreRework.Serializer;

namespace RaccoonSql.CoreRework.Test.Serializer;

public class PrimitiveArraySerializerTest
{
    [Fact]
    public void SerializeBoolArray()
    {
        // arrange 
        var serializer = new PrimitiveArraySerializer<bool>();
        var ms = new MemoryStream();
        
        // act
        serializer.Serialize(ms, [true, false]);
        
        // assert
        var bytes = ms.ToArray();
        byte[] expected = [
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
        var serializer = new PrimitiveArraySerializer<int>();
        var ms = new MemoryStream();
        
        // act
        serializer.Serialize(ms, [0, 1, -1, 255, int.MaxValue, int.MinValue]);
        
        // assert
        var bytes = ms.ToArray();
        byte[] expected = [
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
        var serializer = new PrimitiveArraySerializer<long>();
        var ms = new MemoryStream();
        
        // act
        serializer.Serialize(ms, [0, 1, -1, 255, long.MaxValue, long.MinValue]);
        
        // assert
        var bytes = ms.ToArray();
        byte[] expected = [
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
    public void DeserializeBoolArray()
    {
        // arrange 
        var serializer = new PrimitiveArraySerializer<bool>();
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
        var serializer = new PrimitiveArraySerializer<int>();
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
        var serializer = new PrimitiveArraySerializer<long>();
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