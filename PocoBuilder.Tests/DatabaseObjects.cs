using PocoBuilder.Tests.Workflows;
namespace PocoBuilder.Tests;

[TestClass]
public class DatabaseObjects
{
    // Workflows are also interfaces, and can be combined
    // with property interfaces.
    // E.g an article with a name, that is stored as a journaled
    // database object.
    public interface IDbArticle : IArticle, IName, IJournaledObject { }
    public interface ICartItem : IArticle, IPrice, IPersistant<ICartItem> { Guid CartId { get; init; } int Count { get; set; } }

    public interface IGeneric : ICartItem, IPersistant<IGeneric> { }
    [TestMethod]
    public void Test1_PersistantObject()
    {
        IDbArticle article = DTOBuilder.CreateInstanceOf<IDbArticle>(init => init
            .Set(a => a.Id, Guid.NewGuid())
            .Set(a => a.Created, DateTimeOffset.Now.AddMinutes(-15448453))
            .Set(a => a.CreatedBy, "Fredrik")
            .Set(a => a.ArticleId, 1)
            .Set(a => a.Name, "Very nice article"));

        var cartItemTemplate = IPersistantObject.Cast<ICartItem, IDbArticle>(article);
        cartItemTemplate.Set(m => m.CartId, Guid.NewGuid());
        cartItemTemplate.Set(m => m.Count, 1);
        cartItemTemplate.Set(m => m.Price, 64m);

        var cartItem = IPersistantObject.Create(cartItemTemplate, "CartService");
        Assert.IsNotNull(cartItem);

        // Note: This will NOT trigger a change in persistant storage
        cartItem.Count = 15;
        // so make sure your setters are init only!

        // Trigger a change in persistant storage.
        var cartItemUpdate = IPersistantObject.Update(cartItem, mutator => mutator.Set(m => m.Count, 4));
        Assert.IsNotNull(cartItemUpdate);

        // The cartItem reference is now obsolete, and may not be updated again.
        Assert.ThrowsException<Exception>(() =>
        {
            cartItem = IPersistantObject.Update(cartItem, mutator => mutator.Set(m => m.Count, 1));
        });

        // So it can be re-assigned
        cartItem = cartItemUpdate;
        cartItemUpdate = IPersistantObject.Update(cartItem, mutator => mutator.Set(m => m.Count, 1));
        Assert.IsNotNull(cartItemUpdate);

        // Test generic version
        var testItem = IPersistantObject.Cast<IGeneric, ICartItem>(cartItem);
        var testInstance = IPersistantObject.Create(testItem, "Test");
        Assert.AreEqual(64m, testInstance.Price);
        Assert.IsTrue(testInstance.Mutate(mutator => mutator.Set(m => m.Price, testInstance.Price / 2), out testInstance));
        Assert.AreEqual(32m, testInstance.Price);
        Assert.IsTrue(testInstance.Mutate(mutator => mutator.Set(m => m.Price, testInstance.Price / 2), out IGeneric anotherInstance));
        Assert.AreEqual(16m, anotherInstance.Price);

        // We lost reference chain, and cannot mutate test any longer
        Assert.IsFalse(testInstance.Mutate(mutator => mutator.Set(m => m.Price, anotherInstance.Price / 2), out testInstance));
        Assert.AreEqual(32m, testInstance.Price); // <- unchanged
    }

    [TestMethod]
    public void Test2_JournaledObject()
    {
        var dataFromSomeImport = new DTOTemplate<IDbArticle>();
        dataFromSomeImport.Set(m => m.ArticleId, 5);
        dataFromSomeImport.Set(m => m.Name, "Change me later please");
        // TODO: Also complete the template with local data if it exists.
        // FORNOW: Assume this is a brand new product.

        // We want this data as a proper object instance - not wrapped in a template.
        // This would indicate that it is properly saved in db and handled by core
        IDbArticle? instance = null;
        Assert.ThrowsException<Exception>(() =>
        {
            instance = IPersistantObject.Attach(dataFromSomeImport);
        });

        if (!IPersistantObject.CanAttach(dataFromSomeImport)) // Oh, it's not a valid db object yet, so it must be created.
            instance = IPersistantObject.Create(dataFromSomeImport, createdBy: "AutoImport");

        // After creation, the instance exists in db, and has an Id set.
        Assert.IsNotNull(instance);

        // Thus, a template made from this instance, could be attached.
        Assert.IsTrue(IPersistantObject.CanAttach(new DTOTemplate<IDbArticle>(instance)));

        // But we already have an instance so...
        Assert.IsTrue(instance.IsLatestVersion());

        // Journaled object cannot mutate without an identity.
        Assert.ThrowsException<Exception>(() =>
        {
            var updatedInstance = IPersistantObject.Update(instance,
                mutator => mutator.Set(m => m.Name, "A brand new name"));
        });

        var updatedInstance = IJournaledObject.Update(instance,
            mutator => mutator.Set(m => m.Name, "A brand new name"),
            updatedBy: "Fredrik");
        Assert.IsFalse(instance.IsLatestVersion());
        Assert.IsTrue(updatedInstance.IsLatestVersion());
        Assert.AreEqual(updatedInstance.Id, instance.NextVersionId);

        Assert.AreEqual(instance.ArticleId, updatedInstance.ArticleId);
        Assert.AreNotEqual(instance.Name, updatedInstance.Name);
    }

    [TestMethod]
    public void Test3_JournaledObject()
    {
        var dataFromDb = new DTOTemplate<IDbArticle>()
            .Set(a => a.Id, Guid.NewGuid())
            .Set(a => a.Created, DateTime.Now.AddMinutes(-15448453))
            .Set(a => a.CreatedBy, "Fredrik")
            .Set(a => a.ArticleId, 1)
            .Set(a => a.Name, "Very nice article");

        // Ensure that it is valid db-object before attaching it to avoid exceptions!
        if (IPersistantObject.CanAttach(dataFromDb))
        {
            var article = IPersistantObject.Attach(dataFromDb);
            Assert.IsNotNull(article);

            var update = IJournaledObject.Update(article, mutator => mutator.Set(m => m.Name, "Väldigt trevlig artikel"), "TranslationWorker");
            Assert.IsNotNull(update);
        }
        else Assert.Fail();
    }
}