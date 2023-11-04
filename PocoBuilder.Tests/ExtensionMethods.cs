using PocoBuilder.Tests.Extensions;

namespace PocoBuilder.Tests
{
    [TestClass]
    public class ExtensionMethods
    {
        public interface IDetailProduct : IArticle, IName, IPrice, ICategory, IDescription { }

        [TestMethod]
        public void Test1_ValidateFromExtensionMethod()
        {
            var product = DTOBuilder.CreateInstanceOf<IDetailProduct>(init => init
                .Set(m => m.ArticleId, 15)
                .Set(m => m.Name, "The name")
                .Set(m => m.Price, 99.95m)
                .Set(m => m.Category, "The category")
                .Set(m => m.Description, "The description"));
            Assert.IsNotNull(product);

            Assert.IsTrue(product.Validate());
        }
    }
}

namespace PocoBuilder.Tests.Extensions
{
    public static class IDetailProductExtensions
    {
        public static bool Validate(this ExtensionMethods.IDetailProduct product)
        {
            if (product == null) return false;
            if (product.ArticleId <= 0) return false;
            if (product.Price <= 0) return false;

            var mandatoryStrings = new[] { product.Name, product.Category, product.Description };
            if (mandatoryStrings.Any(string.IsNullOrWhiteSpace)) return false;

            return true;
        }
    }
}