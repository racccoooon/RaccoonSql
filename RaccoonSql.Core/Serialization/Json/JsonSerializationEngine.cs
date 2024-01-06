using System.Text.Json;

namespace RaccoonSql.Core.Serialization.Json;

internal class JsonSerializationEngine : ISerializationEngine
{
    public Stream Serialize<TData>(TData data)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, data, data.GetType());
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    public TData Deserialize<TData>(Stream stream)
    {
        return JsonSerializer.Deserialize<TData>(stream)!;
    }
}