namespace PocoBuilder.Tests;

[TestClass]
public class ParentClasses
{
    public interface IDetailProduct : IName, IArticle, IPrice, ICategory, IDescription { }
    public abstract class GenericBase
    {
        // No need to implement the interface properties
        public IDetailProduct Model => (IDetailProduct)this;

        // Public parameter-less constructors are called when the constructed type is instantiated.
        public bool DefaultContructorCalled { get; } = false;

        // Custom contstructors are never called by the PocoBuilder
        public bool CustomConstructorCalled { get; } = false;

        public GenericBase() { DefaultContructorCalled = true; }
        public GenericBase(IDetailProduct _) { CustomConstructorCalled = true; }
        public bool HasCategory => !string.IsNullOrEmpty(Model.Category);
    }

    [TestMethod]
    public void Test1_BaseClass()
    {
        // The builder returns two references to the same instance.
        var (asInterface, asParent) = DTOBuilder.CreateInstanceOf<IDetailProduct, GenericBase>();
        Assert.AreEqual(asParent, asInterface as object);
        Assert.IsInstanceOfType(asInterface, typeof(IDetailProduct));
        Assert.IsInstanceOfType(asParent, typeof(GenericBase));

        Assert.IsTrue(asParent.DefaultContructorCalled);
        Assert.IsFalse(asParent.CustomConstructorCalled);

        Assert.IsFalse(asParent.HasCategory);
        asInterface.Category = "Unsorted";
        Assert.IsTrue(asParent.HasCategory);
    }
}