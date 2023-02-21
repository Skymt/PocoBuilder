using static PocoBuilder.PocoBuilder;

namespace PocoBuilder.Tests
{
    [TestClass]
    public class PocoBuilderWithParents
    {
        public interface IModel
        {
            int ImmutableId { get; }
            string MutableName { get; set; }
            string Description { get; }
        }
        
        [TestMethod] public void Test1_DefaultParent()
        {
            (var instance, var parent) = CreateInstance<IModel, ModelFor<IModel>>(out var backingFields);
            Assert.IsInstanceOfType(instance, typeof(IModel));
            Assert.IsInstanceOfType(parent, typeof(ModelFor<IModel>));

            backingFields[nameof(instance.ImmutableId)].SetValue(instance, 12345);
            backingFields[nameof(instance.Description)].SetValue(instance, "A description");
            instance.MutableName = "A name";

            Assert.IsNotNull(parent.Model.ImmutableId);
            Assert.IsNotNull(parent.Model.MutableName);
            Assert.IsNotNull(parent.Model.Description);
        }
        [TestMethod] public void Test2_EditableDefaultParent()
        {
            var poco = ModelFor<IModel>.Default();
            (poco as IEditable<IModel>).Edit()
                .Set(m => m.ImmutableId, 12345)
                .Set(m => m.MutableName, "A name")
                .Set(m => m.Description, "A description");

            Assert.IsNotNull(poco.Model.ImmutableId);
            Assert.IsNotNull(poco.Model.MutableName);
            Assert.IsNotNull(poco.Model.Description);
        }
        [TestMethod] public void Test3_ClonedDefaultParent()
        {
            var poco = ModelFor<IModel>.Default();
            (poco as IEditable<IModel>).Edit()
                .Set(m => m.ImmutableId, 12345)
                .Set(m => m.MutableName, "A name")
                .Set(m => m.Description, "A description");

            var clone = poco.Clone(editor => editor
                .Set(m => m.ImmutableId, 12346)
                .Set(m => m.Description, "Another description")
            );

            Assert.AreNotEqual(poco, clone);
            Assert.AreEqual(poco.Model.MutableName, clone.Model.MutableName);
            Assert.AreNotEqual(poco.Model.ImmutableId, clone.Model.ImmutableId);
            Assert.IsNotNull(clone.Model.ImmutableId);
            Assert.AreNotEqual(poco.Model.Description, clone.Model.Description);
            Assert.IsNotNull(clone.Model.Description);
        }
        

        public abstract class ModelParent
        {
            public IModel MyModel { get => (IModel)this; }
            public int Id { get => MyModel.ImmutableId; }
            public bool HasId => MyModel.ImmutableId > 0;
            public bool HasName => !string.IsNullOrEmpty(MyModel.MutableName);
        }
        [TestMethod] public void Test4_CustomParent1()
        {
            var (model, parent) = CreateInstance<IModel, ModelParent>(out var backingFields);
            Assert.IsInstanceOfType(model, typeof(IModel));
            Assert.IsInstanceOfType(parent, typeof(ModelParent));
            Assert.AreEqual(model, parent);
            Assert.IsNotNull(parent.MyModel);

            Assert.IsFalse(parent.HasName);
            model.MutableName = "A name";
            Assert.IsTrue(parent.HasName);

            Assert.IsFalse(parent.HasId);
            backingFields[nameof(model.ImmutableId)].SetValue(model, 12345);
            Assert.IsTrue(parent.HasId);
        }
        [TestMethod] public void Test5_CustomParent2()
        {
            var template = ModelFor<IModel>.Default();
            (template as IEditable<IModel>).Edit().Set(m => m.ImmutableId, 12345);

            var newModel = template.Clone<ModelParent>();
            Assert.IsInstanceOfType(newModel, typeof(ModelParent));
            Assert.AreEqual(template.Model.ImmutableId, newModel.Id);
        }


        public interface IPeripheral1 : IModel { public string Data1 { get; } }
        public interface IPeripheral2 : IModel { public string Data2 { get; } }
        public abstract class ComplexParent : ModelFor<ComplexParent.IComposite, ComplexParent>
        {
            public interface IComposite : IModel, IPeripheral2, IPeripheral1 { }
            public static ComplexParent CreateInstance(int id)
            {
                var instance = CreateInstance();
                instance.BackingFields[nameof(IModel.ImmutableId)].SetValue(instance.Model, id);
                return instance;
            }
            public string? Data 
            { 
                get => Model.Data1; 
                set => BackingFields[nameof(IPeripheral1.Data1)].SetValue(this, value); 
            }
            public void SetAlternateData(string data)
                => BackingFields[nameof(IPeripheral2.Data2)].SetValue(this, data);
        }
        [TestMethod] public void Test6_ComplexParent()
        {
            var parent = ComplexParent.CreateInstance(id: 12345);
            Assert.IsNotNull(parent.Model);
            Assert.AreEqual(12345, parent.Model.ImmutableId);

            parent.Model.MutableName = "A name";
            Assert.IsNotNull(parent.Model.MutableName);

            parent.Data = "A slogan";
            Assert.IsNotNull(parent.Model.Data1);

            parent.SetAlternateData("Some numbers");
            Assert.IsNotNull(parent.Model.Data2);
        }
    }
}