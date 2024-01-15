using System.Text.Json;

namespace RaccoonSql.Core.Serialization.Json;

internal class JsonSerializationEngine : ISerializationEngine
{
    public void Serialize(Stream stream, object data, Type type)
    {
        var utf8JsonWriter = new Utf8JsonWriter(stream);
        JsonSerializer.Serialize(utf8JsonWriter, data);
        utf8JsonWriter.Flush();
    }

    public object Deserialize(Stream stream, Type type)
    {
        return JsonSerializer.Deserialize(stream, type)!;
    }
}