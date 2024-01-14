using Newtonsoft.Json;

namespace RaccoonSql.Core.Serialization.Json;

internal class JsonSerializationEngine : ISerializationEngine
{
    public Stream Serialize(object data)
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

    public object Deserialize(Stream stream, Type type)
    {
        var serializer = new JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.Auto
        };
        var reader = new JsonTextReader(new StreamReader(stream));
        return serializer.Deserialize(reader, type)!;
    }
}