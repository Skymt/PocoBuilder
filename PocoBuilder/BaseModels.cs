using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PocoBuilder
{
    public partial class PocoBuilder
    {
        private interface IModelBase { void SetBackingFields(IDictionary<string, FieldInfo> fields); }
        public abstract class ModelFor<TModel> : IModelBase
            where TModel : class
        {
            private IDictionary<string, FieldInfo>? backingFields;
            protected IDictionary<string, FieldInfo> BackingFields => backingFields ?? throw new Exception();
            public TModel Model => this as TModel ?? throw new Exception();
            void IModelBase.SetBackingFields(IDictionary<string, FieldInfo> fields) => backingFields = fields;
            public static ModelFor<TModel> CreateInstance(Action<TModel, IDictionary<string, FieldInfo>>? populator = null)
            {
                var instance = CreateInstance<TModel, ModelFor<TModel>>(out var backingFields).parent;
                populator?.Invoke(instance.Model, backingFields);
                return instance;
            }
        }
        public abstract class ModelFor<TModel, TParent> : ModelFor<TModel>
            where TParent : ModelFor<TModel, TParent>
            where TModel : class
        {
            public static TParent CreateInstance() => CreateInstance<TModel, TParent>(out var _).parent;
        }

        public abstract class EditableModelFor<TModel> : ModelFor<TModel>
            where TModel : class
        {
            static readonly Regex matchPropertyName = new(@"\.([^.,]*)", RegexOptions.Compiled);
            public static string MatchPropertyName(string s) => matchPropertyName.Match(s).Groups[1].Value;
            public EditableModelFor<TModel> Set<TValue>(Expression<Func<TModel, TValue>> property, TValue value)
            {
                var name = MatchPropertyName(property.ToString());
                BackingFields[name].SetValue(this, value);
                return this;
            }
            public static EditableModelFor<TModel> CreateInstance()
                => CreateInstance<TModel, EditableModelFor<TModel>>(out var _).parent;

        }
        public abstract class EditableModelFor<TModel, TParent> : EditableModelFor<TModel>
            where TParent : EditableModelFor<TModel, TParent>
            where TModel : class
        {
            public static new TParent CreateInstance() => CreateInstance<TModel, TParent>(out var _).parent;
        }
    }
}
