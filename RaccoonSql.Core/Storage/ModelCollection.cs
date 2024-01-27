using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using RaccoonSql.Core.Storage.Persistence;
using RaccoonSql.Core.Utils;

namespace RaccoonSql.Core.Storage;

public class ModelCollection<TModel>
    where TModel : ModelBase
{
    private readonly int _modelsPerChunk;
    private readonly int _rehashThreshold;

    private readonly string _name;
    private readonly ModelStoreOptions _options;

    private readonly FileSystemPersistenceEngine _persistenceEngine;

    private ModelCollectionChunk<TModel>[] _chunks;

    private int _modelCount;

    private Dictionary<string, IIndex> _bTreeIndices = [];
    private Dictionary<string, IIndex> _hashIndices = [];

    private readonly List<ICreateTrigger<TModel>> _createTriggers = new();
    private readonly List<IUpdateTrigger<TModel>> _updateTriggers = new();
    private readonly List<IDeleteTrigger<TModel>> _deleteTriggers = new();

    private readonly Dictionary<string, List<ICheckConstraint>> _checkConstraints;

    private IEnumerable<IIndex> AllIndices => _bTreeIndices.Values.Concat(_hashIndices.Values);

    public ModelCollection(
        string name,
        ModelStoreOptions options,
        FileSystemPersistenceEngine persistenceEngine)
    {
        _name = name;
        _options = options;
        _persistenceEngine = persistenceEngine;

        _modelsPerChunk = options.ModelsPerChunk;
        _rehashThreshold = options.RehashThreshold;

        var modelType = typeof(TModel);

        foreach (var triggerAttribute in modelType.GetCustomAttributes<TriggerAttribute>())
        {
            var triggerType = triggerAttribute.ImplType;
            var trigger = Activator.CreateInstance(triggerType);
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (trigger is ICreateTrigger<TModel> createTrigger)
                _createTriggers.Add(createTrigger);
            if (trigger is IUpdateTrigger<TModel> updateTrigger)
                _updateTriggers.Add(updateTrigger);
            if (trigger is IDeleteTrigger<TModel> deleteTrigger)
                _deleteTriggers.Add(deleteTrigger);
        }

        _checkConstraints = modelType.GetProperties()
            .Select(prop => new
            {
                Prop = prop,
                Attributes = prop.GetCustomAttributes()
                    .Where(a => a.GetType().IsAssignableTo(typeof(CheckConstraintAttribute)))
                    .Cast<CheckConstraintAttribute>()
                    .Select(a => a.CreateConstraint(modelType, prop.PropertyType))
                    .ToList()
            })
            .ToDictionary(x => x.Prop.SetMethod!.Name, x => x.Attributes);


        foreach (var x in modelType.GetProperties()
                     .Select(prop => new { Prop = prop, Attribute = prop.GetCustomAttribute<IndexAttribute>() })
                     .Where(x => x.Attribute != null))
        {
            var indexType = x.Attribute!.Type;
            if (indexType == null)
            {
                // TODO: automatically determine appropriate index type
                indexType = IndexType.BTree;
            }

            switch (indexType)
            {
                case IndexType.BTree:
                    CreateBTreeIndexFromProp(x.Prop);
                    break;
                case IndexType.Hash:
                default:
                    throw new NotImplementedException();
            }
        }

        var chunkCount = _persistenceEngine.GetChunkCount(name);

        _chunks = new ModelCollectionChunk<TModel>[chunkCount];
        for (int i = 0; i < _chunks.Length; i++)
        {
            var chunk = persistenceEngine.LoadChunk<TModel>(name, i, modelType);

            foreach (var model in chunk.Models)
            {
                foreach (var index in AllIndices)
                {
                    index.Insert(model);
                }
            }

            _chunks[i] = chunk;
            _modelCount += chunk.ModelCount;
        }
    }

    public void CreateBTreeIndexFromProp(PropertyInfo prop)
    {
        if (!prop.PropertyType.IsAssignableTo(typeof(IEquatable<>).MakeGenericType(prop.PropertyType)))
        {
            throw new ArgumentException($"index for {prop.Name}: must be IEquatable");
        }

        if (!prop.PropertyType.IsAssignableTo(typeof(IComparable<>).MakeGenericType(prop.PropertyType)))
        {
            throw new ArgumentException($"index for {prop.Name}: must be IComparable");
        }

        var param = Expression.Parameter(typeof(TModel));
        var memberAccess = Expression.MakeMemberAccess(param, prop);
        var lambdaExpression = Expression.Lambda(memberAccess, [param]);

        var createBTreeIndex = GetType().GetMethod(nameof(CreateBTreeIndex))!.MakeGenericMethod(prop.PropertyType);
        createBTreeIndex.Invoke(this, [lambdaExpression]);
    }

    public void CreateBTreeIndex<T>(Expression<Func<TModel, T>> expr)
        where T : IComparable<T>, IEquatable<T>
    {
        var propertyInfo = ExpressionUtils.GetPropertyFromAccessor(expr);

        if (_bTreeIndices.ContainsKey(propertyInfo.Name))
        {
            throw new ArgumentException($"index for property {propertyInfo.Name} already exists");
        }

        var accessor = expr.Compile();

        _bTreeIndices[propertyInfo.Name] = new BTreeIndex<T>(x => accessor((TModel)x), 100); // TODO btree t size
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChunkInfo GetChunkInfo(Guid id)
    {
        unsafe
        {
            var guidBuffer = new GuidBuffer(id);
            var chunkId = (int)(guidBuffer.Uint[3] % _chunks.Length);
            var chunk = _chunks[chunkId];
            return new ChunkInfo
            {
                ChunkId = chunkId,
                Offset = chunk.ModelIndexes[id],
            };
        }
    }

    public StorageInfo GetStorageInfo(Guid id)
    {
        unsafe
        {
            var guidBuffer = new GuidBuffer(id);
            var chunkId = (int)(guidBuffer.Uint[3] % _chunks.Length);
            var chunk = _chunks[chunkId];
            var hasModel = chunk.ModelIndexes.ContainsKey(id);
            ChunkInfo? chunkInfo = hasModel
                ? new ChunkInfo
                {
                    ChunkId = chunkId,
                    Offset = chunk.ModelIndexes[id],
                }
                : null;
            return new StorageInfo
            {
                CollectionName = _name,
                ChunkInfo = chunkInfo,
            };
        }
    }

    public bool ExecuteChecksConstraints(TModel model, bool throwOnFailure)
    {
        IDictionary<string, object?> changes = model.Changes;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (changes is null)
            changes = PocoToDictionary.ToDictionary(model);

        foreach (var (key, value) in changes)
        {
            if (!_checkConstraints.TryGetValue(key, out var constraints))
                continue;

            foreach (var checkConstraint in constraints)
            {
                if (!checkConstraint.Check(model, value))
                {
                    if (throwOnFailure)
                    {
                        throw new Exception(
                            $"Check {checkConstraint.GetType().Name} failed for {key} with value '{value}' on id {model.Id}.");
                    }

                    return false;
                }
            }
        }

        return true;
    }

    public void Update(TModel newModel, Dictionary<string, object?> changes)
    {
        foreach (var trigger in _updateTriggers)
        {
            trigger.OnUpdate(newModel, changes);
        }
        
        var chunkId = CalculateChunkId(newModel.Id);
        var chunk = _chunks[chunkId];
        var modelIndex = chunk.ModelIndexes[newModel.Id];
        var model = chunk.Models[modelIndex];

        foreach (var index in AllIndices)
        {
            index.Update(model, newModel);
        }       
        
        AutoMapper.ApplyChanges(model, changes);
        chunk.UpdateModel(modelIndex, newModel);
    }

    public void Insert(TModel model)
    {
        foreach (var trigger in _createTriggers)
        {
            trigger.OnCreate(model);
        }

        _modelCount++;
        RehashIfNeeded();

        foreach (var index in AllIndices)
        {
            index.Insert(model);
        }

        var chunkId = CalculateChunkId(model.Id);
        var chunk = _chunks[chunkId];

        chunk.InsertModel(model);
    }

    private int CalculateChunkId(Guid id)
    {
        unsafe
        {
            var guidBuffer = new GuidBuffer(id);
            var chunkId = (int)(guidBuffer.Uint[3] % _chunks.Length);
            return chunkId;
        }
    }

    private void RehashIfNeeded()
    {
        if (_modelCount * 100 / (_chunks.Length * _modelsPerChunk) <= _rehashThreshold) return;

        var newChunkCount = _chunks.Length * 2;
        var newChunks = new ModelCollectionChunk<TModel>[newChunkCount];
        for (var i = 0; i < newChunks.Length; i++)
        {
            var chunk = newChunks[i] = new ModelCollectionChunk<TModel>();
            chunk.Init(_name, i, _options, _persistenceEngine);
        }

        var oldChunks = _chunks;

        _chunks = newChunks;

        foreach (var oldChunk in oldChunks)
        {
            foreach (var model in oldChunk.Models)
            {
                var chunkId = CalculateChunkId(model.Id);
                var chunk = _chunks[chunkId];
                chunk.InsertModel(model);
            }
        }
    }

    public TModel Read(Guid id)
    {
        var chunkId = CalculateChunkId(id);
        var chunk = _chunks[chunkId];
        return chunk.Models[chunk.ModelIndexes[id]];
    }

    public void Delete(Guid id)
    {
        var chunkId = CalculateChunkId(id);
        var chunk = _chunks[chunkId];
        var model = chunk.Models[chunk.ModelIndexes[id]];

        foreach (var trigger in _deleteTriggers)
        {
            trigger.OnDelete(model);
        }

        foreach (var index in AllIndices)
        {
            index.Remove(model);
        }

        chunk.DeleteModel(id);
    }

    public IEnumerable<Row<TModel>> GetAllRows()
    {
        // ReSharper disable once LoopCanBeConvertedToQuery
        for (int chunkId = 0; chunkId < _chunks.Length; chunkId++)
        {
            var chunk = _chunks[chunkId];
            for (int modelOffset = 0; modelOffset < chunk.Models.Count; modelOffset++)
            {
                var model = chunk.Models[modelOffset];

                yield return new Row<TModel>()
                {
                    Model = model,
                    ChunkInfo = new ChunkInfo
                    {
                        ChunkId = chunkId,
                        Offset = modelOffset,
                    }
                };
            }
        }
    }

    public IIndex GetIndex(string name)
    {
        if (name.StartsWith("btree:"))
        {
            return _bTreeIndices[name["btree:".Length..]];
        }

        if (name.StartsWith("hash:"))
        {
            return _hashIndices[name["btree:".Length..]];
        }

        throw new ArgumentOutOfRangeException(nameof(name), $"unsupported index type: {name}");
    }
}

public readonly struct Row<TModel> where TModel : ModelBase
{
    public ChunkInfo ChunkInfo { get; init; }
    public TModel Model { get; init; }
}

public interface IIndex
{
    public IEnumerable<ModelBase> Scan(object from, object to, bool fromSet, bool toSet, bool fromInclusive,
        bool toInclusive, bool backwards);

    void Insert(ModelBase model);
    void Update(ModelBase oldModelBase, ModelBase model);
    void Remove(ModelBase model);
}

public class BTreeIndex<T> : IIndex
    where T : IComparable<T>, IEquatable<T>
{
    private readonly Func<ModelBase, T> _accessor;
    private readonly BPlusTree<T, ModelBase> _tree;

    public BTreeIndex(Func<ModelBase, T> accessor, int t)
    {
        _accessor = accessor;
        _tree = new BPlusTree<T, ModelBase>(t);
    }

    public IEnumerable<ModelBase> Scan(object from, object to, bool fromSet, bool toSet, bool fromInclusive,
        bool toInclusive, bool backwards)
    {
        // ReSharper disable twice NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        return _tree.FunkyRange((T)from ?? default!, (T)to ?? default!, fromSet, toSet, !fromInclusive, !toInclusive,
            backwards);
    }

    public void Insert(ModelBase model)
    {
        _tree.Insert(_accessor(model), model);
    }

    public void Update(ModelBase oldModelBase, ModelBase model)
    {
        if (_accessor(oldModelBase).Equals(_accessor(model))) return;

        Remove(oldModelBase);
        Insert(model);
    }

    public void Remove(ModelBase model)
    {
        _tree.Remove(_accessor(model), model);
    }
}