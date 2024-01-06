using RaccoonSql.Core;
using RaccoonSql.Core.Serialization.Json;
using RaccoonSql.Core.Storage.FileSystem;
using RaccoonSql.Demo.Models;

var fileSystemStorageEngineOptions = new FileSystemStorageEngineOptions
{
    StoragePath = "/",
    SerializationEngineFactory = new JsonSerializationEngineFactory(),
};
var modelStoreOptions = new ModelStoreOptions
{
    DefaultInsertConflictBehavior = ConflictBehavior.Ignore,
    DefaultRemoveConflictBehavior = ConflictBehavior.Ignore,
    DefaultUpdateConflictBehavior = ConflictBehavior.Ignore,
    FindDefaultConflictBehavior = ConflictBehavior.Ignore,
};
var modelStore = new ModelStore(modelStoreOptions, new FileSystemStorageEngineFactory(fileSystemStorageEngineOptions));

var persons = modelStore.Set<PersonModel>();

var person = persons.Find(Guid.Parse("4C870818-4EB7-4DBB-B37C-0A9622291841"));

var newPerson = new PersonModel
{
    Birthday = DateOnly.FromDateTime(DateTime.Now),
};

persons.Insert(newPerson);

var fetchPerson = persons.Find(newPerson.Id);
if (fetchPerson is not null) {
    persons.Remove(fetchPerson.Id);
}

if (person is not null)
{
    person.Birthday = DateOnly.MaxValue;
    persons.Update(person);
}