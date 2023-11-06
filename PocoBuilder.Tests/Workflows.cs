namespace PocoBuilder.Tests;

[TestClass]
public class Workflows
{
    // Workflows can be attached to property collections (like IArticle, IName) by
    // assigning another interface, in this case journaled database management.
    public interface ITest : IArticle, IName, IJournaledObject { }

    [TestMethod]
    public void Test1_Workflows()
    {
        // Say we get some data from some import, and we fill a template of our own design with it.
        var dataFromSomeImport = new Template<ITest>();
        dataFromSomeImport.Set(m => m.ArticleId, 5);
        dataFromSomeImport.Set(m => m.Name, "Change me later please");

        // We want this data as a proper object instance - not wrapped in a template
        ITest instance;

        // This template is not valid for loading (when data comes from db, more properties must have been set)
        Assert.ThrowsException<Exception>(() =>
        {
            instance = IPersistantObject.Load(dataFromSomeImport);
            // Load verifies that the data in the template is enough,
            // and assigns a method, to update this data in the database.
        });

        // But it can be created, which is apropriate since it's from an import
        instance = IPersistantObject.Create(dataFromSomeImport, createdBy: "AutoImport");
        // This returns an actual instance, and the data will be persisted (not in this sample though)

        // This instance is the first and so far only version.
        Assert.IsTrue(instance.IsLatestVersion());

        // We cannot simply mutate it, since it is a fully journaled object
        Assert.ThrowsException<Exception>(() =>
        {
            IPersistantObject.Mutate(instance, mutator => mutator.Set(m => m.Name, "A brand new name"));
        });

        // Instead we need to update it, and provide an identity
        var updatedInstance = IJournaledObject.Update(instance, mutator => mutator.Set(m => m.Name, "A brand new name"), updatedBy: "Fredrik");
        Assert.IsFalse(instance.IsLatestVersion());
        Assert.AreEqual(updatedInstance.Id, instance.NextVersionId);

        Assert.AreEqual(instance.ArticleId, updatedInstance.ArticleId);
        Assert.AreNotEqual(instance.Name, updatedInstance.Name);
    }

    public interface IPersistantObject
    {
        Guid Id { get; init; }
        DateTimeOffset Created { get; init; }
        string CreatedBy { get; init; }
        protected Action? Persist { get; init; }
        protected bool Obsolete { get; set; }
        
        // Create a representation in permanent storage, and return a proper instance of the template
        static T Create<T>(Template<T> template, string createdBy)
            where T : IPersistantObject
        {
            var id = template.Get<Guid?>(m => m.Id);
            if (id != null && id != Guid.Empty) throw new Exception("This object has a database ID - are you sure it hasn't already been created?");

            template.Set(m => m.Id, Guid.NewGuid())
                .Set(m => m.CreatedBy, createdBy)
                .Set(m => m.Created, DateTimeOffset.UtcNow);
            // TODO: Get an apropriate delegate that performs UPDATE for T from a config source, and assign it to IPersistantObject.Persist.
            // template.Set(m => m.Persist, action);

            var instance = DTOBuilder.CreateInstanceOf(template);
            // TODO: Get an apropriate delegate that performs INSERT for T from a config source and invoke it for instance.
            return instance;
        }

        // Assign proper updating mechanisms and return an actual instance of the template
        static T Load<T>(Template<T> template)
            where T : IPersistantObject
        {
            var id = template.Get<Guid?>(m => m.Id);
            if (id == null || id == Guid.Empty) throw new Exception("This template is missing database ID - are you sure this data comes from the right place?");

            var created = template.Get<DateTimeOffset?>(m => m.Created);
            if (created == null || created == DateTime.MinValue) throw new Exception("This template is missing it's creation date - we cannot load something that hasn't been created.");

            if (string.IsNullOrEmpty(template.Get(m => m.CreatedBy))) throw new Exception("This template is missing a creator - anonymous data is not recommended in a professional setting!");

            // TODO: Get an apropriate delegate that performs UPDATE for T from a config source, and assign it to IPersistantObject.Persist.
            // template.Set(m => m.Persist, action);
            return DTOBuilder.CreateInstanceOf(template);
        }

        // Change values, immutable or not, and update the database.
        // Returns a new object reference with the changes made.
        static T Mutate<T>(T instance, Action<ISetter<T>> mutator, bool throwIfObsolete = false)
                where T : IPersistantObject
        {
            lock (instance)
            {
                if (instance is IJournaledObject) throw new Exception($"Please use IJournaledObject.Update() to mutate {typeof(T).FullName}");
                if (instance.Obsolete && throwIfObsolete) throw new Exception("This object reference has already mutated, and is no longer valid.");

                var template = new Template<T>(instance);

                mutator(template);
                instance.Obsolete = true;

                var mutatedInstance = DTOBuilder.CreateInstanceOf(template);
                mutatedInstance.Persist?.Invoke();

                return mutatedInstance;
            }
        }
    }
    public interface IJournaledObject : IPersistantObject
    {
        Guid? NextVersionId { get; protected set; }
        bool IsLatestVersion() => !NextVersionId.HasValue;

        // Update the values, but keep full history, and assign blame to the author
        static T Update<T>(T instance, Action<ISetter<T>> mutator, string updatedBy)
            where T : IJournaledObject
        {
            lock (instance)
            {
                if (instance.Obsolete || instance.NextVersionId.HasValue) 
                    throw new Exception("You cannot make an update to an obsolete object reference.");
                var template = new Template<T>(instance);

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