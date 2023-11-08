namespace PocoBuilder.Tests;

[TestClass]
public class TypeDetails
{
    public interface IListProduct : IArticle, IName { }
    public interface IDetailProduct : IListProduct, IDescription
    {
        string CustomProperty { get; init; }
    }

    [TestMethod]
    public void Test1_TypeGenerationAndInspection()
    {
        // DTOBuilder creates class types from interface types.
        // These types can be activated and the properties of
        // that instance can be populated with values.
        var type = DTOBuilder.GetTypeFor<IListProduct>();
        var instance = Activator.CreateInstance(type);
        var properties = type.GetProperties();
        properties.First(p => p.Name == nameof(IListProduct.Name)).SetValue(instance, "This is a name");
        // ( BIG NOTE: You'd think reflection on init-only properties would be protected somehow.
        // Seems no one cared though :( https://github.com/dotnet/runtime/issues/11811.)
        


        // The generated type supports immutability.
        // The proper way of assigning values to them is during activation.
        // The generated type therefore contains two constructors;
        // * one with no parameters
        // * one with parameters that matches all defined non-protected properties
        var constructors = type.GetConstructors();
        Assert.AreEqual(2, constructors.Length);

        // Since keeping track of this constructor signature is important
        // DTOBuilder comes with some utils to make it easier.
        // See the tests Templates and Factory for more details.

        // HINT: A convenient shorthand to type safe setting of
        // immutable and read-only properties is by using the
        // instantiation helper directly in the builder:
        var sampleInstance = DTOBuilder.CreateInstanceOf<IListProduct>(init => init
            .Set(m => m.ArticleId, 1)
            .Set(m => m.Name, "The product name")
        );
        // The rest of the tests here will focus on reflection.

        var parameterizedConstructor = constructors.First(c => c.GetParameters().Length > 0);
        var parameters = parameterizedConstructor.GetParameters();

        // As mentioned - the constructor signature matches the declared properties
        Assert.AreEqual(properties.Length, parameters.Length);
        for(int i = 0; i < parameters.Length; i++)
            Assert.AreEqual(properties[i].Name, parameters[i].Name);
    }

    [TestMethod]
    public void Test2_PropertyOrder()
    {
        var type = DTOBuilder.GetTypeFor<IDetailProduct>();
        var properties = type.GetProperties();

        // Please take a moment to reflect on how this order of properties matches the interface declaration.
        var expectedProperties = new[] { "CustomProperty", "ArticleId", "Name", "Description" };

        foreach (var (expected, actual) in expectedProperties.Zip(properties))
        {
            Assert.AreEqual(expected, actual.Name);
        }
    }

    [TestMethod]
    public void Test3_TypeInstantiation()
    {
        var type = DTOBuilder.GetTypeFor<IDetailProduct>();
        var instance = Activator.CreateInstance(type) as IDetailProduct;

        Assert.IsNotNull(instance);

        // Only mutable properties can be set after activation.
        instance.Description = "A long and poetic text about a fancy product";
        Assert.IsNotNull(instance.Description);
    }

    [TestMethod]
    public void Test4_TypeInstantiationReadonlyProperties()
    {
        var type = DTOBuilder.GetTypeFor<IDetailProduct>();
        var properties = type.GetProperties();
        var values = new object[properties.Length];
        for (int i = 0; i < properties.Length; i++)
        {
            values[i] = properties[i].Name switch
            {
                nameof(IDetailProduct.ArticleId) => 2,
                nameof(IDetailProduct.Name) => "FancyProduct",
                nameof(IDetailProduct.Description) => "A long and poetic text about a fancy product",
                nameof(IDetailProduct.CustomProperty) => "A custom value",
                _ => throw new NotImplementedException()
            };
        }

        var instance = Activator.CreateInstance(type, values) as IDetailProduct;
        Assert.IsNotNull(instance);
        Assert.AreEqual(2, instance.ArticleId);
        Assert.AreEqual("FancyProduct", instance.Name);
        Assert.IsNotNull(instance.Description);
        Assert.IsNotNull(instance.CustomProperty);
    }
}