using System.Text.Json;
namespace PocoBuilder.Tests;

[TestClass]
public class Serialization
{
    public interface IDetailProduct : IName, IArticle, IPrice, ICategory, IDescription { }
    public interface IListProduct : IArticle, IName, IPrice { }
    public interface ICustomArticle : IListProduct
    {
        string SampleReadOnlyProperty { get; }
    }
    [TestMethod]
    public void Test1_Serialization()
    {
        var detailedProduct = PocoBuilder.CreateInstanceOf<IDetailProduct>(init => init
            .Set(i => i.ArticleId, 2)
            .Set(i => i.Price, 99.95m)
            .Set(i => i.Name, "Fancy Product")
            .Set(i => i.Description, "Eloquent Description")
            .Set(i => i.Category, "Logical Category")
        );
        var json = JsonSerializer.Serialize<object>(detailedProduct);
        //{"Name":"Fancy Product","ArticleId":2,"Price":99.95,"Category":"Logical Category","Description":"Eloquent Description"}
        // Note: Serializer gets confused by interfaces not having their getters implemented,
        // so it needs to get an indication that it actually is an object that it is serializing.

        Assert.IsFalse(string.IsNullOrWhiteSpace(json));
        Assert.AreNotEqual("{}", json);

        var listProduct = PocoBuilder.CreateInstanceOf<IListProduct>(init => init
            .Set(i => i.ArticleId, 2)
            .Set(i => i.Name, "Fancy Product")
            .Set(i => i.Price, 99.95m)
        );
        json = JsonSerializer.Serialize<object>(listProduct);
        // Note: The order of the properties matches the order of the interface declarations.
        //{"ArticleId":2,"Name":"Fancy Product","Price":99.95}
        Assert.IsFalse(string.IsNullOrWhiteSpace(json));
        Assert.AreNotEqual("{}", json);
    }

    [TestMethod]
    public void Test2_Deserialization()
    {
        var json = JsonSerializer.Serialize<object>(PocoBuilder.CreateInstanceOf<ICustomArticle>(init => init
            .Set(i => i.ArticleId, 2)
            .Set(i => i.Name, "Fancy Product")
            .Set(i => i.Price, 99.95m)
            .Set(i => i.SampleReadOnlyProperty, "This is a value!")
        ));
        //{"SampleReadOnlyProperty":"This is a value!","ArticleId":2,"Name":"Fancy Product","Price":99.95}

        var targetType = PocoBuilder.GetTypeFor<ICustomArticle>();
        var deserialized = JsonSerializer.Deserialize(json, targetType) as ICustomArticle;

        Assert.IsNotNull(deserialized);
        Assert.IsTrue(deserialized.ArticleId == 2);
        Assert.IsTrue(deserialized.Price == 99.95m);
        Assert.IsNotNull(deserialized.Name);

        // Note: JsonSerializer.Deserialize() does not use the
        // parameterized constructor, and thusly cannot initialize
        // read-only properties.
        // (This is because PocoBuilder does not annotate with the JsonConstructorAttribute)
        Assert.IsNull(deserialized.SampleReadOnlyProperty);
    }

    [TestMethod]
    public void Test3_DeserializationGeneric()
    {
        var json = JsonSerializer.Serialize<object>(PocoBuilder.CreateInstanceOf<ICustomArticle>(init => init
            .Set(i => i.ArticleId, 2)
            .Set(i => i.Name, "Fancy Product")
            .Set(i => i.Price, 99.95m)
            .Set(i => i.SampleReadOnlyProperty, "This is a value!")
        ));
        //{"SampleReadOnlyProperty":"This is a value!","ArticleId":2,"Name":"Fancy Product","Price":99.95}
        var targetType = PocoBuilder.GetTypeFor<ICustomArticle>();

        // Calling the generic version of Deserialize (not sure why anyone'd want to do this):
        var genericDerializerMethods = typeof(JsonSerializer).GetMethods().Where(m => m.Name == nameof(JsonSerializer.Deserialize) && m.IsGenericMethod);
        foreach (var method in genericDerializerMethods)
        {
            if (method!.GetParameters().First().ParameterType == typeof(string))
            {
                var instance = method.MakeGenericMethod(targetType).Invoke(null, new[] { json, null });
                // Above line is equal to: var instance = JsonSerializer.Deserialize<CustomArticle>(json, options: null);

                Assert.IsInstanceOfType(instance, targetType);
                Assert.AreEqual(2, ((ICustomArticle)instance).ArticleId);
                break;
            }
        }
    }
}
