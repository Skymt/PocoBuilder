using System.Collections.Concurrent;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace PocoBuilder
{
    public static class PocoBuilder
    {
        public interface ISetter<TInterface> { ISetter<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue? value); }
        readonly struct Setter<TInterface> : ISetter<TInterface>
        {
            readonly Dictionary<string, object?> values;
            public Setter() => values = Properties<TInterface>().ToDictionary(m => m.Name, m => (object?)null);
            public ISetter<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue? value)
            {
                if (property.Body is MemberExpression expression)
                {
                    var memberName = expression.Member.Name;
                    if (values.ContainsKey(memberName))
                        values[memberName] = value;
                }
                return this;
            }
            public static implicit operator object?[]?(Setter<TInterface> setter) => setter.values.Values.ToArray();
        }

        readonly static Type IsExternalInitType = typeof(IsExternalInit);
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
            var typeName = $"{parentType.Name}.{interfaceType.Name}.{string.Join(".", interfaceType.GetInterfaces().Select(i => i.Name))}";

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

            instance = Activator.CreateInstance(type, parameters)!;
            return instance;
        }
        
        public static bool VerifyPocoInterface<TInterface>()
        {
            // POCO classes can only contain properties and fields.
            // Fields are not valid in an interface, thus a POCO interface may only contain properties!
            var interfaceType = typeof(TInterface);
            if (!interfaceType.IsInterface) return false;

            var declaredProperties = interfaceType.GetProperties();
            var inheritedProperties = interfaceType.GetInterfaces().SelectMany(i => i.GetProperties());
            var allPropertyNames = declaredProperties.Union(inheritedProperties).Select(p => p.Name);
            // Property names must be unique.
            if (allPropertyNames.Count() != allPropertyNames.Distinct().Count()) return false;

            var declaredMethods = interfaceType.GetMethods();
            var inheritedMethods = interfaceType.GetInterfaces().SelectMany(i => i.GetMethods());
            var allMethods = declaredMethods.Union(inheritedMethods);
            // Any method, except those generated by properties, are invalid!
            if (allMethods.Where(m => (m.Attributes & MethodAttributes.SpecialName) == 0).Any()) return false;

            // I'd check for fields, but since I already checked that TInterface actually is
            // an interface, it seems superflous.
            return true;
        }
        public static IReadOnlyDictionary<string, Type> GetProperties<TInterface>()
            => Properties<TInterface>().ToDictionary(p => p.Name, p => p.PropertyType);

        static Type ImplementClass(TypeBuilder type, Type interfaceType, ConstructorInfo? parentConstructor = null)
        {
            type.AddInterfaceImplementation(interfaceType);
            var properties = Properties(interfaceType);
            var ctor = type.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, properties.Select(p => p.PropertyType).ToArray());
            var ctorIL = ctor.GetILGenerator();

            foreach ((var property, var index) in properties.Select((p, i) => (p, i + 1)))
            {
                var backingField = ImplementProperty(property, type);
                ctor.DefineParameter(index, ParameterAttributes.None, property.Name);
                ctorIL.Emit(OpCodes.Ldarg_0);
                switch (index)
                {
                    case 1: ctorIL.Emit(OpCodes.Ldarg_1); break;
                    case 2: ctorIL.Emit(OpCodes.Ldarg_2); break;
                    case 3: ctorIL.Emit(OpCodes.Ldarg_3); break;
                    default: ctorIL.Emit(OpCodes.Ldarg_S, index); break;
                }
                ctorIL.Emit(OpCodes.Stfld, backingField);
            }
            if (parentConstructor != null)
            {
                type.DefineDefaultConstructor(MethodAttributes.Public);
                ctorIL.Emit(OpCodes.Ldarg_0);
                ctorIL.Emit(OpCodes.Call, parentConstructor);
            }
            ctorIL.Emit(OpCodes.Ret);
            return type.CreateType()!;
        }
        static FieldBuilder ImplementProperty(PropertyInfo propertyInfo, TypeBuilder type)
        {
            var interfaceGetter = propertyInfo.GetGetMethod();
            var interfaceSetter = propertyInfo.GetSetMethod();

            var fieldAttributes = FieldAttributes.Private;
            if (interfaceSetter?.ReturnParameter.GetRequiredCustomModifiers().Any(IsExternalInitType.Equals) ?? true) 
                fieldAttributes |= FieldAttributes.InitOnly;

            FieldBuilder field = type.DefineField("__" + propertyInfo.Name, propertyInfo.PropertyType, fieldAttributes);
            PropertyBuilder property = type.DefineProperty(propertyInfo.Name, PropertyAttributes.None, propertyInfo.PropertyType, null);
            
            if (interfaceGetter != null)
            {
                var getter = cloneMethod(interfaceGetter);
                ILGenerator getIL = getter.GetILGenerator();
                getIL.Emit(OpCodes.Ldarg_0);
                getIL.Emit(OpCodes.Ldfld, field);
                getIL.Emit(OpCodes.Ret);
                property.SetGetMethod(getter);
                type.DefineMethodOverride(getter, interfaceGetter);
            }
            
            if (interfaceSetter != null)
            {
                var setter = cloneMethod(interfaceSetter);
                ILGenerator setIL = setter.GetILGenerator();
                setIL.Emit(OpCodes.Ldarg_0);
                setIL.Emit(OpCodes.Ldarg_1);
                setIL.Emit(OpCodes.Stfld, field);
                setIL.Emit(OpCodes.Ret);
                property.SetSetMethod(setter);
                type.DefineMethodOverride(setter, interfaceSetter);
            }

            return field;
            MethodBuilder cloneMethod(MethodInfo method)
            {
                var newMethod = type.DefineMethod(method.Name, method.Attributes & ~MethodAttributes.Abstract);
                newMethod.SetSignature(method.ReturnType, 
                    method.ReturnParameter.GetRequiredCustomModifiers(),
                    method.ReturnParameter.GetOptionalCustomModifiers(), 
                    method.GetParameters().Select(p => p.ParameterType).ToArray(), null, null);
                return newMethod;
            }
        }
    }

    public class PocoFactory<TInterface> : DynamicObject, PocoBuilder.ISetter<TInterface>
    {
        readonly Dictionary<string, object?> values = PocoBuilder.GetProperties<TInterface>()
            .ToDictionary(kvp => kvp.Key, kvp => (object?)null);
        readonly Type objectType = PocoBuilder.GetTypeFor<TInterface>();
        public TInterface CreateInstance() => (TInterface)Activator.CreateInstance(objectType, values.Values.ToArray())!;

        public PocoBuilder.ISetter<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue? value)
        {
            if (property.Body is MemberExpression expression)
            {
                var memberName = expression.Member.Name;
                if (values.ContainsKey(memberName))
                    values[memberName] = value;
            }
            return this;
        }
        public object? this[string name] 
        { 
            get => values[name]; 
            set
            {
                if(values.ContainsKey(name))
                    values[name] = value;
            } 
        }
        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            if(values.ContainsKey(binder.Name))
            {
                values[binder.Name] = value;
                return true;
            }
            return false;
        }
        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            if (values.ContainsKey(binder.Name))
            {
                result = values[binder.Name];
                return true;
            }
            result = null;
            return false;
        }
        public override IEnumerable<string> GetDynamicMemberNames() => values.Keys;
    }
}