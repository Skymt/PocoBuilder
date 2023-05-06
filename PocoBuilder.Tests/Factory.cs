using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PocoBuilder.Tests.Builder;

namespace PocoBuilder.Tests
{
    [TestClass]
    public class Factory
    {
        public interface IListProduct : IArticle, IName, IPrice, ICategory { }

        [TestMethod]
        public void Test1_Factory()
        {
            var factory = new PocoFactory<IListProduct>();
            factory
                .Set(i => i.Name, "The name of the thingy")
                .Set(i => i.Category, "Unsorted")
                .Set(i => i.ArticleId, 5)
                .Set(i => i.Price, 99.50m)
            ;

            var instance1 = factory.CreateInstance();
            Assert.AreEqual("Unsorted", instance1.Category);


            dynamic shenanigans = factory;
            shenanigans.Price = 98.5m;
            shenanigans.Id++;
            
            var instance2 = factory.CreateInstance();

            Assert.AreEqual(6, instance2.ArticleId);
            Assert.AreEqual(instance1.Category, instance2.Category);
            Assert.AreNotEqual(instance1.Price, instance2.Price);

            factory[nameof(IListProduct.Price)] = 49; // NOTE! ONLY THE Set() METHOD IS TYPESAFE!
            Assert.ThrowsException<MissingMethodException>(() =>
            {
                var instance3 = factory.CreateInstance();
            });
        }
    }
}
