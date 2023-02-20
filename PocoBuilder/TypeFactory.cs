using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace PocoBuilder
{
    public partial class PocoBuilder
    {
        readonly static AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new("DynamicTypes"), AssemblyBuilderAccess.Run);
        readonly static ModuleBuilder module = assembly.DefineDynamicModule("DynamicModule");
        readonly static ConcurrentDictionary<string, (Type type, IDictionary<string, FieldInfo> fields)> typeCache = new();

        /// <summary>
        /// Creates a class type, where all properties defined in TInterface has been implented.
        /// </summary>
        /// <typeparam name="TInterface">The interface defining the properties</typeparam>
        /// <param name="backingFields">The fields the properties will read from and potentially write to in the constructed class.</param>
        /// <returns>A type that can be instantiated using <see cref="Activator.CreateInstance(Type)"/></returns>
        public static Type GetPocoTypeFor<TInterface>(out IDictionary<string, FieldInfo> backingFields)
        {
            var interfaceType = typeof(TInterface);
            return GetPocoTypeFor(interfaceType, out backingFields);
        }

        /// <summary>
        /// Creates a class type, where all properties defined in the provided interface has been implented.
        /// </summary>
        /// <param name="interfaceType">The type of interface defining the properties</param>
        /// <param name="backingFields">The fields the properties will read from and write (if set; is defined) to in the constructed class.</param>
        /// <returns>A type that can be instantiated using <see cref="Activator.CreateInstance(Type)"/></returns>
        public static Type GetPocoTypeFor(Type interfaceType, out IDictionary<string, FieldInfo> backingFields)
        {
            var newTypeName = $"_{interfaceType.DeclaringType?.Name}.{interfaceType.FullName}";
            var (type, fields) = typeCache.GetOrAdd(newTypeName, localTypeFactory, interfaceType);
            backingFields = fields; return type;
            static (Type, IDictionary<string, FieldInfo>) localTypeFactory(string className, Type interfaceType)
            {
                var pocoType = module.DefineType(className, TypeAttributes.Public);
                var backingFields = ImplementInterface(pocoType, interfaceType);
                return (pocoType, backingFields);
            }
        }
        /// <summary>
        /// Creates a class type, where all properties defined in TInterface has been implented.
        /// </summary>
        /// <typeparam name="TInterface">The interface defining the properties</typeparam>
        /// <typeparam name="TParent">The parent class of the created class</typeparam>
        /// <param name="backingFields">The fields the properties will read from and potentially write to in the constructed class.</param>
        /// <returns>A type that can be instantiated using <see cref="Activator.CreateInstance(Type)"/></returns>
        public static Type GetPocoTypeFor<TInterface, TParent>(out IDictionary<string, FieldInfo> backingFields)
        {
            var interfaceType = typeof(TInterface);
            var parentType = typeof(TParent);
            return GetPocoTypeFor(interfaceType, parentType, out backingFields);
        }

        /// <summary>
        /// Creates a class type, where all properties defined in the provided interface has been implented.
        /// </summary>
        /// <param name="interfaceType">The type of interface defining the properties</param>
        /// <param name="parentType">The parent class of the created class</param>
        /// <param name="backingFields">The fields the properties will read from and write (if set; is defined) to in the constructed class.</param>
        /// <returns>A type that can be instantiated using <see cref="Activator.CreateInstance(Type)"/></returns>
        public static Type GetPocoTypeFor(Type interfaceType, Type parentType, out IDictionary<string, FieldInfo> backingFields)
        {
            var newTypeName = $"_{parentType.Name}.{interfaceType.Name}";
            var (type, fields) = typeCache.GetOrAdd(newTypeName, localTypeFactory, interfaceType);
            backingFields = fields; return type;
            (Type, IDictionary<string, FieldInfo>) localTypeFactory(string className, Type interfaceType)
            {
                var pocoType = module.DefineType(className, TypeAttributes.Public, parentType);
                var backingFields = ImplementInterface(pocoType, interfaceType);
                return (pocoType, backingFields);
            }
        }
    }
}