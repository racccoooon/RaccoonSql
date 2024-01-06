using System.IO.Abstractions;
using Humanizer;
using RaccoonSql.Core;
using RaccoonSql.Core.Serialization.Json;
using RaccoonSql.Core.Storage;
using RaccoonSql.Core.Storage.Persistence.FileSystem;
using RaccoonSql.Demo.Models;

var persistenceOptions = new FileSystemPersistenceOptions
{
    FileSystem = new FileSystem(),
    SerializationEngineFactory = new JsonSerializationEngineFactory(),
    Path = "TestDB",
};
var fileSystemStorageEngineOptions = new StorageEngineOptions
{
    PersistenceProviderFactory = new FileSystemPersistenceEngineFactory(persistenceOptions),
};
var modelStoreOptions = new ModelStoreOptions
{
    DefaultInsertConflictBehavior = ConflictBehavior.Ignore,
    DefaultRemoveConflictBehavior = ConflictBehavior.Ignore,
    DefaultUpdateConflictBehavior = ConflictBehavior.Ignore,
    FindDefaultConflictBehavior = ConflictBehavior.Ignore,
};
var modelStore = new ModelStore(modelStoreOptions, new StorageEngineFactory(fileSystemStorageEngineOptions));

var persons = modelStore.Set<PersonModel>();

persons.Insert(new PersonModel()
{
    Birthday = DateOnly.FromDateTime(new DateTime(1994, 12, 16)),
});

persons.Insert(new PersonModel()
{
    Birthday = DateOnly.FromDateTime(new DateTime(1996, 9, 11)),
});

var all = persons.Where(x => x.Birthday.Year < 1995)
    .ToList();

Console.WriteLine(all.Humanize());