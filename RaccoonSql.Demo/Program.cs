using RaccoonSql.Core;
using RaccoonSql.Core.Storage.InMemory;
using RaccoonSql.Demo.Models;

var modelStore = new ModelStore(new InMemoryStorageEngineFactory());
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