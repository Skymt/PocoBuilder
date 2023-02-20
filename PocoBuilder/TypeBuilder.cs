using System.Reflection;
using System.Reflection.Emit;

namespace PocoBuilder
{
    public partial class PocoBuilder
    {
        static IDictionary<string, FieldInfo> ImplementInterface(TypeBuilder type, Type interfaceType)
        {
            type.AddInterfaceImplementation(interfaceType);

            var declaredProperties = interfaceType.GetProperties();
            var inheritedProperties = interfaceType.GetInterfaces().SelectMany(i => i.GetProperties());
            var properties = declaredProperties.Union(inheritedProperties).ToHashSet();

            foreach (var property in properties)
                ImplementProperty(property, type);

            Type pocoType;
            try { pocoType = type.CreateType() ?? throw new Exception(); }
            catch { throw; }

            return properties.ToDictionary(getKey, getField);

            string getKey(PropertyInfo p) => p.Name;
            FieldInfo getField(PropertyInfo p) => 
                pocoType.GetField("__" + p.Name, BindingFlags.Instance | BindingFlags.NonPublic) 
                ?? throw new Exception();
        }
        static void ImplementProperty(PropertyInfo propertyInfo, TypeBuilder type)
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
        }
    }
}
