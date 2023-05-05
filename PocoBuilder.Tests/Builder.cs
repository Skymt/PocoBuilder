namespace PocoBuilder.Tests
{
    [TestClass]
    public class Builder
    {
        // PocoBuilder takes an interface and builds a proper class.
        // This leverages the power of combining several interfaces
        // to form a contract, without forcing the boiler-plate
        // of actually implementing all those interfaces.

        // Consider the following interfaces as a model description
        public interface IBaseModel
        {
            int Id { get; }
            string Name { get; set; }
        }
        public interface IPeripheral1 : IBaseModel
        {
            string Data1 { get; set; }
        }
        public interface IPeripheral2 : IBaseModel
        {
            Guid? Data2 { get; }
        }

        // They can then contextually be combined to represent a contract.
        // When testing, it is apropriate to use all of them, but when e.g.
        // writing a translation service for products, you would not need to include the prices
        // even though both descriptions and prices should be defined and known in a core assembly.
        public interface ITest1 : IBaseModel, IPeripheral1, IPeripheral2 { }
        [TestMethod]
        public void Test1_Initialization()
        {
            // To build (or fetch from memory cache) the poco type.   
            var interfaceAsClassType = PocoBuilder.GetTypeFor<ITest1>();

            // The new type has both an parameter-less constructor
            var emptyInstance = Activator.CreateInstance(interfaceAsClassType) as ITest1;
            Assert.IsNotNull(emptyInstance);
            Assert.IsNull(emptyInstance.Data1);

            // and a constructor for assigning values, to support immutability.
            var filledInstance = Activator.CreateInstance(interfaceAsClassType, 1, "Name", "Data1", Guid.NewGuid()) as ITest1;
            Assert.IsNotNull(filledInstance);
            Assert.IsNotNull(filledInstance.Data1);

            // It also creates all the properties, with getters and setters as defined by the interface.
            emptyInstance.Data1 = "This property just mutated...";
            Assert.IsNotNull(emptyInstance.Data1);

            // Immutable properties can of course only be read.
            var data2 = filledInstance.Data2;
            Assert.IsNotNull(data2);
        }

        [TestMethod]
        public void Test2_TypeSafeInitialization()
        {
            // Using Activator to pass the initial values as an array of objects is typically a bit unsafe.
            // PocoBuilder therefore offers this alternative, to get a type-safe activation.
            var filledInstance = PocoBuilder.CreateInstanceOf<ITest1>(init => init
                .Set(i => i.Id, 1)
                .Set(i => i.Name, "The name of the thingy")
                .Set(i => i.Data1, "Something else about the thingy")
                .Set(i => i.Data2, Guid.NewGuid())
            );
            // This technique can also be used to map foreign properties,
            // or expanded to take into account several data sources.
            Assert.IsNotNull(filledInstance);
            Assert.AreEqual(1, filledInstance.Id);
            Assert.IsNotNull(filledInstance.Data2);
        }

        // The interfaces the poco builder likes has some limitations.
        // They may not have conflicting properties.
        public interface IUnrelated1 { string Data1 { get; } }
        public interface IConflictingProperties : IPeripheral1, IUnrelated1 { }

        // Nor can they contain methods, even with default implementations.
        // While a type will be created, the method will not be carried over!
        public interface IContainsMethod { void DoSomething(); }
        [TestMethod]
        public void Test3_InvalidInterfaceDeclarations()
        {
            // Unrelated interfaces can serve a purpose, like ITableEntity, but
            // make sure no properties get duplicated!
            Assert.IsFalse(PocoBuilder.VerifyPocoInterface<IConflictingProperties>());
            // (The IDE should report this problem as well, when these properties are referenced)

            // Methods are also not allowed - the poco builder is not a compiler!
            Assert.IsFalse(PocoBuilder.VerifyPocoInterface<IContainsMethod>());
            // Instead, use base classes and/or extension methods.

        }

        // Base classes can be useful, especially when inheriting from
        // data-source specific entities, like TableEntity.
        // But extension methods are also a great way to extend the functionality
        // of the interfaces!
        public abstract class GenericBase
        {
            // No need to implement the interface properties
            public ITest1 Model => (ITest1)this;

            // Public parameter-less constructors are called when the constructed type is instantiated.
            public bool DefaultContructorCalled { get; } = true;

            // Custom contstructors are never called by the PocoBuilder
            public bool CustomConstructorCalled { get; } = false;

            public GenericBase() { DefaultContructorCalled = true; }
            public GenericBase(ITest1 _) { CustomConstructorCalled = true; }
            public bool NameIsSet => !string.IsNullOrEmpty(Model.Name);
        }

        [TestMethod]
        public void Test4_BaseClass()
        {
            // The builder returns two references to the same instance.
            var (asInterface, asParent) = PocoBuilder.CreateInstanceOf<ITest1, GenericBase>();
            Assert.AreEqual((object)asParent, (object)asInterface);
            Assert.IsInstanceOfType(asInterface, typeof(ITest1));
            Assert.IsInstanceOfType(asParent, typeof(GenericBase));

            Assert.IsTrue(asParent.DefaultContructorCalled);
            Assert.IsFalse(asParent.CustomConstructorCalled);

            Assert.IsFalse(asParent.NameIsSet);
            asInterface.Name = "This is a name";
            Assert.IsTrue(asParent.NameIsSet);
        }
    }
}
