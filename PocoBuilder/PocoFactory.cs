using System.Reflection;

namespace PocoBuilder
{
    public partial class PocoBuilder
    {
        public static TInterface CreateInstance<TInterface>(out IDictionary<string, FieldInfo> backingFields)
        {
            var pocoType = GetPocoTypeFor<TInterface>(out backingFields);
            var pocoInstance = Activator.CreateInstance(pocoType)
                ?? throw new Exception("Failed to construct instance.");
            return (TInterface)pocoInstance;
        }

        public static (TInterface obj, TParent parent) CreateInstance<TInterface, TParent>(out IDictionary<string, FieldInfo> backingFields)
            where TInterface : class
        {
            var pocoType = GetPocoTypeFor<TInterface, TParent>(out backingFields);
            var pocoInstance = Activator.CreateInstance(pocoType)
                ?? throw new Exception("Failed to construct instance.");
            if (pocoInstance is IModelBase modelBase) modelBase.SetBackingFields(backingFields);
            return ((TInterface)pocoInstance, (TParent)pocoInstance);
        }

        public static bool VerifyInterface<TInterface>()
        {
            var interfaceType = typeof(TInterface);
            if(!interfaceType.IsInterface) return false;

            var declaredProperties = interfaceType.GetProperties();
            var inheritedProperties = interfaceType.GetInterfaces().SelectMany(i => i.GetProperties());
            var allPropertyNames = declaredProperties.Union(inheritedProperties).Select(p => p.Name);
            if(allPropertyNames.Count() != allPropertyNames.Distinct().Count()) return false;

            var declaredMethods = interfaceType.GetMethods();
            var inheritedMethods = interfaceType.GetInterfaces().SelectMany(i => i.GetMethods());
            var allMethods = declaredMethods.Union(inheritedMethods);
            if(allMethods.Any()) return false;

            return true; 
        }
    }
}
