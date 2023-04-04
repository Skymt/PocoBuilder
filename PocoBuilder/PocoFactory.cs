using System.Reflection;

namespace PocoBuilder
{
    public partial class PocoBuilder
    {
        /// <summary>
        /// Uses <see cref="GetPocoTypeFor(Type, out IDictionary{string, FieldInfo})"/> to get a type.
        /// It then creates an instance of that type using <see cref="Activator.CreateInstance(Type)"/>, and returns that as an
        /// implementation of the requested interface.
        /// </summary>
        /// <typeparam name="TInterface">The interface defining the properties</typeparam>
        /// <param name="backingFields">The fields the properties will read from and potentially write to in the constructed class.</param>
        /// <returns>An instance of the provided interface, where all properties can be interacted with normally</returns>
        /// <exception cref="Exception">A generic error has prevented the instantiation of the dynamic type</exception>
        public static TInterface CreatePocoInstance<TInterface>(out IDictionary<string, FieldInfo> backingFields)
        {
            var pocoType = GetPocoTypeFor<TInterface>(out backingFields);
            var pocoInstance = Activator.CreateInstance(pocoType)
                ?? throw new Exception("Failed to construct instance.");
            return (TInterface)pocoInstance;
        }

        /// <summary>
        /// Uses <see cref="GetPocoTypeFor(Type, out IDictionary{string, FieldInfo})"/> to get a type.
        /// It then creates an instance of that type using <see cref="Activator.CreateInstance(Type)"/>, and returns that as an
        /// implementation of the requested interface.
        /// </summary>
        /// <typeparam name="TInterface">The interface defining the properties</typeparam>
        /// <typeparam name="TParent">The parent class of the created class</typeparam>
        /// <param name="backingFields">The fields the properties will read from and potentially write to in the constructed class.</param>
        /// <returns>An instance of the provided interface, where all properties can be interacted with normally</returns>
        /// <exception cref="Exception">A generic error has prevented the instantiation of the dynamic type</exception>
        public static (TInterface obj, TParent parent) CreatePocoInstance<TInterface, TParent>(out IDictionary<string, FieldInfo> backingFields)
            where TInterface : class
        {
            var pocoType = GetPocoTypeFor<TInterface, TParent>(out backingFields);
            var pocoInstance = Activator.CreateInstance(pocoType)
                ?? throw new Exception("Failed to construct instance.");
            if (pocoInstance is IModelBase modelBase) modelBase.SetBackingFields(backingFields);
            return ((TInterface)pocoInstance, (TParent)pocoInstance);
        }

        /// <summary>
        /// Checks is the interface is a valid poco interface.
        /// </summary>
        /// <typeparam name="TInterface">The interface to check</typeparam>
        /// <returns>True, if the interface can be used as TInterface to create types and objects</returns>
        public static bool VerifyPocoInterface<TInterface>()
        {
            var interfaceType = typeof(TInterface);
            if (!interfaceType.IsInterface) return false;

            var declaredProperties = interfaceType.GetProperties();
            var inheritedProperties = interfaceType.GetInterfaces().SelectMany(i => i.GetProperties());
            var allPropertyNames = declaredProperties.Union(inheritedProperties).Select(p => p.Name);
            if (allPropertyNames.Count() != allPropertyNames.Distinct().Count()) return false;

            var declaredMethods = interfaceType.GetMethods();
            var inheritedMethods = interfaceType.GetInterfaces().SelectMany(i => i.GetMethods());
            var allMethods = declaredMethods.Union(inheritedMethods).ToArray();
            if (allMethods.Where(m => (m.Attributes & MethodAttributes.SpecialName) == 0).Any()) return false;

            return true;
        }
    }
}
