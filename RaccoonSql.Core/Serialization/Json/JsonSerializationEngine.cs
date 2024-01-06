using Newtonsoft.Json;

namespace RaccoonSql.Core.Serialization.Json;

internal class JsonSerializationEngine : ISerializationEngine
{
    public Stream Serialize<TData>(TData data)
    {
        var serializer = new JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.Auto
        };
        var stream = new MemoryStream();
        var writer = new JsonTextWriter(new StreamWriter(stream));
        serializer.Serialize(writer, data);
        writer.Flush();
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    public TData Deserialize<TData>(Stream stream)
    {
        var serializer = new JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.Auto
        };
        var reader = new JsonTextReader(new StreamReader(stream));
        return serializer.Deserialize<TData>(reader)!;
    }
}