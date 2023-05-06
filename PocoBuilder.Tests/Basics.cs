namespace PocoBuilder.Tests
{
    [TestClass]
    public class Basics
    {
        public interface IArticle { int Id { get; } }
        public interface IName : IArticle { string Name { get; init; } }
        public interface IDescription : IArticle { string Description { get; set; } }

        public interface IListProduct : IArticle, IName { }
        public interface IDetailProduct : IArticle, IName, IDescription { }

        [TestMethod]
        public void Test1_TypeGenerationAndInspection()
        {
            var type = PocoBuilder.GetTypeFor<IListProduct>();
            var constructors = type.GetConstructors();
            Assert.AreEqual(2, constructors.Length);

            var parameterizedConstructor = constructors.First(c => c.GetParameters().Length > 0);

            var expectedParameterOrder = new[] { "Id", "Name" };
            var parameters = parameterizedConstructor.GetParameters();

            Assert.AreEqual(expectedParameterOrder.Length, parameters.Length);
            Assert.AreEqual(expectedParameterOrder[0], parameters[0].Name);
            Assert.AreEqual(expectedParameterOrder[1], parameters[1].Name);
        }

        [TestMethod]
        public void Test2_TypeInstantiation() 
        {
            var type = PocoBuilder.GetTypeFor<IDetailProduct>();
            var instance = Activator.CreateInstance(type) as IDetailProduct;

            Assert.IsNotNull(instance);

            instance.Description = "A long and poetic text about a fancy product";
            Assert.IsNotNull(instance.Description);
        }

        [TestMethod]
        public void Test3_TypeInstantiationImmutableProperties()
        {
            var type = PocoBuilder.GetTypeFor<IDetailProduct>();
            var properties = type.GetProperties();
            var values = new object[properties.Length];
            for(int i = 0; i < properties.Length; i++)
            {
                values[i] = properties[i].Name switch
                {
                    nameof(IDetailProduct.Id) => 2,
                    nameof(IDetailProduct.Name) => "FancyProduct",
                    nameof(IDetailProduct.Description) => "A long and poetic text about a fancy product",
                    _ => throw new NotImplementedException()
                };
            }

            var instance = Activator.CreateInstance(type, values) as IDetailProduct;
            Assert.IsNotNull(instance);
            Assert.AreEqual(2, instance.Id);
            Assert.IsNotNull(instance.Name);
        }

        [TestMethod]
        public void Test4_InstantiationHelper()
        {
            var instance = PocoBuilder.CreateInstanceOf<IDetailProduct>(init => init
                .Set(i => i.Id, 2)
                .Set(i => i.Name, "Fancy product")
                .Set(i => i.Description, "A long and poetic text about a fancy product")
            );
            Assert.IsNotNull(instance);
            Assert.AreEqual(2, instance.Id);
            Assert.IsNotNull(instance.Name);
        }
    }
}
