using FluentAssertions;
using RaccoonSql.CoreRework.Serializer;

namespace RaccoonSql.CoreRework.Test.Serializer;

public class HashSetSerializerTest
{
    
    [Fact]
    public void SerializeAndDeserializeStrings()
    {
        // arrange
        HashSet<string> hashSet = ["Geld", "Cuteness", "Coolness"];
        var serializer = new HashSetSerializer<string>();
        var ms = new MemoryStream();

        // act
        serializer.Serialize(ms, hashSet);
        ms.Seek(0, SeekOrigin.Begin);
        var hashSet2 = serializer.Deserialize(ms);
        
        // assert
        hashSet2.Should().BeEquivalentTo(hashSet);
    }
    
    [Fact]
    public void SerializeAndDeserializeInts()
    {
        // arrange
        HashSet<int> hashSet = [2, 3, 5];
        var serializer = new HashSetSerializer<int>();
        var ms = new MemoryStream();

        // act
        serializer.Serialize(ms, hashSet);
        ms.Seek(0, SeekOrigin.Begin);
        var hashSet2 = serializer.Deserialize(ms);
        
        // assert
        hashSet2.Should().BeEquivalentTo(hashSet);
    }

    record Raccoon
    {
        public string Name { get; set; }
        public int Cuteness { get; set; }
        public float Floofiness { get; set; }
    }
    
    [Fact]
    public void SerializeAndDeserializeObjects()
    {
        // arrange
        HashSet<Raccoon> hashSet = [
            new Raccoon {Name = "Herbert", Cuteness=75, Floofiness = 1000},
            new Raccoon {Name = "Gertlinde", Cuteness=83, Floofiness = 2500},
            new Raccoon {Name = "Franziskus", Cuteness=66, Floofiness = 50},
        ];
        var serializer = new HashSetSerializer<Raccoon>();
        var ms = new MemoryStream();

        // act
        serializer.Serialize(ms, hashSet);
        ms.Seek(0, SeekOrigin.Begin);
        var hashSet2 = serializer.Deserialize(ms);
        
        // assert
        hashSet2.Should().BeEquivalentTo(hashSet);
    }
}