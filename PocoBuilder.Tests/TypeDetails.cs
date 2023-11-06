using NuGet.Frameworks;

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
        // These types can be activated and populated with values.
        var type = DTOBuilder.GetTypeFor<IListProduct>();
        var properties = type.GetProperties();

        // The generated type supports immutability.
        // The proper way of assigning values to them is during activation.
        // The generated type therefore contains two constructors;
        // * one with no parameters
        // * one with parameters that matches all defined properties
        var constructors = type.GetConstructors();
        Assert.AreEqual(2, constructors.Length);

        var parameterizedConstructor = constructors.First(c => c.GetParameters().Length > 0);
        var parameters = parameterizedConstructor.GetParameters();
        Assert.AreEqual(properties.Length, parameters.Length);
        Assert.AreEqual(properties[0].Name, parameters[0].Name);
        Assert.AreEqual(properties[1].Name, parameters[1].Name);


        var expectedParameterOrder = new[] { "ArticleId", "Name" };
        Assert.AreEqual(expectedParameterOrder.Length, parameters.Length);
        Assert.AreEqual(expectedParameterOrder[0], parameters[0].Name);
        Assert.AreEqual(expectedParameterOrder[1], parameters[1].Name);

        // Since keeping track of this constructor signature is important
        // DTOBuilder comes with some utils to make it easier.
        // See the tests Templates and Factory for more details.

        // The rest of the tests here will focus on reflection.

        // HINT: A convenient shorthand to type safe setting of
        // immutable and read-only properties is by using the
        // instantiation helper directly in the builder:
        var instance = DTOBuilder.CreateInstanceOf<IListProduct>(init => init
            .Set(m => m.ArticleId, 1)
            .Set(m => m.Name, "The product name")
        );
        Assert.IsNotNull(instance);
        Assert.AreEqual(1, instance.ArticleId);
    }

    [TestMethod]
    public void Test2_PropertyOrder()
    {
        var type = DTOBuilder.GetTypeFor<IDetailProduct>();
        var properties = type.GetProperties();

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
