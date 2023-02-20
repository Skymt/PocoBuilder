namespace PocoBuilder.Tests
{
    [TestClass]
    public class PocoBuilderWithParents
    {
        public interface IModel
        {
            int ImmutableId { get; }
            string MutableName { get; set; }
        }
        public abstract class ModelParent
        {
            public IModel Model { get => (IModel)this; }
            public bool HasId => Model.ImmutableId > 0;
            public bool HasName => !string.IsNullOrEmpty(Model.MutableName);
        }
        [TestMethod] public void Test1_ModelParent()
        {
            var (model, parent) = PocoBuilder.CreateInstance<IModel, ModelParent>(out var backingFields);
            Assert.IsInstanceOfType(model, typeof(IModel));
            Assert.IsInstanceOfType(parent, typeof(ModelParent));
            Assert.AreEqual(model, parent);
            Assert.IsNotNull(parent.Model);

            Assert.IsFalse(parent.HasName);
            model.MutableName = "A name";
            Assert.IsTrue(parent.HasName);

            Assert.IsFalse(parent.HasId);
            backingFields[nameof(model.ImmutableId)].SetValue(model, 12345);
            Assert.IsTrue(parent.HasId);
        }
        [TestMethod] public void Test2_DefaultParent()
        {
            var parent = PocoBuilder.ModelFor<IModel>.CreateInstance(populator: (instance, fields) =>
            {
                instance.MutableName = "A name";
                fields[nameof(instance.ImmutableId)].SetValue(instance, 12345);
            });
            Assert.IsInstanceOfType(parent.Model, typeof(IModel));
            Assert.IsInstanceOfType((IModel)parent, typeof(IModel));

            Assert.IsNotNull(parent.Model.ImmutableId);
            Assert.IsNotNull(parent.Model.MutableName);
        }
        [TestMethod] public void Test3_EditableDefaultParent()
        {
            var parent = PocoBuilder.EditableModelFor<IModel>.CreateInstance();
            Assert.IsInstanceOfType(parent.Model, typeof(IModel));
            Assert.IsInstanceOfType((IModel)parent, typeof(IModel));

            parent.Set(m => m.ImmutableId, 12345).Set(m => m.MutableName, "A name");
            Assert.IsNotNull(parent.Model.ImmutableId);
            Assert.IsNotNull(parent.Model.MutableName);
        }

        public interface IPeripheral1 : IModel { public string Data1 { get; } }
        public interface IPeripheral2 : IModel { public string Data2 { get; } }
        public abstract class BasicParent : PocoBuilder.ModelFor<BasicParent.IComposite, BasicParent>
        {
            public interface IComposite : IModel, IPeripheral2, IPeripheral1 { }
            public static BasicParent CreateInstance(int? id)
            {
                var instance = CreateInstance();
                if (id.HasValue) 
                    instance.BackingFields[nameof(IModel.ImmutableId)].SetValue(instance.Model, id);
                else 
                    throw new Exception();
                return instance;
            }
            public string? Data 
            { 
                get => Model.Data1; 
                set => BackingFields[nameof(IPeripheral1.Data1)].SetValue(this, value); 
            }
            public void SetData2(string data)
                => BackingFields[nameof(IPeripheral2.Data2)].SetValue(this, data);
        }
        [TestMethod] public void Test4_BasicParent()
        {
            var parent = BasicParent.CreateInstance(id: 12345);
            Assert.IsNotNull(parent.Model);
            Assert.AreEqual(12345, parent.Model.ImmutableId);

            parent.Model.MutableName = "A name";
            Assert.IsNotNull(parent.Model.MutableName);

            parent.Data = "A slogan";
            Assert.IsNotNull(parent.Model.Data1);

            parent.SetData2("Some numbers");
            Assert.IsNotNull(parent.Model.Data2);
        }
    }
}