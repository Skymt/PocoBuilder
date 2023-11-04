namespace PocoBuilder.Tests;

[TestClass]
public class DefaultMethods
{
    public interface IInterfaceWithMethods : IArticle, IName, IPrice
    {
        string GetSalesPitch()
            => $"Buy {Name}, now only {Price:#.##} monies!!!";
        void Deconstruct(out int articleId, out string name, out IPrice price)
            => (articleId, name, price) = (ArticleId, Name, this);
    }

    [TestMethod]
    public void Test1_DefaultMethod()
    {
        var product = DTOBuilder.CreateInstanceOf<IInterfaceWithMethods>(init => init
            .Set(m => m.ArticleId, 15)
            .Set(m => m.Name, "Fancy product")
            .Set(m => m.Price, 99.9534m));
        Assert.IsNotNull(product);

        var salesPitch = product.GetSalesPitch();
        Assert.AreEqual("Buy Fancy product, now only 99.95 monies!!!", salesPitch);
    }

    [TestMethod]
    public void Test2_Deconstruction()
    {
        var product = DTOBuilder.CreateInstanceOf<IInterfaceWithMethods>(init => init
            .Set(m => m.ArticleId, 15)
            .Set(m => m.Name, "The name")
            .Set(m => m.Price, 99.95m));
        Assert.IsNotNull(product);

        var (id, name, price) = product;

        Assert.AreEqual(15, id);
        Assert.AreEqual("The name", name);

        Assert.AreEqual(15, price.ArticleId);
        Assert.AreEqual(99.95m, price.Price);
    }
}
