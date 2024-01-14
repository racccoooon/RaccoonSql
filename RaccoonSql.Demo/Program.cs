using System.Diagnostics;
using System.IO.Abstractions;
using Bogus;
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
var addressFaker = new Faker<Address>()
    .RuleFor(x => x.City, f => f.Address.City())
    .RuleFor(x => x.Street, f => f.Address.StreetName());

var personFaker = new Faker<PersonModel>()
    .RuleFor(x => x.Address, () => addressFaker)
    .RuleFor(x => x.Birthday, f => f.Date.PastDateOnly(50))
    .RuleFor(x => x.Height, f => f.Random.Number(110, 220))
    .RuleFor(x => x.Name, f => f.Person.FullName);

var personModels = personFaker.GenerateForever().Take(100_000).ToList();

var stopwatch = Stopwatch.StartNew();

foreach (var t in personModels)
{
    persons.Insert(t);
}

stopwatch.Stop();

Console.WriteLine(TimeSpan.FromMilliseconds(stopwatch.ElapsedMilliseconds).Humanize());