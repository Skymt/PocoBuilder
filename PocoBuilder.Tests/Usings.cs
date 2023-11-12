global using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PocoBuilder.Tests
{
    public interface IArticle { int ArticleId { get; init; } }
    public interface IPrice : IArticle { decimal Price { get; init; } }
    public interface IName : IArticle { string Name { get; init; } }
    public interface IDescription : IArticle { string Description { get; set; } }
    public interface ICategory : IArticle { string Category { get; set; } }
}
namespace PocoBuilder.Tests.Workflows
{
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

        static DTOTemplate<TTarget> Cast<TTarget, TSource>(TSource instance)
            where TTarget : IPersistantObject
            where TSource : IPersistantObject
        {
            var newTemplate = new DTOTemplate<TSource>(instance).Cast<TTarget>();
            newTemplate.Set(m => m.Id, Guid.Empty);
            newTemplate.Set(m => m.Created, DateTimeOffset.MinValue);
            newTemplate.Set(m => m.CreatedBy, string.Empty);
            return newTemplate;
        }

        // Create a representation in permanent storage, and return an instance of the template
        static T Create<T>(DTOTemplate<T> template, string createdBy)
            where T : IPersistantObject
        {
            if (template.TryGet(m => m.Id, out var id) && id != Guid.Empty) throw new Exception("This object has a database ID - are you sure it hasn't already been created?");

            template.Set(m => m.Id, Guid.NewGuid())
                .Set(m => m.CreatedBy, createdBy)
                .Set(m => m.Created, DateTimeOffset.UtcNow);

            var instance = DTOBuilder.CreateInstanceOf(template);
            // TODO: Get an apropriate delegate that performs INSERT for T from a config source and invoke it for instance.
            instance.Persist = null; // TODO: Get an apropriate delegate that performs UPDATE for T from a config source
            return instance;
        }

        // Assign proper updating mechanisms and return an actual instance of the template
        static T Attach<T>(DTOTemplate<T> template)
            where T : IPersistantObject
        {
            if (!template.TryGet(m => m.Id, out var id) || id == Guid.Empty) throw new Exception("This template is missing database ID - are you sure this data comes from the right place?");

            if (!template.TryGet(m => m.Created, out var created) || created == DateTimeOffset.MinValue) throw new Exception("This template is missing it's creation date");

            if (string.IsNullOrEmpty(template.Get(m => m.CreatedBy))) throw new Exception("This template is missing a creator - anonymous data is not recommended in a professional setting!");

            var instance = DTOBuilder.CreateInstanceOf(template);
            instance.Persist = null; // TODO: Get an apropriate delegate that performs UPDATE for T from a config source
            return instance;
        }

        static bool CanAttach<T>(DTOTemplate<T> template) where T : IPersistantObject
        {
            if (!template.TryGet(m => m.Id, out var id) || id == Guid.Empty) return false;
            if (!template.TryGet(m => m.Created, out var created) || created == DateTimeOffset.MinValue) return false;
            if (string.IsNullOrEmpty(template.Get(m => m.CreatedBy))) return false;

            return true;
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

                var mutation = DTOBuilder.CreateInstanceOf(template);
                mutation.Persist = instance.Persist;
                mutation.Persist?.Invoke();

                instance.Obsolete = true;
                instance.Persist = null;
                return mutation;
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
                instance.Persist = null;

                return newVersion;
            }
        }
    }
}