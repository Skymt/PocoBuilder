namespace PocoBuilder.Tests;

[TestClass]
public class Factory
{
    // NOTE: I'm starting to think DTOFactory is a bad idea,
    // all it has that Templates<TInterface> don't are
    // unsafe ways of setting values.
    // And the performance is atrocious! So much allocation!

    public interface IListProduct : IArticle, IName, IPrice, ICategory { }

    private static DTOFactory<IListProduct> BuildFactory()
    {
        var factory = new DTOFactory<IListProduct>();
        factory.Set(i => i.Name, "Fancy product")
            .Set(i => i.Category, "Unsorted")
            .Set(i => i.ArticleId, 5)
            .Set(i => i.Price, 99.50m)
        ;
        return factory;
    }

    [TestMethod]
    public void Test1_FactoryGetSet()
    {
        // Create a factory with some default values
        var factory = BuildFactory();

        // Instances of the interface is now a method call away.
        var instance1 = factory.CreateInstance();
        Assert.AreEqual("Fancy product", instance1.Name);
        Assert.AreEqual("Unsorted", instance1.Category);
        Assert.AreEqual(5, instance1.ArticleId);
        Assert.AreEqual(99, 5m, instance1.Price);

        // Type safe access to properties are handled with Get and Set methods
        var articleId = factory.Get(i => i.ArticleId);
        Assert.AreEqual(5, articleId);
        var category = factory.Get(i => i.Category);
        Assert.AreEqual("Unsorted", category);

        // Value can then be worked with...
        articleId++;
        category = "Sorted";
        // ...and set back into the factory
        factory.Set(i => i.ArticleId, articleId);
        factory.Set(i => i.Category, category);

        // A new instance can then be activated
        var instance2 = factory.CreateInstance();
        Assert.AreEqual(6, instance2.ArticleId);
        Assert.AreEqual("Sorted", instance2.Category);

        // Values unchanged between instance activations remain the same
        Assert.AreEqual(instance1.Price, instance2.Price);
        Assert.AreEqual("Fancy product", instance2.Name);
    }

    [TestMethod]
    public void Test2_FactoryGetSetDictionary()
    {
        var factory = BuildFactory();
        var articleId = (int)(factory[nameof(IArticle.ArticleId)] ?? 0);
        var category = factory[nameof(ICategory.Category)];
        Assert.AreEqual(5, articleId);
        Assert.AreEqual("Unsorted", category);

        articleId++;
        factory[nameof(IArticle.ArticleId)] = articleId;
        factory[nameof(ICategory.Category)] = "Sorted";

        var instance = factory.CreateInstance();
        Assert.AreEqual(6, instance.ArticleId);
        Assert.AreEqual("Sorted", instance.Category);

        // THIS IS NOT A TYPE SAFE OPERATION
        // If the types of the property values are mismatched
        // a suitable constructor cannot be matched for activation,
        // and a MissingMethodException is thrown!
        factory[nameof(IArticle.ArticleId)] = Guid.NewGuid();
        Assert.ThrowsException<MissingMethodException>(factory.CreateInstance);
    }

    [TestMethod]
    public void Test3_FacotoryGetSetDynamic()
    {
        var factory = BuildFactory();
        dynamic shenanigans = factory;
        Assert.AreEqual(5, shenanigans.ArticleId);
        Assert.AreEqual("Unsorted", shenanigans.Category);

        shenanigans.ArticleId++;
        shenanigans.Category = "Sorted";
        var instance = factory.CreateInstance();
        Assert.AreEqual(6, instance.ArticleId);
        Assert.AreEqual("Sorted", instance.Category);

        // THIS IS NOT A TYPE SAFE OPERATION
        shenanigans.ArticleId = "Day one of going undercover as an integer and I'm already caught :(";
        Assert.ThrowsException<MissingMethodException>(factory.CreateInstance);
    }

    [TestMethod]
    public void Test4_FactoryEnumerable()
    {
        var factory = BuildFactory();
        var articlesWithIncrementingIds = factory.CreateInstances(t => t.Set(m => m.ArticleId, t.Get(m => m.ArticleId) + 1).Set(m => m.Price, (decimal)Random.Shared.NextDouble()));

        var oneHundredArticles = articlesWithIncrementingIds.Take(100).ToArray();

        Assert.AreEqual(100, oneHundredArticles.Length);

        Assert.AreEqual(5, oneHundredArticles[0].ArticleId);
        Assert.AreEqual(100, oneHundredArticles[^5].ArticleId);
        Assert.AreNotEqual(oneHundredArticles.First().Price, oneHundredArticles.Last().Price);
    }
}
