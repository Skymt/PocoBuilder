namespace PocoBuilder.Tests;

[TestClass]
public class Templates
{
    public interface ITestArticle : IArticle, IName, IPrice { }
    public interface ITestCartItem : IArticle, IName, IPrice { int Count { get; init; } }

    [TestMethod]
    public void Test1_Templates()
    {
        // Templates are a bit like factories, but with type safety.
        // i.e. only the generic Get and Set methods are available
        // for value mutation.
        var template = new Template<ITestArticle>();
        template.Set(m => m.ArticleId, 1);
        template.Set(m => m.Name, "Produktnamn");

        var instance = DTOBuilder.CreateInstanceOf(template);
        Assert.IsNotNull(instance);
        Assert.AreEqual(1, instance.ArticleId);

        // Just like factories, templates can be re-used when
        // creating instances.
        var oneHundredArticles = infiniteArticles().Take(100);
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
        var template = new Template<ITestArticle>();
        template.Set(m => m.ArticleId, 1);
        template.Set(m => m.Name, "Produktnamn");
        template.Set(m => m.Price, 95);

        var cartItem = template.Cast<ITestCartItem>();
        cartItem.Set(m => m.Count, 3);

        var instance = DTOBuilder.CreateInstanceOf(cartItem);
        Assert.IsInstanceOfType<ITestCartItem>(instance);
        Assert.AreEqual("Produktnamn", instance.Name);
        Assert.AreEqual(95m, instance.Price);
        Assert.AreEqual(3, instance.Count);
    }
}
