
# PocoBuilder

PocoBuilder is a utility that can generate dynamic classes at runtime based on a provided interface. This is useful when you want your models to share a common set of properties defined in a core project, but still be agnostic regarding their usage.

Consider the following base models:

    public interface IArticle { int Id { get; init; } }
    public interface IName : IArticle { string Name { get; set; } }
    public interface IDescription : IArticle { string Description { get; set; } }

The implementing projects can then determine what contract they need

    public interface IListProduct : IArticle, IName { }
    public interface IDetailProduct : IArticle, IName, IDescription { }

PocoBuilder can then help create a class, instantiate and populate it with values, and e.g. use System.Text.Json.Serializer to stringify that object...

    var instance = PocoBuilder.CreateInstanceOf<IListProduct>(init => init
        .Set(i => i.Id, 2)
        .Set(i => i.Name, "Fancy Product")
    );
    string json = JsonSerializer.Serialize<object>(instance);

or to deserialize.

    string json = "{\"Id\":2,\"Name\":\"Fancy Product\"}";
    Type targetType = PocoBuilder.GetTypeFor<IListProduct>();
    var instance = JsonSerializer.Deserialize(json, targetType) as IListProduct;

## Usage
PocoBuilder can create a class, and return the System.Type for that class

    Type interfaceAsClassType = PocoBuilder.GetTypeFor<IDetailProduct>();

This type can then be used to create an instance of the interface using Activator.CreateInstance().

    var instance = Activator.CreateInstance(interfaceAsClassType) as IDetailProduct
    instance.Id = 2; instance.Name = "Fancy product";
    instance.Description = "A long and poetic text about a fancy product";

PocoBuilder also has a shorthand method that support setting immutable and read-only properties

    var instance = PocoBuilder.CreateInstanceOf<IListProduct>(init => init
        .Set(i => i.Id, 2)
        .Set(i => i.Name, "Fancy Product")
        .Set(i => i.Description, "A long and poetic text about a fancy product")
    );

PocoBuilder typically creates two constructors. The parameterized one can be reflected upon, to map values to properties in case System.Activator is preferable to PocoBuilder for instantiation.

    interfaceAsClassType.GetConstructors()[1].GetParameters()

The order is the same as the declaration of first properties, then parent interfaces.

    public interface IProductInEmail : IListProduct, IDescription
    {
        string UserTokenizedLink { get; init; }
    }
    var instance = Activator.CreateInstance(
        PocoBuilder.GetTypeFor<IProductInEmail>(),
        "www.UserTokenizedLink.com",
        2, // Id
        "Name",
        "Description"
    );

## Adding functionality
Both GetTypeFor() and CreateInstanceOf() can accept a parent class. If the parent class contains a public parameter-less constructor, this will be called upon activation of the generated type.

    public abstract class BaseClass
    {
        public IListProduct Model { get => (IListProduct)this; }
    }
    var type = PocoBuilder.GetTypeFor<IListModel, BaseClass>();

_Note: The CreateInstanceOf returns a tuple when specifying a base class. The references in the tuple points to the same instance, but as the types of the interface and the base class:_

    var (asInterface, asParent) = PocoBuilder.CreateInstanceOf<IListModel, BaseClass>();

Another way of adding functionality to your interfaces is through [extension methods](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods)

## Limitations
Poco classes typically contain only fields and properties. Interfaces cannot contain fields, thus a Poco interface may only contain properties.

PocoBuilder has a utility method to verify the POCOness of an interface

    public static bool VerifyPocoInterface<TInterface>() {...}

The properties can be mutable, immutable or read-only though.

    int? Mutable { get; set; }
    int? Immutable { get; init; }
    int? ReadOnly { get; }