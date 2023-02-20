using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PocoBuilder
{
    public partial class PocoBuilder
    {
        private interface IModelBase { void SetBackingFields(IDictionary<string, FieldInfo> fields); }

        /// <summary><para>
        /// Optional base class for poco interfaces. Provides the public 
        /// Model property, that is of the provided TInterface type.
        /// It also has the backing fields for the properties of the interface
        /// in a protected property. This class can be used directly as a TParent
        /// class, but the recommended approach is to inherit from the
        /// <see cref="ModelFor{TInterface, TParent}"/> base class, in an implementation
        /// that knows how to work with the backing fields.
        /// </para><para>
        /// The static <see cref="ModelFor{TInterface}.CreateInstance(Action{TInterface, IDictionary{string, FieldInfo}}?)"/> 
        /// method provides access to the backing fields, in case the interface defines immutable properties.
        /// The recommended method is to use the the <see cref="ModelFor{TInterface, TParent}"/> base class.
        /// </para>
        /// </summary>
        /// <typeparam name="TInterface">The poco interface to wrap</typeparam>
        public abstract class ModelFor<TInterface> : IModelBase
            where TInterface : class
        {
            private IDictionary<string, FieldInfo>? backingFields;

            /// <summary>
            /// The backing fields of the poco model, to set values of immutable properties.
            /// </summary>
            protected IDictionary<string, FieldInfo> BackingFields => backingFields ?? throw new Exception();

            /// <summary>
            /// The poco model, as defined by TInterface
            /// </summary>
            public TInterface Model => this as TInterface ?? throw new Exception();
            void IModelBase.SetBackingFields(IDictionary<string, FieldInfo> fields) => backingFields = fields;

            /// <summary>
            /// Creates a instance that can be casted to TInterface, the instance of TInterface
            /// can also be accessed through the property Model.
            /// </summary>
            /// <param name="populator">Optional action to set immutable properties of TInterface</param>
            /// <returns>An instance of <see cref="ModelFor{TInterface}"/></returns>
            public static ModelFor<TInterface> CreateInstance(Action<TInterface, IDictionary<string, FieldInfo>>? populator = null)
            {
                var instance = CreateInstance<TInterface, ModelFor<TInterface>>(out var backingFields).parent;
                populator?.Invoke(instance.Model, backingFields);
                return instance;
            }
        }

        /// <summary>
        /// Optional base class for poco interfaces. Provides the public 
        /// Model property, that is of the provided TInterface type.
        /// It also has the backing fields for the properties of the interface
        /// in a protected property.
        /// </summary>
        /// <typeparam name="TInterface">The poco interface to wrap</typeparam>
        /// <typeparam name="TParent">The parent that inherits from this base class</typeparam>
        public abstract class ModelFor<TInterface, TParent> : ModelFor<TInterface>
            where TParent : ModelFor<TInterface, TParent>
            where TInterface : class
        {
            /// <summary>
            /// Creates a instance that can be casted to TInterface, the instance of TInterface
            /// can also be accessed through the property Model.
            /// </summary>
            /// <returns>An instance of TParent</returns>
            public static TParent CreateInstance() => CreateInstance<TInterface, TParent>(out var _).parent;
        }

        /// <summary>
        /// Optional base class for poco interfaces.
        /// Provides the Set method, that directly sets values 
        /// on the backing fields, thus ignoring mutability of the
        /// properties defined by TInterface.
        /// </summary>
        /// <typeparam name="TInterface">The poco interface to wrap</typeparam>
        public abstract class EditableModelFor<TInterface> : ModelFor<TInterface>
            where TInterface : class
        {
            static readonly Regex matchPropertyName = new(@"\.([^.,]*)", RegexOptions.Compiled);
            static string MatchPropertyName(string s) => matchPropertyName.Match(s).Groups[1].Value;
            
            /// <summary>
            /// Sets a value of a property defined by TInterface, regardless if
            /// there is a setter defined or not.
            /// </summary>
            /// <typeparam name="TValue">The type of property (implicit by defining the property argument)</typeparam>
            /// <param name="property">The property to set</param>
            /// <param name="value">The value to set</param>
            /// <returns>Itself, for fluidity.</returns>
            public EditableModelFor<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue value)
            {
                var name = MatchPropertyName(property.ToString());
                BackingFields[name].SetValue(this, value);
                return this;
            }
            public static EditableModelFor<TInterface> CreateInstance()
                => CreateInstance<TInterface, EditableModelFor<TInterface>>(out var _).parent;

        }

        /// <summary>
        /// Optional base class for poco interfaces.
        /// Provides the Set method, that directly sets values 
        /// on the backing fields, thus ignoring mutability of the
        /// properties defined by TInterface.
        /// </summary>
        /// <typeparam name="TInterface">The poco interface to wrap</typeparam>
        /// <typeparam name="TParent">The parent that inherits from this base class</typeparam>
        public abstract class EditableModelFor<TInterface, TParent> : EditableModelFor<TInterface>
            where TParent : EditableModelFor<TInterface, TParent>
            where TInterface : class
        {
            public static new TParent CreateInstance() => CreateInstance<TInterface, TParent>(out var _).parent;
        }
    }
}
