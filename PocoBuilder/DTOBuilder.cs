using System.Collections.Concurrent;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace PocoBuilder;
public static class DTOBuilder
{
    // This class, and the related factory, are supposed to be the feature complete black box magic part of a core library.
    // But feel free to play around, I'm sure nothing can go wrong!
    readonly static Type IsExternalInitType = typeof(IsExternalInit);
    readonly static AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new("DynamicPocoTypes"), AssemblyBuilderAccess.Run);
    readonly static ModuleBuilder module = assembly.DefineDynamicModule("DynamicPocoModule");
    readonly static ConcurrentDictionary<Type, string> classNames = new();

    public static Type GetTypeFor<TInterface>()
    {
        var typeName = classNames.GetOrAdd(typeof(TInterface), nameBuilder);
        return module.GetType(typeName) ?? typeBuilder();

        string nameBuilder(Type t)
        {
            var typename = t.Namespace + ".";
            if (t.DeclaringType != null) typename += t.DeclaringType.Name + "-";
            return typename + t.Name[1..];
        }
        Type typeBuilder()
        {
            var newType = module.DefineType(typeName!, TypeAttributes.Public);
            return ImplementClass(newType, typeof(TInterface));
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
    public static TInterface CreateInstanceOf<TInterface>(Template<TInterface> template) => (TInterface)Activator.CreateInstance(GetTypeFor<TInterface>(), template)!;
    
    static Type ImplementClass(TypeBuilder type, Type interfaceType)
    {
        type.AddInterfaceImplementation(interfaceType);
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

        type.DefineDefaultConstructor(MethodAttributes.Public);
        ctorIL.Emit(OpCodes.Ldarg_0);
        ctorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(BindingFlags.Instance | BindingFlags.Public, Array.Empty<Type>())!);
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

    public interface ISetter<TInterface> { ISetter<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue? value); }
    public readonly struct Template<TInterface> : ISetter<TInterface>
    {
        readonly Dictionary<string, object?> template;
        public Template() => template = GetTypeFor<TInterface>().GetProperties().ToDictionary(p => p.Name, p => (object?)null);
        public TValue? Get<TValue>(Expression<Func<TInterface, TValue>> property)
        {
            if (property.Body is MemberExpression expression)
                return (TValue?)template[expression.Member.Name];
            return default;
        }
        public Template<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue? value)
        {
            if (property.Body is MemberExpression expression)
                template[expression.Member.Name] = value;
            return this;
        }
        ISetter<TInterface> ISetter<TInterface>.Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue? value) where TValue : default => Set(property, value);
        
        public static implicit operator object?[]?(Template<TInterface> setter) => setter.template.Values.ToArray();
    }
}

public class DTOFactory<TInterface> : DynamicObject
{
    readonly protected Type objectType;
    readonly protected Dictionary<string, object?> template = DTOBuilder
        .GetTypeFor<TInterface>().GetProperties().ToDictionary(p => p.Name, p => (object?)null);
    public DTOFactory() => objectType = DTOBuilder.GetTypeFor<TInterface>();
    protected DTOFactory(Type objectType) => this.objectType = objectType;

    public virtual TInterface CreateInstance() => (TInterface)Activate();
    public virtual IEnumerable<TInterface> CreateInstances(Action<DTOFactory<TInterface>> templater)
    {
        while(true) { yield return CreateInstance(); templater(this); }
    }
    protected object Activate() => Activator.CreateInstance(objectType, template.Values.ToArray())!;

    public TValue? Get<TValue>(Expression<Func<TInterface, TValue>> property)
    {
        if (property.Body is MemberExpression expression)
            return (TValue?)template[expression.Member.Name];
        return default;
    }
    public DTOFactory<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue? value)
    {
        if (property.Body is MemberExpression expression)
            template[expression.Member.Name] = value;
        return this;
    }
    public object? this[string name]
    {
        get => template[name];
        set
        {
            if (template.ContainsKey(name))
                template[name] = value;
        }
    }
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        if (template.ContainsKey(binder.Name))
        {
            result = template[binder.Name];
            return true;
        }
        result = null;
        return false;
    }
    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        if (template.ContainsKey(binder.Name))
        {
            template[binder.Name] = value;
            return true;
        }
        return false;
    }
    public override IEnumerable<string> GetDynamicMemberNames() => template.Keys;
}