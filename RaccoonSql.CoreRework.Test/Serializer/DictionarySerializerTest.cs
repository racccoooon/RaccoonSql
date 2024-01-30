using FluentAssertions;
using RaccoonSql.CoreRework.Serializer;

namespace RaccoonSql.CoreRework.Test.Serializer;

public class DictionarySerializerTest
{
    [Fact]
    public void SerializeAndDeserialize()
    {
        // arrange
        var dict = new Dictionary<string, int>
        {
            { "Geld", 0 },
            { "Cuteness", 100 },
            { "Coolness", 9001 },
        };
        var serializer = new DictionarySerializer<string, int>();
        var ms = new MemoryStream();

        // act
        serializer.Serialize(ms, dict);
        ms.Seek(0, SeekOrigin.Begin);
        var dict2 = serializer.Deserialize(ms);
        
        // assert
        dict2.Should().BeEquivalentTo(dict);
    }
}