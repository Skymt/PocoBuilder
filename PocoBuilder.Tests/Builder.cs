namespace PocoBuilder.Tests
{
    [TestClass]
    public class Builder
    {
        public interface IModel
        {
            int Id { get; }
            string Name { get; set; }
        }
        public interface IPeripheral1 : IModel
        {
            string Data1 { get; set; }
        }
        public interface IPeripheral2 : IModel
        {
            string Data2 { get; }
        }

        public interface ITest1 : IModel, IPeripheral1, IPeripheral2 { }
        [TestMethod]
        public void Test1_MutableProperties()
        {
            Assert.IsTrue(PocoBuilder.VerifyPocoInterface<ITest1>());

            var instance = PocoBuilder.CreatePocoInstance<ITest1>(out var _);
            Assert.IsNotNull(instance);
            Assert.IsInstanceOfType(instance, typeof(ITest1));

            instance.Name = "Test";
            Assert.AreEqual("Test", instance.Name);

            instance.Data1 = "Arbitrary information about the model";
            Assert.IsNotNull(instance.Data1);
        }

        public interface IUnrelated1 { string This { get; } }
        public interface IUnrelated2 { string That { get; } }
        public interface ITest2<T> : IModel, IUnrelated1, IUnrelated2
        {
            T Value { get; }
        }
        [TestMethod]
        public void Test2_ImmutableProperties()
        {
            Assert.IsTrue(PocoBuilder.VerifyPocoInterface<ITest2<int>>());

            var instance = PocoBuilder.CreatePocoInstance<ITest2<int>>(out var fields);
            Assert.IsNotNull(instance);
            Assert.IsInstanceOfType(instance, typeof(ITest2<int>));
            Assert.IsInstanceOfType(instance, typeof(IUnrelated1));
            Assert.IsInstanceOfType(instance, typeof(IUnrelated2));

            fields[nameof(instance.Id)].SetValue(instance, 12345);
            fields[nameof(instance.Value)].SetValue(instance, 6);
            fields[nameof(instance.Name)].SetValue(instance, "Test");
            fields[nameof(instance.This)].SetValue(instance, "Arbitrary information about the model");
            fields[nameof(instance.That)].SetValue(instance, "Arbitrary information about the model");

            Assert.AreEqual(12345, instance.Id);
            Assert.AreEqual("Test", instance.Name);
            Assert.IsTrue(instance.Value == 6);
            Assert.IsNotNull(instance.This);
            Assert.IsNotNull(instance.That);
        }

        public interface IUnrelated3 { string That { get; } }
        public interface IConflictingProperties : IUnrelated1, IUnrelated2, IUnrelated3 { }
        public interface IMethod { void DoSomething(); }
        [TestMethod]
        public void Test3_InvalidInterfaceDeclarations()
        {
            Assert.IsFalse(PocoBuilder.VerifyPocoInterface<IConflictingProperties>());
            Assert.IsFalse(PocoBuilder.VerifyPocoInterface<IMethod>());
        }
    }
}
