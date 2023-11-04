
# DTOBuilder

DTOBuilder is a utility that can generate dynamic DTO (Data Transfer Object) classes at runtime based on a provided interface. This is useful when you want your models to share a common set of properties defined in a core project, but still be agnostic regarding their usage.

These classes can be used for more than just data transfer, but I hesitate to call them POCO, since they aren't really plain, being generated at runtime and all.

Consider the following base models:

    public interface IArticle { int Id { get; init; } }
    public interface IName : IArticle { string Name { get; set; } }
    public interface IDescription : IArticle { string Description { get; set; } }

The implementing projects can then determine what contract they need

    public interface IListProduct : IArticle, IName { }
    public interface IDetailProduct : IArticle, IName, IDescription { }

DTOBuilder can then help create a class, instantiate and populate it with values, and e.g. use System.Text.Json.Serializer to stringify that object...

    var instance = DTOBuilder.CreateInstanceOf<IListProduct>(init => init
        .Set(i => i.Id, 2)
        .Set(i => i.Name, "Fancy Product")
    );
    string json = JsonSerializer.Serialize<object>(instance);

or to deserialize.

    string json = "{\"Id\":2,\"Name\":\"Fancy Product\"}";
    Type targetType = DTOBuilder.GetTypeFor<IListProduct>();
    var instance = JsonSerializer.Deserialize(json, targetType) as IListProduct;

## Usage
DTOBuilder can create a class, and return the System.Type for that class

    Type interfaceAsClassType = DTOBuilder.GetTypeFor<IDetailProduct>();

This type can then be used to create an instance of the interface using Activator.CreateInstance().

    var instance = Activator.CreateInstance(interfaceAsClassType) as IDetailProduct
    instance.Id = 2; instance.Name = "Fancy product";
    instance.Description = "A long and poetic text about a fancy product";

DTOBuilder also has a shorthand method that support setting immutable and read-only properties

    var instance = DTOBuilder.CreateInstanceOf<IListProduct>(init => init
        .Set(i => i.Id, 2)
        .Set(i => i.Name, "Fancy Product")
        .Set(i => i.Description, "A long and poetic text about a fancy product")
    );

DTOBuilder creates two constructors. The parameterized one can be reflected upon, to map values to properties in case System.Activator is preferable to DTOBuilder for instantiation.

    interfaceAsClassType.GetConstructors()[1].GetParameters()

The order is the same as the declaration of first properties, then parent interfaces.

    public interface IProductInEmail : IListProduct, IDescription
    {
        string UserTokenizedLink { get; init; }
    }
    var instance = Activator.CreateInstance(
        DTOBuilder.GetTypeFor<IProductInEmail>(),
        "www.UserTokenizedLink.com",
        2, // Id
        "Name",
        "Description"
    );

## Adding functionality
[Default implementations](https://devblogs.microsoft.com/dotnet/default-implementations-in-interfaces/) are now possible in interfaces.

Another way of adding functionality to your interfaces is through [extension methods](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods)
