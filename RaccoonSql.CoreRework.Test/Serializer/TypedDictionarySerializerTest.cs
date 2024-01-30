using System.Reflection;
using FluentAssertions;
using RaccoonSql.CoreRework.Serializer;

namespace RaccoonSql.CoreRework.Test.Serializer;

public class TypedDictionarySerializerTest
{
    public class Demo
    {
        public required int Foo { get; set; }
        public required string Bar { get; set; }
        public required bool True { get; set; } = true;
    }
    
    [Fact]
    public void SerializeAndDeserialize()
    {
        // arrange
        var dict = new Dictionary<PropertyInfo, object>
        {
            {typeof(Demo).GetProperty(nameof(Demo.Foo))!, 127},
            {typeof(Demo).GetProperty(nameof(Demo.Bar))!, "Text!"},
        };
        var ms = new MemoryStream();
        var serializer = RaccSerializer.GetTypedDictionarySerializer<Demo>();

        // act
        serializer.Serialize(ms, dict);
        ms.Seek(0, SeekOrigin.Begin);
        var dict2 = serializer.Deserialize(ms);
        
        // assert
        dict2.Should().BeEquivalentTo(dict);
    }
}