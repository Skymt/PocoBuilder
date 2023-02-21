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
            backingFields[nameof(instance.ImmutableId)].SetValue(instance, 12345);
            backingFields[nameof(instance.MutableName)].SetValue(instance, "A name");
            backingFields[nameof(instance.Description)].SetValue(instance, "A description");
            Assert.IsNotNull(parent.Model.ImmutableId);
            Assert.IsNotNull(parent.Model.MutableName);
            Assert.IsNotNull(parent.Model.Description);
        }
        [TestMethod] public void Test2_ClonedDefaultParent()
        {
            (var instance, var parent) = CreateInstance<IModel, ModelFor<IModel>>(out var backingFields);
            backingFields[nameof(instance.ImmutableId)].SetValue(instance, 12345);
            backingFields[nameof(instance.MutableName)].SetValue(instance, "A name");
            backingFields[nameof(instance.Description)].SetValue(instance, "A description"); 
            
            var clone = parent.Clone(editor => editor
                .Set(m => m.ImmutableId, 12346)
                .Set(m => m.Description, "Another description")
            );
            Assert.AreNotEqual(parent, clone);
            Assert.AreEqual(parent.Model.MutableName, clone.Model.MutableName);
            Assert.IsNotNull(clone.Model.ImmutableId);
            Assert.AreNotEqual(parent.Model.ImmutableId, clone.Model.ImmutableId);

        }
        [TestMethod] public void Test3_EditableDefaultParent()
        {
            (var instance, var parent) = CreateInstance<IModel, ModelFor<IModel>>(out var backingFields);
            backingFields[nameof(instance.ImmutableId)].SetValue(instance, 12345);
            backingFields[nameof(instance.MutableName)].SetValue(instance, "A name");
            backingFields[nameof(instance.Description)].SetValue(instance, "A description");

            (parent as IEditable<IModel>).Edit()
                .Set(m => m.ImmutableId, 12346)
                .Set(m => m.Description, "Updated description");

            Assert.AreNotEqual(12345, parent.Model.ImmutableId);
            Assert.AreNotEqual("A description", parent.Model.Description);
        }

        public abstract class ModelParent
        {
            public IModel Model { get => (IModel)this; }
            public int Id { get => Model.ImmutableId; }
            public bool HasId => Model.ImmutableId > 0;
            public bool HasName => !string.IsNullOrEmpty(Model.MutableName);
        }
        [TestMethod] public void Test4_CustomParent1()
        {
            var (model, parent) = CreateInstance<IModel, ModelParent>(out var backingFields);
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