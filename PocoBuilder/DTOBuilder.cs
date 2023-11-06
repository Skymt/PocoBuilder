using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace PocoBuilder;
public static class DTOBuilder
{
    // This class, and the related utilities, are supposed to be the feature complete black box magic part of a core library.
    // But feel free to play around, I'm sure nothing can go wrong!
    readonly static Type IsExternalInitType = typeof(IsExternalInit);
    readonly static AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new("DynamicPocoTypes"), AssemblyBuilderAccess.Run);
    readonly static ModuleBuilder module = assembly.DefineDynamicModule("DynamicPocoModule");
    readonly static ConcurrentDictionary<Type, string> classNameCache = new();

    public static Type GetTypeFor<TInterface>()
    {
        var typeName = classNameCache.GetOrAdd(typeof(TInterface), classNameFactory);
        return module.GetType(typeName)!;

        static string classNameFactory(Type type)
        {
            var newName = type.Namespace + '.';
            if (type.DeclaringType != null) 
                newName += type.DeclaringType.Name + '.';
            newName += type.Name[1..];

            ImplementClass(module.DefineType(newName!, TypeAttributes.Public), type);
            return newName;
        }
    }

    public static TInterface CreateInstanceOf<TInterface>(Action<ISetter<TInterface>>? initializer = null)
    {
        object instance;
        if (initializer != null)
        {
            var setter = new Template<TInterface>(); initializer(setter);
            instance = Activator.CreateInstance(GetTypeFor<TInterface>(), setter)!;
        } 
        else instance = Activator.CreateInstance(GetTypeFor<TInterface>())!;
        return (TInterface)instance;
    }
    public static TInterface CreateInstanceOf<TInterface>(Template<TInterface> template) => 
        (TInterface)Activator.CreateInstance(GetTypeFor<TInterface>(), template)!;
    
    static Type ImplementClass(TypeBuilder type, Type interfaceType)
    {
        type.AddInterfaceImplementation(interfaceType);
        type.DefineDefaultConstructor(MethodAttributes.Public);

        var declaredProperties = interfaceType.GetProperties();
        var inheritedProperties = interfaceType.GetInterfaces().SelectMany(i => i.GetProperties());
        var properties = new HashSet<PropertyInfo>(declaredProperties.Union(inheritedProperties));

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

        var privateProperties = interfaceType.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic)
            .Union(interfaceType.GetInterfaces().SelectMany(i => i.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic)));

        foreach (var property in privateProperties)
        {
            _ = ImplementProperty(property, type);
        }

        return type.CreateType()!;
    }
    static FieldBuilder ImplementProperty(PropertyInfo propertyInfo, TypeBuilder type)
    {
        var interfaceGetter = propertyInfo.GetGetMethod(true);
        var interfaceSetter = propertyInfo.GetSetMethod(true);

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