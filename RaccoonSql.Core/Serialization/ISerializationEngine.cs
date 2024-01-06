using JetBrains.Annotations;

namespace RaccoonSql.Core.Serialization;

[PublicAPI]
public interface ISerializationEngine
{
    Stream Serialize<TData>(TData data) 
        where TData : IModel;
    
    Task<Stream> SerializeAsync<TData>(TData data, CancellationToken cancellationToken = default) 
        where TData : IModel
        => Task.FromResult(Serialize(data));
    
    TData Deserialize<TData>(Stream stream) 
        where TData : IModel;

    Task<TData> DeserializeAsync<TData>(Stream stream, CancellationToken cancellationToken = default)
        where TData : IModel
        => Task.FromResult(Deserialize<TData>(stream));
}