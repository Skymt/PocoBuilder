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

        public static Type GetPocoTypeFor<TInterface>(out IDictionary<string, FieldInfo> backingFields)
        {
            var interfaceType = typeof(TInterface);
            return GetPocoTypeFor(interfaceType, out backingFields);
        }
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

        public static Type GetPocoTypeFor<TInterface, TParent>(out IDictionary<string, FieldInfo> backingFields)
        {
            var interfaceType = typeof(TInterface);
            var parentType = typeof(TParent);
            return GetPocoTypeFor(interfaceType, parentType, out backingFields);
        }
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