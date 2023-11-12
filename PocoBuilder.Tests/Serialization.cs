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

    public interface ISimple { int Index { get; } }
    public interface IGeneric<T> { T Value { get; init; } }

    [TestMethod]
    public void Test1_Serialization()
    {
        var detailedProduct = DTOBuilder.CreateInstanceOf<IDetailProduct>(init => init
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

        var listProduct = DTOBuilder.CreateInstanceOf<IListProduct>(init => init
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
        var json = JsonSerializer.Serialize<object>(DTOBuilder.CreateInstanceOf<ICustomArticle>(init => init
            .Set(i => i.ArticleId, 2)
            .Set(i => i.Name, "Fancy Product")
            .Set(i => i.Price, 99.95m)
            .Set(i => i.SampleReadOnlyProperty, "This is a value!")
        ));
        //{"SampleReadOnlyProperty":"This is a value!","ArticleId":2,"Name":"Fancy Product","Price":99.95}

        var targetType = DTOBuilder.GetTypeFor<ICustomArticle>();
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
    public void Test3_Converters()
    {
        var jsonOptions = new JsonSerializerOptions();
        // DTOConverters handles the transformation between
        // the interface type and the generated class type.
        jsonOptions.Converters.Add(new DTOConverter<ICustomArticle>());

        var listProduct = DTOBuilder.CreateInstanceOf<ICustomArticle>(init => init
            .Set(i => i.ArticleId, 2)
            .Set(i => i.Name, "Fancy Product")
            .Set(i => i.Price, 99.95m)
            .Set(i => i.SampleReadOnlyProperty, "TADAA!")
        );

        // Now you can serialize without <object>
        var json = JsonSerializer.Serialize(listProduct, jsonOptions);
        Assert.IsFalse(string.IsNullOrWhiteSpace(json));

        // And you can use your desired target interface-type as
        // generic argument when deserializing.
        var instance = JsonSerializer.Deserialize<ICustomArticle>(json, jsonOptions);
        Assert.IsNotNull(instance);
        Assert.IsInstanceOfType<ICustomArticle>(instance);

        // This also overcomes the limitation with read-only properties
        Assert.AreEqual("TADAA!", instance.SampleReadOnlyProperty);
    }

    [TestMethod]
    public void Test4_MoreAboutConverters()
    {
        // Converters are quite versitile and honors the options they are
        // bundled with.
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Registering converters for collection types is only relevant
        // when they are generic arguments. Collections are supported
        // anyway.
        jsonOptions.Converters.Add(new DTOConverter<ISimple>());
        jsonOptions.Converters.Add(new DTOConverter<IGeneric<ISimple>>());
        jsonOptions.Converters.Add(new DTOConverter<IGeneric<ISimple[]>>());

        string json = "{\"index\":2}"; // Case insensitivity is handled
        var simple = JsonSerializer.Deserialize<ISimple>(json, jsonOptions);
        Assert.IsNotNull(simple); Assert.AreEqual(2, simple.Index);

        json = "[{\"INDEX\":3},{\"index\":4},{\"InDeX\":5}]"; // Collections are supported.
        var simpleArr = JsonSerializer.Deserialize<ISimple[]>(json, jsonOptions);
        Assert.IsNotNull(simpleArr); Assert.AreEqual(4, simpleArr[1].Index);

        json = "{\"Value\":{\"Index\":6}}"; // Complex generic types are a-ok
        var generic = JsonSerializer.Deserialize<IGeneric<ISimple>>(json, jsonOptions);
        Assert.IsNotNull(generic); Assert.AreEqual(6, generic.Value.Index);

        json = "[{\"value\":{\"Index\":7}},{\"value\":{\"Index\":8}}]"; // and they can come as arrays as well.
        var genericArr = JsonSerializer.Deserialize<IGeneric<ISimple>[]>(json, jsonOptions);
        Assert.IsNotNull(genericArr); Assert.AreEqual(8, genericArr[1].Value.Index);

        // A separate converter registration is required if the generic
        // type is a collection type though! Calling GetTypeFor<ISimple[]>()
        // would result in an exception but GetTypeFor<IGeneric<ISimple[]>>()
        // is fine.
        json = "{\"valuE\":[{\"index\":9},{\"index\":10},{\"index\":11}]}";
        var genericOfArr = JsonSerializer.Deserialize<IGeneric<ISimple[]>>(json, jsonOptions);
        Assert.IsNotNull(genericOfArr); Assert.AreEqual(10, genericOfArr.Value[1].Index);
    }
}
