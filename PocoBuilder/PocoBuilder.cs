using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace PocoBuilder
{
    public class PocoBuilder
    {
        public interface ISetter<TInterface> { ISetter<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue value); }
        readonly struct Setter<TInterface> : ISetter<TInterface>
        {
            readonly Dictionary<string, object?> values;
            public Setter() => values = Properties<TInterface>().ToDictionary(m => m.Name, m => (object?)null);
            public ISetter<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue value)
            {
                if (property.Body is MemberExpression expression)
                {
                    var memberName = expression.Member.Name;
                    if (value != null && values.ContainsKey(memberName))
                        values[expression.Member.Name] = value;
                }
                return this;
            }
            public static implicit operator object?[]?(Setter<TInterface> setter) => setter.values.Values.ToArray();
        }

        readonly static AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new("DynamicTypes"), AssemblyBuilderAccess.Run);
        readonly static ModuleBuilder module = assembly.DefineDynamicModule("DynamicModule");

        readonly static ConcurrentDictionary<Type, HashSet<PropertyInfo>> propertyCache = new();
        static HashSet<PropertyInfo> Properties<TInterface>() => Properties(typeof(TInterface));
        static HashSet<PropertyInfo> Properties(Type interfaceType) => propertyCache.GetOrAdd(interfaceType, interfaceType =>
        {
            var declaredProperties = interfaceType.GetProperties();
            var inheritedProperties = interfaceType.GetInterfaces().SelectMany(i => i.GetProperties());
            return new HashSet<PropertyInfo>(declaredProperties.Union(inheritedProperties));
        });

        public static Type GetTypeFor<TInterface>() => GetTypeFor<TInterface, object>();
        public static Type GetTypeFor<TInterface, TParent>()
        {
            var interfaceType = typeof(TInterface); var parentType = typeof(TParent);
            var typeName = $"{parentType.Name}.{interfaceType.Name}";

            Type typeBuilder()
            {
                var newType = module.DefineType(typeName, TypeAttributes.Public, parentType);
                var constructor = parentType?.GetConstructor(BindingFlags.Instance | BindingFlags.Public, Array.Empty<Type>());
                return ImplementClass(newType, interfaceType, constructor);
            }
            return module.GetType(typeName) ?? typeBuilder();
        }

        public static TInterface CreateInstanceOf<TInterface>(Action<ISetter<TInterface>>? initializer = null)
            => (TInterface)GetInstanceOf(GetTypeFor<TInterface>(), initializer);
        public static (TInterface asInterface, TParent asParent) CreateInstanceOf<TInterface, TParent>(Action<ISetter<TInterface>>? initializer = null)
        {
            var type = GetTypeFor<TInterface, TParent>();
            object instance = GetInstanceOf(type, initializer);
            return ((TInterface)instance, (TParent)instance);
        }
        static object GetInstanceOf<TInterface>(Type type, Action<ISetter<TInterface>>? initializer = null)
        {
            object instance;
            object?[]? parameters = null;

            if (initializer != null)
            {
                var setter = new Setter<TInterface>(); initializer(setter);
                parameters = setter;
            }

            instance = Activator.CreateInstance(type, parameters) ?? throw new InvalidOperationException();
            return instance;
        }
        
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

        static Type ImplementClass(TypeBuilder type, Type interfaceType, ConstructorInfo? parentConstructor = null)
        {
            type.AddInterfaceImplementation(interfaceType);
            var properties = Properties(interfaceType);

            var ctorIL = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, properties.Select(p => p.PropertyType).ToArray()).GetILGenerator();
            foreach ((var property, var index) in properties.Select((p, i) => (p, i + 1)))
            {
                ctorIL.Emit(OpCodes.Ldarg_0);
                switch (index)
                {
                    case 1: ctorIL.Emit(OpCodes.Ldarg_1); break;
                    case 2: ctorIL.Emit(OpCodes.Ldarg_2); break;
                    case 3: ctorIL.Emit(OpCodes.Ldarg_3); break;
                    default: ctorIL.Emit(OpCodes.Ldarg_S, index); break;
                }
                ctorIL.Emit(OpCodes.Stfld, ImplementProperty(property, type));
            }
            if (parentConstructor != null)
            {
                type.DefineDefaultConstructor(MethodAttributes.Public);
                ctorIL.Emit(OpCodes.Ldarg_0);
                ctorIL.Emit(OpCodes.Call, parentConstructor);
            }
            ctorIL.Emit(OpCodes.Ret);
            return type.CreateType() ?? throw new InvalidOperationException();
        }
        static FieldBuilder ImplementProperty(PropertyInfo propertyInfo, TypeBuilder type)
        {
            FieldBuilder field = type.DefineField("__" + propertyInfo.Name, propertyInfo.PropertyType, FieldAttributes.Private);
            PropertyBuilder property = type.DefineProperty(propertyInfo.Name, PropertyAttributes.None, propertyInfo.PropertyType, null);

            MethodAttributes attributes =
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName |
                MethodAttributes.Virtual;

            var interfaceGetter = propertyInfo.GetGetMethod();
            if (interfaceGetter != null)
            {
                MethodBuilder getter = type.DefineMethod("get_" + propertyInfo.Name, attributes, propertyInfo.PropertyType, Type.EmptyTypes);
                ILGenerator getIL = getter.GetILGenerator();
                getIL.Emit(OpCodes.Ldarg_0);
                getIL.Emit(OpCodes.Ldfld, field);
                getIL.Emit(OpCodes.Ret);
                property.SetGetMethod(getter);
                type.DefineMethodOverride(getter, interfaceGetter);
            }

            var interfaceSetter = propertyInfo.GetSetMethod();
            if (interfaceSetter != null)
            {
                MethodBuilder setter = type.DefineMethod("set_" + propertyInfo.Name, attributes, null, new Type[] { propertyInfo.PropertyType });
                ILGenerator setIL = setter.GetILGenerator();
                setIL.Emit(OpCodes.Ldarg_0);
                setIL.Emit(OpCodes.Ldarg_1);
                setIL.Emit(OpCodes.Stfld, field);
                setIL.Emit(OpCodes.Ret);
                property.SetSetMethod(setter);
                type.DefineMethodOverride(setter, interfaceSetter);
            }
            return field;
        }
    }
}