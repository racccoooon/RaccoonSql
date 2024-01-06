using RaccoonSql.Core.Serialization;

namespace RaccoonSql.Serialization.MemoryPack;

public class MemoryPackSerializationEngineFactory
: ISerializationEngineFactory
{
    public ISerializationEngine Create()
    {
        return new MemoryPackSerializationEngine();
    }
}