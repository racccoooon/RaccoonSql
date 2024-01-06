using JetBrains.Annotations;

namespace RaccoonSql.Core.Storage;

[PublicAPI]
public interface IStorageEngineFactory
{
    IStorageEngine Create();
}