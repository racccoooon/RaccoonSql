using FluentAssertions;
using RaccoonSql.CoreRework.Serializer;

namespace RaccoonSql.CoreRework.Test.Serializer;

public class ListSerializerTest
{
    
    [Fact]
    public void SerializeAndDeserializeStrings()
    {
        // arrange
        List<string> list = ["Geld", "Cuteness", "Coolness"];
        var serializer = new ListSerializer<string>();
        var ms = new MemoryStream();

        // act
        serializer.Serialize(ms, list);
        ms.Seek(0, SeekOrigin.Begin);
        var list2 = serializer.Deserialize(ms);
        
        // assert
        list2.Should().BeEquivalentTo(list);
    }
    
    [Fact]
    public void SerializeAndDeserializeInts()
    {
        // arrange
        List<int> list = [2, 3, 5];
        var serializer = new ListSerializer<int>();
        var ms = new MemoryStream();

        // act
        serializer.Serialize(ms, list);
        ms.Seek(0, SeekOrigin.Begin);
        var list2 = serializer.Deserialize(ms);
        
        // assert
        list2.Should().BeEquivalentTo(list);
    }

    class Raccoon
    {
        public string Name { get; set; }
        public int Cuteness { get; set; }
        public float Floofiness { get; set; }
    }
    
    [Fact]
    public void SerializeAndDeserializeObjects()
    {
        // arrange
        List<Raccoon> list = [
            new Raccoon {Name = "Herbert", Cuteness=75, Floofiness = 1000},
            new Raccoon {Name = "Gertlinde", Cuteness=83, Floofiness = 2500},
            new Raccoon {Name = "Franziskus", Cuteness=66, Floofiness = 50},
        ];
        var serializer = new ListSerializer<Raccoon>();
        var ms = new MemoryStream();

        // act
        serializer.Serialize(ms, list);
        ms.Seek(0, SeekOrigin.Begin);
        var list2 = serializer.Deserialize(ms);
        
        // assert
        list2.Should().BeEquivalentTo(list);
    }
}