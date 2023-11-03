namespace PocoBuilder.Tests;

[TestClass]
public class Deconstruction
{
    public interface IDetailProduct : IArticle, IName, IPrice
    {
        void Deconstruct(out int articleId, out string name, out IPrice price)
        {
            articleId = ArticleId; name = Name; price = this;
        }
    }

    [TestMethod]
    public void Test1_Deconstruction()
    {
        var product = DTOBuilder.CreateInstanceOf<IDetailProduct>(init => init
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
