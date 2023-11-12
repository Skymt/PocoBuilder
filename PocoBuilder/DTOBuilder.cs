using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace PocoBuilder;
public static class DTOBuilder
{
    readonly static Type isExternalInitType = typeof(IsExternalInit);
    readonly static AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new("DynamicPocoTypes"), AssemblyBuilderAccess.Run);
    readonly static ModuleBuilder module = assembly.DefineDynamicModule("DynamicPocoModule");
    readonly static ConcurrentDictionary<Type, Type> typeCache = new();
    static DTOBuilder()
    {
#pragma warning disable SYSLIB0015 // this is so sad https://github.com/dotnet/runtime/issues/11811
        var disablePrivateReflection = typeof(DisablePrivateReflectionAttribute).GetConstructor(Array.Empty<Type>());
        var attr = new CustomAttributeBuilder(disablePrivateReflection!, Array.Empty<object>());
        assembly.SetCustomAttribute(attr);
#pragma warning restore SYSLIB0015 // setters marked with "init" keyword ARE ***NOT SAFE FROM REFLECTION***!
    }


    public static Type GetTypeFor<TInterface>()
    {
        var classType = typeCache.GetOrAdd(typeof(TInterface), typeFactory);
        return classType;

        static Type typeFactory(Type interfaceType)
        {
            if (!interfaceType.IsInterface)
                throw new Exception($"DTOBuilder creates class types from interface types. {interfaceType.FullName} is not an interface type.");
            if (!interfaceType.Name.StartsWith('I'))
                throw new Exception("What kind of programmer declares an interface name without a leading 'I'?!? I refuse to accept this!");

            var newName = interfaceType.Namespace + '.';
            if (interfaceType.DeclaringType != null) 
                newName += interfaceType.DeclaringType.Name + '.';
            if (interfaceType.IsGenericType)
            {
                newName += interfaceType.Name[1..^2] + '.';
                newName += interfaceType.GetGenericArguments()
                    .Select(t => t.Name.Replace("[]", ".Arr") + '.')
                    .Aggregate((s, n) => s + n);
            }
            else newName += interfaceType.Name[1..]; // Trim the leading 'I'

            var typeBuilder = module.DefineType(newName, TypeAttributes.Public);
            ImplementClass(typeBuilder, interfaceType);
            return module.GetType(newName)!;
        }
    }

    public static TInterface CreateInstanceOf<TInterface>() => (TInterface)Activator.CreateInstance(GetTypeFor<TInterface>())!;
    public static TInterface CreateInstanceOf<TInterface>(Action<ISetter<TInterface>> initializer, IServiceProvider? serviceProvider = null)
    {
        var setter = serviceProvider == null
            ? new DTOTemplate<TInterface>()
            : new DTOTemplate<TInterface>(serviceProvider);
        initializer(setter);
        return CreateInstanceOf(setter);
    }
    public static TInterface CreateInstanceOf<TInterface>(DTOTemplate<TInterface> template) => 
        (TInterface)Activator.CreateInstance(GetTypeFor<TInterface>(), template)!;
    
    static Type ImplementClass(TypeBuilder type, Type interfaceType)
    {
        type.AddInterfaceImplementation(interfaceType);
        type.DefineDefaultConstructor(MethodAttributes.Public);

        var declaredProperties = interfaceType.GetProperties();
        var inheritedProperties = interfaceType.GetInterfaces().SelectMany(i => i.GetProperties());
        var properties = declaredProperties.Union(inheritedProperties);

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
        ctorIL.Emit(OpCodes.Ret);

        var protectedFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        var protectedProperties = interfaceType.GetInterfaces().SelectMany(i => i.GetProperties(protectedFlags));
        protectedProperties = protectedProperties.Union(interfaceType.GetProperties(protectedFlags));
        foreach (var property in protectedProperties) ImplementProperty(property, type);

        return type.CreateType()!;
    }
    static FieldBuilder ImplementProperty(PropertyInfo propertyInfo, TypeBuilder type)
    {
        var interfaceGetter = propertyInfo.GetGetMethod(true);
        var interfaceSetter = propertyInfo.GetSetMethod(true);

        var fieldAttributes = FieldAttributes.Private;
        if (interfaceSetter?.ReturnParameter.GetRequiredCustomModifiers().Any(isExternalInitType.Equals) ?? true)
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