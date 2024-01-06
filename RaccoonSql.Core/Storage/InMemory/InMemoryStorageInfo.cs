namespace RaccoonSql.Core.Storage.InMemory;

public record InMemoryStorageInfo(bool Exists, string CollectionName, Guid Id) : IStorageInfo;