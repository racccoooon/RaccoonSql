namespace RaccoonSql.Core.Persistance;

public interface IPersistenceEngineFactory
{
    IPersistenceEngine Create();
}