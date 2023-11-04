using NuGet.Frameworks;

namespace PocoBuilder.Tests;

[TestClass]
public class Basics
{
    public interface IListProduct : IArticle, IName { }
    public interface IDetailProduct : IListProduct, IDescription
    {
        string CustomProperty { get; init; }
    }

    [TestMethod]
    public void Test1_TypeGenerationAndInspection()
    {
        var type = DTOBuilder.GetTypeFor<IListProduct>();
        var properties = type.GetProperties();

        var constructors = type.GetConstructors();
        Assert.AreEqual(2, constructors.Length);

        var parameterizedConstructor = constructors.First(c => c.GetParameters().Length > 0);
        var parameters = parameterizedConstructor.GetParameters();
        Assert.AreEqual(properties.Length, parameters.Length);
        Assert.AreEqual(properties[0].Name, parameters[0].Name);
        Assert.AreEqual(properties[1].Name, parameters[1].Name);

        // Hint: The order of the properties, matches the signature
        // of the parameterized constructor.
        // NOTE: This might not be true when using parent classes.
        // A safer way to get the constructor parameters is by
        // reflecting on the constructor!
        // The safest way is to use the provided helper methods
        // (see test 5).

        var expectedParameterOrder = new[] { "ArticleId", "Name" };
        Assert.AreEqual(expectedParameterOrder.Length, parameters.Length);
        Assert.AreEqual(expectedParameterOrder[0], parameters[0].Name);
        Assert.AreEqual(expectedParameterOrder[1], parameters[1].Name);
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

    [TestMethod]
    public void Test5_InstantiationHelper()
    {
        var instance = DTOBuilder.CreateInstanceOf<IDetailProduct>(init => init
            .Set(i => i.ArticleId, 2)
            .Set(i => i.CustomProperty, "A custom value")
        );
        Assert.IsNotNull(instance);
        Assert.AreEqual(2, instance.ArticleId);
        Assert.IsNotNull(instance.CustomProperty);

        // Note: Uninitialized properties will have their default values.
        Assert.IsNull(instance.Name);
    }
}
