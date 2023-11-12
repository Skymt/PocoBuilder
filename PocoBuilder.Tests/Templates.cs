namespace PocoBuilder.Tests;

[TestClass]
public class Templates
{
    public interface ITestArticle : IArticle, IName, IPrice { }
    public interface ITestCartItem : IArticle, IName, IPrice { int Count { get; init; } }

    [TestMethod]
    public void Test1_Templates()
    {
        // Templates are a type-safe containers for
        // interfaces with immutable properties
        var template = new DTOTemplate<ITestArticle>();
        template.Set(m => m.ArticleId, 1);
        template.Set(m => m.Name, "Produktnamn");

        var instance = DTOBuilder.CreateInstanceOf(template);
        Assert.IsNotNull(instance);
        Assert.AreEqual(1, instance.ArticleId);

        // Templates can be re-used to create many instances
        var oneHundredArticles = infiniteArticles().Take(100).ToList();
        Assert.AreEqual(1, oneHundredArticles.First().ArticleId);
        Assert.AreEqual(100, oneHundredArticles.Last().ArticleId);
        Assert.AreEqual(100, oneHundredArticles.Count());
        return;

        IEnumerable<ITestArticle> infiniteArticles()
        {
            var currentId = template.Get(m => m.ArticleId);
            while (true)
            {
                template.Set(m => m.ArticleId, currentId++);
                yield return DTOBuilder.CreateInstanceOf(template);
            }
        }
    }

    [TestMethod]
    public void Test2_TemplateCasting() 
    {
        var template1 = new DTOTemplate<ITestArticle>();
        template1.Set(m => m.ArticleId, 1);
        template1.Set(m => m.Name, "Produktnamn");
        template1.Set(m => m.Price, 95);

        var template2 = template1.Cast<ITestCartItem>();
        template2.Set(m => m.Count, 3);

        var instance = DTOBuilder.CreateInstanceOf(template2);
        Assert.IsInstanceOfType<ITestCartItem>(instance);
        Assert.AreEqual("Produktnamn", instance.Name);
        Assert.AreEqual(95m, instance.Price);
        Assert.AreEqual(3, instance.Count);

        // Templates preserve values through casts.
        var template3 = template2.Cast<ITestArticle>();
        template3.Set(m => m.Name, "Något annat");

        // template3 has no property Count
        var template4 = template3.Cast<ITestCartItem>();

        // But when casted back to ITestCartItem, the value remains!
        var castedInstance = DTOBuilder.CreateInstanceOf(template4);
        Assert.AreEqual(3, castedInstance.Count);
        Assert.AreEqual("Något annat", castedInstance.Name);
    }
}
