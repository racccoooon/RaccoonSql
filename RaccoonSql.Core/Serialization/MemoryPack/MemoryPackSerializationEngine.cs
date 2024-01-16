using MemoryPack;

namespace RaccoonSql.Core.Serialization.MemoryPack;

public class MemoryPackSerializationEngine : ISerializationEngine
{
    public void Serialize(Stream stream, object data, Type type)
    {
        var serialize = MemoryPackSerializer.Serialize(type, data);
        stream.Write(serialize, 0, serialize.Length);
    }

    public object Deserialize(Stream stream, Type type)
    {
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        return MemoryPackSerializer.Deserialize(type, ms.ToArray())!;
    }
}