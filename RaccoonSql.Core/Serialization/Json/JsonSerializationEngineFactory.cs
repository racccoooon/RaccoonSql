namespace RaccoonSql.Core.Serialization.Json;

public class JsonSerializationEngineFactory : ISerializationEngineFactory
{
    public ISerializationEngine Create()
    {
        return new JsonSerializationEngine();
    }
}