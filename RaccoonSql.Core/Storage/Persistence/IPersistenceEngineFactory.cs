namespace RaccoonSql.Core.Storage.Persistence;

public interface IPersistenceEngineFactory
{
    IPersistenceEngine Create();
}