namespace PocoBuilder.Tests;

[TestClass]
public class Workflows
{
    // Workflows are also interfaces, and can be combined
    // with property interfaces.
    // E.g an article with a name, that is stored as a journaled
    // database object.
    public interface IDbArticle : IArticle, IName, IJournaledObject { }

    [TestMethod]
    public void Test1_Workflows()
    {
        // Say we get some data from some import, and we fill a template of our own design with it.
        var dataFromSomeImport = new DTOTemplate<IDbArticle>();
        dataFromSomeImport.Set(m => m.ArticleId, 5);
        dataFromSomeImport.Set(m => m.Name, "Change me later please");
        // TODO: Also complete the template with local data if it exists.
        // FORNOW: Assume this is a brand new product.

        // We want this data as a proper object instance - not wrapped in a template.
        // This would indicate that it is properly saved in db and handled by core
        IDbArticle instance;
        
        // This template is not valid for attaching
        Assert.ThrowsException<Exception>(() =>
        {
            instance = IPersistantObject.Attach(dataFromSomeImport);
            // (Attach verifies that the data in the template is enough
            // to make updates to this object in our database, and
            // assigns a delegate to actually do this update.)
        });

        // But it can be created, if supplied with a username.
        instance = IPersistantObject.Create(dataFromSomeImport, createdBy: "AutoImport");
        // This returns an actual instance, and the data will be persisted (not in this sample though)

        // This instance is the first and so far only version.
        Assert.IsTrue(instance.IsLatestVersion());

        // We cannot simply change it anonymously, since it is a fully journaled object
        Assert.ThrowsException<Exception>(() =>
        {
            IPersistantObject.Update(instance, mutator => mutator.Set(m => m.Name, "A brand new name"));
        });

        // Instead we need to update it, and provide an identity
        var updatedInstance = IJournaledObject.Update(instance, mutator => mutator.Set(m => m.Name, "A brand new name"), updatedBy: "Fredrik");
        Assert.IsFalse(instance.IsLatestVersion());
        Assert.IsTrue(updatedInstance.IsLatestVersion());
        Assert.AreEqual(updatedInstance.Id, instance.NextVersionId);

        Assert.AreEqual(instance.ArticleId, updatedInstance.ArticleId);
        Assert.AreNotEqual(instance.Name, updatedInstance.Name);

        // NOTE: This sample does not cover all types of valuable database storage!
        // Valuelist tables, log entry tables, n-n relationship tables AND OTHERS
        // are NOT suitable for this datastructure!!!
    }

    public interface IPersistantObject
    {
        Guid Id { get; init; }
        DateTimeOffset Created { get; init; }
        string CreatedBy { get; init; }

        // Protected properties are not visible outside of the interface, or the implementing class.
        // Thus, they are not part of the constructor signature, and must contain a normal
        // getter and setter to be usable.
        protected Action? Persist { get; set; }
        protected bool Obsolete { get; set; }
        
        // Create a representation in permanent storage, and return an instance of the template
        static T Create<T>(DTOTemplate<T> template, string createdBy)
            where T : IPersistantObject
        {
            var id = template.Get<Guid?>(m => m.Id);
            if (id != null && id != Guid.Empty) throw new Exception("This object has a database ID - are you sure it hasn't already been created?");

            template.Set(m => m.Id, Guid.NewGuid())
                .Set(m => m.CreatedBy, createdBy)
                .Set(m => m.Created, DateTimeOffset.UtcNow);

            var instance = DTOBuilder.CreateInstanceOf(template);
            // TODO: Get an apropriate delegate that performs UPDATE for T from a config source, and assign it to instance.Persist.
            // TODO: Get an apropriate delegate that performs INSERT for T from a config source and invoke it for instance.
            return instance;
        }

        // Assign proper updating mechanisms and return an actual instance of the template
        static T Attach<T>(DTOTemplate<T> template)
            where T : IPersistantObject
        {
            var id = template.Get<Guid?>(m => m.Id); // read as nullable guid, because who the eff knows what populated this template.
            if (id == null || id == Guid.Empty) throw new Exception("This template is missing database ID - are you sure this data comes from the right place?");

            var created = template.Get<DateTimeOffset?>(m => m.Created);
            if (created == null || created == DateTime.MinValue) throw new Exception("This template is missing it's creation date");

            if (string.IsNullOrEmpty(template.Get(m => m.CreatedBy))) throw new Exception("This template is missing a creator - anonymous data is not recommended in a professional setting!");

            var instance = DTOBuilder.CreateInstanceOf(template);
            // TODO: Get an apropriate delegate that performs UPDATE for T from a config source, and assign it to IPersistantObject.Persist.
            return instance;
        }

        // Change values and update the database.
        // Returns a new object reference with the changes made.
        static T Update<T>(T instance, Action<ISetter<T>> mutator, bool throwIfObsolete = false)
                where T : IPersistantObject
        {
            lock (instance)
            {
                if (instance is IJournaledObject) throw new Exception($"Please use IJournaledObject.Update() to mutate {typeof(T).FullName}");
                if (instance.Obsolete && throwIfObsolete) throw new Exception("This object reference has already mutated, and is no longer valid.");
                else if (instance.Obsolete) return instance;

                var template = new DTOTemplate<T>(instance);

                mutator(template);

                var mutatedInstance = DTOBuilder.CreateInstanceOf(template);
                mutatedInstance.Persist?.Invoke();
                instance.Obsolete = true;
                instance.Persist = null;

                return mutatedInstance;
            }
        }
    }
    public interface IJournaledObject : IPersistantObject
    {
        Guid? NextVersionId { get; protected set; }
        bool IsLatestVersion() => !NextVersionId.HasValue;

        // Update the values, and keep full history, and assign blame to the author
        static T Update<T>(T instance, Action<ISetter<T>> mutator, string updatedBy)
            where T : IJournaledObject
        {
            lock (instance)
            {
                if (instance.Obsolete || instance.NextVersionId.HasValue) 
                    throw new Exception("You cannot make an update to an obsolete object.");

                var template = new DTOTemplate<T>(instance);
                mutator(template);

                template.Set(m => m.Id, Guid.Empty);
                var newVersion = Create(template, createdBy: updatedBy);

                instance.NextVersionId = newVersion.Id;
                instance.Obsolete = true;
                instance.Persist?.Invoke();

                return newVersion;
            }
        }
    }
}