﻿using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PocoBuilder
{
    // TODO: Copy to new instance WITH altered properties
    public partial class PocoBuilder
    {
        public interface IEditable<TInterface> { ISetter<TInterface> Edit(); }
        public interface ISetter<TInterface> { ISetter<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue value); }
        private interface IModelBase { void SetBackingFields(IDictionary<string, FieldInfo> fields); }

        public abstract class ModelFor<TInterface> : IModelBase, IEditable<TInterface>
            where TInterface : class
        {
            readonly struct Setter : ISetter<TInterface>
            {
                static readonly Regex matchPropertyName = new(@"\.([^.,]*)", RegexOptions.Compiled);
                static string MatchPropertyName(string s) => matchPropertyName.Match(s).Groups[1].Value;
                readonly IDictionary<string, FieldInfo> fields; readonly TInterface instance;
                public Setter(TInterface instance, IDictionary<string, FieldInfo> fields) { this.instance = instance; this.fields = fields; }
                public ISetter<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue value)
                {
                    var name = MatchPropertyName(property.ToString());
                    fields[name].SetValue(instance, value);
                    return this;
                }
            }
            protected ISetter<TInterface> CreateSetter() => new Setter(Model, BackingFields);
            ISetter<TInterface> IEditable<TInterface>.Edit() => new Setter(Model, BackingFields);
            void IModelBase.SetBackingFields(IDictionary<string, FieldInfo> fields) => backingFields = fields;

            
            IDictionary<string, FieldInfo>? backingFields;
            protected IDictionary<string, FieldInfo> BackingFields => backingFields ?? throw new Exception();

            public TInterface Model => this as TInterface ?? throw new Exception();

            public ModelFor<TInterface> Clone(Action<ISetter<TInterface>>? setter = null) => Clone<ModelFor<TInterface>>(setter);
            public TParent Clone<TParent>(Action<ISetter<TInterface>>? setter = null)
            {
                (var instance, var parent) = CreateInstance<TInterface, TParent>(out var fields);
                foreach(var key in BackingFields.Keys)
                    fields[key].SetValue(instance, BackingFields[key].GetValue(Model));
                setter?.Invoke(CreateSetter());
                return parent;
            }
            public static ModelFor<TInterface> Default() => CreateInstance<ModelFor<TInterface>>();
            public static TParent CreateInstance<TParent>() where TParent : ModelFor<TInterface>
                => CreateInstance<TInterface, TParent>(out var _).parent;
        }
        public abstract class ModelFor<TInterface, TParent> : ModelFor<TInterface>
            where TParent : ModelFor<TInterface, TParent>
            where TInterface : class
        {
            public static TParent CreateInstance() => CreateInstance<TParent>();
            public new TParent Clone(Action<ISetter<TInterface>>? setter = null) => Clone<TParent>(setter);
        }
    }
}
