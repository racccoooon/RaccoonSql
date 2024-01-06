using Humanizer;
using RaccoonSql.Core;
using RaccoonSql.Core.Persistance.FileSystem;
using RaccoonSql.Core.Storage;
using RaccoonSql.Demo.Models;

var fileSystemStorageEngineOptions = new StorageEngineOptions
{
    StoragePath = "/",
    PersistenceProviderFactory = new FileSystemPersistenceEngineFactory(),
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