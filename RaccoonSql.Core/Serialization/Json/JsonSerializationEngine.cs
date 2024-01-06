using System.Text.Json;

namespace RaccoonSql.Core.Serialization.Json;

internal class JsonSerializationEngine : ISerializationEngine
{
    public Stream Serialize<TData>(TData data) where TData : IModel
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, data);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    public TData Deserialize<TData>(Stream stream) where TData : IModel
    {
        return JsonSerializer.Deserialize<TData>(stream)!;
    }
}