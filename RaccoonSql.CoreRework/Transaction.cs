using RaccoonSql.CoreRework.Internal;

namespace RaccoonSql.CoreRework;

public enum CommitMode
{
    Manual,
    Auto,
}

public readonly struct TransactionOptions()
{
    public CommitMode CommitMode { get; init; } = CommitMode.Manual;
}

public interface ITransaction : IDisposable
{
    bool IsCompleted { get; }

    IModelSet<TModel> Set<TModel>()
        where TModel : ModelBase;

    void Commit();
    void Rollback();

    //TODO: implement these on the transaction directly for ease of use
    // void Add(ModelBase model);
    // void AddRange(params ModelBase[] models);
    // void AddRange(IEnumerable<ModelBase> models);
    //
    // TModel? Find<TModel>(Guid id);
    //
    // void Remove(ModelBase model);
    // void Remove(IEnumerable<ModelBase> models);
    // void Remove(params ModelBase[] models);
}

public sealed class Transaction : ITransaction
{
    private readonly ModelStore _store;
    private readonly TransactionOptions _transactionOptions;
    
    public bool IsCompleted { get; private set; }

    private readonly Dictionary<Type, IModelSet> _modelSets = [];

    internal Transaction(ModelStore store,
        TransactionOptions transactionOptions)
    {
        _store = store;
        _transactionOptions = transactionOptions;
    }

    public IModelSet<TModel> Set<TModel>()
        where TModel : ModelBase
    {
        if (IsCompleted) throw new TransactionClosedException();

        if (!_modelSets.TryGetValue(typeof(TModel), out var set))
        {
            set = _modelSets[typeof(TModel)] = new ModelSet<TModel>(_store.GetCollection<TModel>(), this);
        }

        return (IModelSet<TModel>)set;
    }

    public void Commit()
    {
        if (IsCompleted) throw new TransactionClosedException();
        IsCompleted = true;

        List<ChangeSet> commit = [];
        foreach (var (_, set) in _modelSets)
        {
            var changeSet = set.GetChanges();
            if(!changeSet.HasChanges)
                continue;
            
            commit.Add(changeSet);
        }

        _store.Commit(commit);
    }

    public void Rollback()
    {
        if (IsCompleted) throw new TransactionClosedException();
        IsCompleted = true;
    }

    public void Dispose()
    {
        if (IsCompleted) return;
        
        switch (_transactionOptions.CommitMode)
        {
            case CommitMode.Auto:
                Commit();
                break;
            
            case CommitMode.Manual:
                Rollback();
                break;
            
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}

public class TransactionClosedException : Exception;