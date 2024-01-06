using MemoryPack;
using RaccoonSql.Core;
using RaccoonSql.Core.Serialization;

namespace RaccoonSql.Serialization.MemoryPack;

public class MemoryPackSerializationEngine : ISerializationEngine
{
    public Stream Serialize<TData>(TData data)
    {
        var bytes = MemoryPackSerializer.Serialize(typeof(TData), data);
        return new MemoryStream(bytes);
    }

    public TData Deserialize<TData>(Stream stream)
    {
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        return MemoryPackSerializer.Deserialize<TData>(ms.ToArray())!;
    }
}