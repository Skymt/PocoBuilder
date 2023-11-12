using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace PocoBuilder;
public interface ISetter<TInterface> { ISetter<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue value); }
public readonly struct DTOTemplate<TInterface> : ISetter<TInterface>
{
    readonly IServiceProvider? serviceProvider = null;
    readonly Dictionary<string, object?> properties;

    public DTOTemplate() => properties = DTOBuilder.GetTypeFor<TInterface>().GetProperties().ToDictionary(p => p.Name, p => (object?)null);
    public DTOTemplate(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
        properties = DTOBuilder.GetTypeFor<TInterface>().GetProperties().ToDictionary(p => p.Name, valueExtractor);
        object? valueExtractor(PropertyInfo p) => serviceProvider.GetService(p.PropertyType);
    }
    public DTOTemplate(TInterface instance) => properties = DTOBuilder.GetTypeFor<TInterface>()
        .GetProperties().ToDictionary(p => p.Name, p => p.GetValue(instance));

    public bool TryGet<TValue>(Expression<Func<TInterface, TValue>> property, out TValue? value)
    {
        value = default;
        if (property.Body is MemberExpression expression)
        {
            if(properties.ContainsKey(expression.Member.Name))
            {
                var objectValue = properties[expression.Member.Name];
                if(objectValue != null)
                {
                    value = (TValue?)objectValue;
                    return true;
                }
            }
        }
        return false;
    }
    public TValue Get<TValue>(Expression<Func<TInterface, TValue>> property)
    {
        if (property.Body is MemberExpression expression)
            return (TValue)properties[expression.Member.Name]!;
        return default!;
    }
    public DTOTemplate<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue value)
    {
        if (property.Body is MemberExpression expression)
        {
            if(!properties.ContainsKey(expression.Member.Name))
                throw new AccessViolationException($"{DTOBuilder.GetTypeFor<TInterface>().Name}.{expression.Member.Name} is protected, and cannot be set by a templater.");
            properties[expression.Member.Name] = value;
        }
        return this;
    }
    ISetter<TInterface> ISetter<TInterface>.Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue value) where TValue : default => Set(property, value);

    public DTOTemplate<TCast> Cast<TCast>()
    {
        var template = serviceProvider == null
            ? new DTOTemplate<TCast>()
            : new DTOTemplate<TCast>(serviceProvider);
        foreach (var propertyName in template.properties!.Keys.Where(properties.ContainsKey))
            template.properties[propertyName] = properties[propertyName];
        return template;
    }
    public static implicit operator object?[]?(DTOTemplate<TInterface> template) => template.properties.Values.ToArray();
}