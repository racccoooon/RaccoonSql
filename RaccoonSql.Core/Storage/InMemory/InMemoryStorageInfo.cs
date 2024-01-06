namespace RaccoonSql.Core.Storage.InMemory;

public record InMemoryStorageInfo(bool Exists, Type Type, Guid Id) : IStorageInfo;