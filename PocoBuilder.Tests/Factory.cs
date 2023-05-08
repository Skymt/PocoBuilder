namespace PocoBuilder.Tests
{
    [TestClass]
    public class Factory
    {
        public interface IListProduct : IArticle, IName, IPrice, ICategory { }

        [TestMethod]
        public void Test1_Factory()
        {
            // Factories can be thought of as a mutatable version of what-ever poco interface
            // they are provided with.
            var factory = new PocoFactory<IListProduct>();
            factory.Set(i => i.Name, "Fancy product")
                .Set(i => i.Category, "Unsorted")
                .Set(i => i.ArticleId, 5)
                .Set(i => i.Price, 99.50m)
            ;

            // When the values are set to your liking, an instance of the interface can be activated.
            var instance1 = factory.CreateInstance();
            Assert.AreEqual("Unsorted", instance1.Category);

            // For convenience, other ways of setting the values can be used.
            factory[nameof(IListProduct.Name)] = "Fancy product v.2";
            factory[nameof(IListProduct.ArticleId)] = 6;
            factory[nameof(IListProduct.Price)] = 95.5m;
            var instance2 = factory.CreateInstance();

            Assert.AreEqual(6, instance2.ArticleId);
            Assert.AreEqual(instance1.Category, instance2.Category);
            Assert.AreNotEqual(instance1.Price, instance2.Price);

            // Or even dynamic. But take care not to mix up the types!
            dynamic shenanigans = factory;
            shenanigans.Name = "Fancy product v.3";
            shenanigans.ArticleId++;
            shenanigans.Price = 25; // Uh oh, this should be a decimal, not an int!
            
            // ONLY THE PocoFactory.Set() METHOD IS TYPESAFE!

            Assert.ThrowsException<MissingMethodException>(() =>
            {
                // Wrong signature of the parameters means there is no correct constructor. 
                var instance = factory.CreateInstance();
            });



        }
    }
}
