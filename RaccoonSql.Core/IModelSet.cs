using JetBrains.Annotations;

namespace RaccoonSql.Core;

[PublicAPI]
public interface IModelSet<TData> where TData : IModel
{
    void Insert(TData data, ConflictBehavior? conflictBehavior = null);

    Task InsertAsync(TData data, ConflictBehavior? conflictBehavior = null, CancellationToken cancellationToken = default)
    {
        Insert(data);
        return Task.CompletedTask;
    }
    
    bool Exists(Guid id);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Exists(id));
    
    TData? Find(Guid id, ConflictBehavior? conflictBehavior = null);

    Task<TData?> FindAsync(Guid id, ConflictBehavior? conflictBehavior = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Find(id, conflictBehavior));
    
    void Update(TData data, ConflictBehavior? conflictBehavior = null);

    Task UpdateAsync(TData data, ConflictBehavior? conflictBehavior = null, CancellationToken cancellationToken = default)
    {
        Update(data, conflictBehavior);
        return Task.CompletedTask;
    }
    
    void Upsert(TData data);

    Task UpsertAsync(TData data, CancellationToken cancellationToken = default)
    {
        Upsert(data);
        return Task.CompletedTask;
    }
    
    void Remove(Guid id, ConflictBehavior? conflictBehavior = null);

    Task RemoveAsync(Guid id, ConflictBehavior? conflictBehavior = null, CancellationToken cancellationToken = default)
    {
        Remove(id, conflictBehavior);
        return Task.CompletedTask;
    }

    IEnumerable<TData> Where(Func<TData, bool> predicate);
}