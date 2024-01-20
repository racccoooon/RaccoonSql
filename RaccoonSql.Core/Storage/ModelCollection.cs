using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using RaccoonSql.Core.Storage.Persistence;
using RaccoonSql.Core.Utils;
using RaccoonSql.Demo.Models;

namespace RaccoonSql.Core.Storage;

public class ModelCollection<TModel>
    where TModel : ModelBase
{
    private const int ModelsPerChunk = 512;
    private const int RehashThreshold = 66;

    private readonly string _name;
    
    private readonly IPersistenceEngine _persistenceEngine;
    
    private ModelCollectionChunk<TModel>[] _chunks;
    
    private uint _modelCount;
    
    private Dictionary<string, IIndex> _bTreeIndices = [];
    private Dictionary<string, IIndex> _hashIndices = [];

    private readonly List<ICreateTrigger<TModel>> _createTriggers = new();
    private readonly List<IUpdateTrigger<TModel>> _updateTriggers = new();
    private readonly List<IDeleteTrigger<TModel>> _deleteTriggers = new();

    private readonly Dictionary<string, List<ICheckConstraint>> _checkConstraints;

    private IEnumerable<IIndex> AllIndices => _bTreeIndices.Values.Concat(_hashIndices.Values);

    public ModelCollection(string name, IPersistenceEngine persistenceEngine)
    {
        _name = name;
        _persistenceEngine = persistenceEngine;

        var modelType = typeof(TModel);
        
        foreach (var triggerAttribute in modelType.GetCustomAttributes<TriggerAttribute>())
        {
            var triggerType = triggerAttribute.ImplType;
            var trigger = Activator.CreateInstance(triggerType);
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if(trigger is ICreateTrigger<TModel> createTrigger)
                _createTriggers.Add(createTrigger);
            if(trigger is IUpdateTrigger<TModel> updateTrigger)
                _updateTriggers.Add(updateTrigger);
            if(trigger is IDeleteTrigger<TModel> deleteTrigger)
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
        for (uint i = 0; i < _chunks.Length; i++)
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
        var body = expr.Body as MemberExpression;
        if (body is null)
        {
            throw new ArgumentException("index function for btree must be member expression");
        }

        Debug.Assert(expr.Parameters.Count == 1);
        if (expr.Parameters[0] != body.Expression)
        {
            throw new ArgumentException("index function for btree must be member expression");
        }

        if (body.Member is not PropertyInfo propertyInfo)
        {
            throw new ArgumentException("index can only be created on a property");
        }

        if (_bTreeIndices.ContainsKey(propertyInfo.Name))
        {
            throw new ArgumentException($"index for property {propertyInfo.Name} already exists");
        }

        var func = expr.Compile();

        _bTreeIndices[propertyInfo.Name] = new BTreeIndex<T>(x => func((TModel)x), 100); // TODO btree t size
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ChunkInfo GetChunkInfo(Guid id)
    {
        unsafe
        {
            var guidBuffer = new GuidBuffer(id);
            var chunkId = guidBuffer.Int[3] % (uint)_chunks.Length;
            var chunk = _chunks[chunkId];
            return new ChunkInfo
            {
                ChunkId = chunkId,
                Offset = chunk.ModelOffset[id],
            };
        }
    }

    public StorageInfo GetStorageInfo(Guid id)
    {
        unsafe
        {
            var guidBuffer = new GuidBuffer(id);
            var chunkId = guidBuffer.Int[3] % (uint)_chunks.Length;
            var chunk = _chunks[chunkId];
            var hasModel = chunk.ModelOffset.ContainsKey(id);
            ChunkInfo? chunkInfo = hasModel
                ? new ChunkInfo
                {
                    ChunkId = chunkId,
                    Offset = chunk.ModelOffset[id],
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
        foreach (var (key, value) in model.Changes)
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
                            $"Check {checkConstraint.GetType().Name} failed for {key} with value {value} on id {model.Id}.");
                    }

                    return false;
                }
            }
        }

        return true;
    }

    public void Write(TModel model, ChunkInfo? chunkInfo)
    {
        if (chunkInfo is not null)
        {
            foreach (var trigger in _updateTriggers)
            {
                //TODO: get the changes
                trigger.OnUpdate(model, new Dictionary<string, object?>());
            }
            
            foreach (var index in AllIndices)
            {
                index.Update(_chunks[chunkInfo.Value.ChunkId].GetModel(chunkInfo.Value.Offset), model);
            }
        }
        else
        {
            foreach (var trigger in _createTriggers)
            {
                trigger.OnCreate(model);
            }
            
            _modelCount++;
            RehashIfNeeded();
            chunkInfo = DetermineChunkForInsert(model.Id);
            foreach (var index in AllIndices)
            {
                index.Insert(model);
            }
        }

        var chunk = _chunks[chunkInfo.Value.ChunkId];

        var change = chunk.WriteModel(chunkInfo.Value.Offset, model);
        _persistenceEngine.WriteChunk(_name, chunkInfo.Value.ChunkId, chunk, change);
    }

    private ChunkInfo DetermineChunkForInsert(Guid id)
    {
        unsafe
        {
            var guidBuffer = new GuidBuffer(id);
            var chunkId = guidBuffer.Int[3] % (uint)_chunks.Length;
            var chunk = _chunks[chunkId];
            return new ChunkInfo { ChunkId = chunkId, Offset = chunk.ModelCount };
        }
    }

    private void RehashIfNeeded()
    {
        if (_modelCount * 100 / (_chunks.Length * ModelsPerChunk) <= RehashThreshold) return;

        var newChunkCount = _chunks.Length * 2;
        var newChunks = new ModelCollectionChunk<TModel>[newChunkCount];
        for (var i = 0; i < newChunks.Length; i++)
        {
            newChunks[i] = new ModelCollectionChunk<TModel>();
        }

        var oldChunks = _chunks;

        _chunks = newChunks;

        foreach (var oldChunk in oldChunks)
        {
            foreach (var model in oldChunk.Models)
            {
                var chunkInfo = DetermineChunkForInsert(model.Id);
                var chunk = _chunks[chunkInfo.ChunkId];
                chunk.WriteModel(chunkInfo.Offset, model);
            }
        }
    }

    public TModel Read(ChunkInfo chunkInfo)
    {
        var chunk = _chunks[chunkInfo.ChunkId];
        return chunk.GetModel(chunkInfo.Offset);
    }

    public void Delete(ChunkInfo chunkInfo)
    {
        var chunk = _chunks[chunkInfo.ChunkId];
        var model = chunk.GetModel(chunkInfo.Offset);
        
        foreach (var trigger in _deleteTriggers)
        {
            trigger.OnDelete(model);
        }
        
        foreach (var index in AllIndices)
        {
            index.Remove(model);
        }

        chunk.DeleteModel(chunkInfo.Offset);
    }

    public IEnumerable<Row<TModel>> GetAllRows()
    {
        // ReSharper disable once LoopCanBeConvertedToQuery
        for (uint chunkId = 0; chunkId < _chunks.Length; chunkId++)
        {
            var chunk = _chunks[chunkId];
            for (uint modelOffset = 0; modelOffset < chunk.Models.Count; modelOffset++)
            {
                var model = chunk.Models[(int)modelOffset];

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
    public IEnumerable<ModelBase> Scan(object from, object to, bool fromSet, bool toSet, bool fromInclusive, bool toInclusive, bool backwards);
    void Insert(ModelBase model);
    void Update(ModelBase oldModelBase, ModelBase model);
    void Remove(ModelBase model);
}

public class BTreeIndex<T> : IIndex
    where T : IComparable<T>, IEquatable<T>
{
    private readonly Func<ModelBase, T> _func;
    private readonly BPlusTree<T, ModelBase> _tree;

    public BTreeIndex(Func<ModelBase, T> func, int t)
    {
        _func = func;
        _tree = new BPlusTree<T, ModelBase>(t);
    }

    public IEnumerable<ModelBase> Scan(object from, object to, bool fromSet, bool toSet, bool fromInclusive, bool toInclusive, bool backwards)
    {
        return _tree.FunkyRange((T)from ?? default, (T)to ?? default, fromSet, toSet, !fromInclusive, !toInclusive, backwards);
    }

    public void Insert(ModelBase model)
    {
        _tree.Insert(_func(model), model);
    }

    public void Update(ModelBase oldModelBase, ModelBase model)
    {
        if (_func(oldModelBase).Equals(_func(model))) return;
        
        Remove(oldModelBase);
        Insert(model);
    }

    public void Remove(ModelBase model)
    {
        _tree.Remove(_func(model), model);
    }
}