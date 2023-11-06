using System.Dynamic;
using System.Linq.Expressions;

namespace PocoBuilder;

public class DTOFactory<TInterface> : DynamicObject
{
    readonly protected Type objectType;
    readonly protected Dictionary<string, object?> template;
    public DTOFactory(IServiceProvider? serviceProvider = null)
    {
        objectType = DTOBuilder.GetTypeFor<TInterface>();
        template = objectType.GetProperties().ToDictionary(p => p.Name, 
            p => serviceProvider?.GetService(p.PropertyType));
    }

    public virtual TInterface CreateInstance() => (TInterface)Activate();
    public virtual IEnumerable<TInterface> CreateInstances(Action<DTOFactory<TInterface>> templater)
    {
        while (true) { yield return CreateInstance(); templater(this); }
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