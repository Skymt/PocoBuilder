using System.Text.Json;
namespace PocoBuilder.Tests
{
    [TestClass]
    public class Serialization
    {
        public interface IDetailProduct : IArticle.IDisplayName, IArticle.IPrice, IArticle.ICategory, IArticle.IDescription { }
        public interface IListProduct : IArticle, IArticle.IDisplayName, IArticle.IPrice { }
        [TestMethod]
        public void Test1_Serialization()
        {
            var detailedProduct = PocoBuilder.CreateInstanceOf<IDetailProduct>(init => init
                .Set(i => i.Id, 2)
                .Set(i => i.Price, 99.95m)
                .Set(i => i.DisplayName, "Fancy Product")
                .Set(i => i.Description, "Eloquent Description")
                .Set(i => i.Category, "Logical Category")
            );
            var json = JsonSerializer.Serialize<object>(detailedProduct);
            //{"DisplayName":"Fancy Product","Id":2,"Price":99.95,"Category":"Logical Category","Description":"Eloquent Description"}

            Assert.IsFalse(string.IsNullOrWhiteSpace(json));
            Assert.AreNotEqual("{}", json);

            var listProduct = PocoBuilder.CreateInstanceOf<IListProduct>(init => init
                .Set(i => i.Id, 2)
                .Set(i => i.DisplayName, "Fancy Product")
                .Set(i => i.Price, 99.95m)
            );
            json = JsonSerializer.Serialize<object>(listProduct);
            // Note: The order of the properties matches the order of the inherited interfaces.
            //{"Id":2,"DisplayName":"Fancy Product","Price":99.95}
            Assert.IsFalse(string.IsNullOrWhiteSpace(json));
            Assert.AreNotEqual("{}", json);
        }

        [TestMethod]
        public void Test2_Deserialization()
        {
            var json = JsonSerializer.Serialize<object>(PocoBuilder.CreateInstanceOf<IListProduct>(init => init
                .Set(i => i.Id, 2)
                .Set(i => i.DisplayName, "Fancy Product")
                .Set(i => i.Price, 99.95m)
            ));
            //{"Id":2,"DisplayName":"Fancy Product","Price":99.95}

            var targetType = PocoBuilder.GetTypeFor<IListProduct>();
            var deserialized = JsonSerializer.Deserialize(json, targetType) as IListProduct;
            Assert.IsNotNull(deserialized);
            Assert.IsTrue(deserialized.Id == 2);
            Assert.IsTrue(deserialized.Price == 99.95m);
            Assert.IsNotNull(deserialized.DisplayName);
            // Note: The JsonSerializer does not use the constructor to populate the data
            // beacause PocoBuilder does not decorate it with the JsonConstructorAttribute.

            // Instead it uses the init-only setters of the properties.
        }

        public interface IArticle 
        { 
            int Id { get; init; }
            public interface IPrice : IArticle { decimal Price { get; init; } }
            public interface IDisplayName : IArticle { string DisplayName { get; init; } }
            public interface ICategory : IArticle { string Category { get; init; } }
            public interface IDescription : IArticle { string Description { get; init; } }
        }
    }
}
