using System.IO.Abstractions;
using RaccoonSql.Core.Serialization.Json;
using RaccoonSql.Core.Storage;
using RaccoonSql.Core.Storage.Persistence;

namespace RaccoonSql.Core;

public class ModelStore(
    ModelStoreOptions options)
{
    private FileSystemPersistenceEngine PersistenceEngine { get; } = new(
        new FileSystem(),
        options,
        new JsonSerializationEngine());
    
    private readonly Dictionary<string, object> _modelSets = new();

    public ModelSet<TModel> Set<TModel>(string? setName = null) where TModel : ModelBase
    {
        var name = typeof(TModel).FullName! + "$" + (setName ?? "");
        
        if (!_modelSets.TryGetValue(name, out var set))
        {
            set = new ModelSet<TModel>(new ModelCollection<TModel>(
                name, 
                options, 
                PersistenceEngine), options);
            _modelSets[name] = set;
        }
        
        return (ModelSet<TModel>)set;
    }
}

