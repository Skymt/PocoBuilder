using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace PocoBuilder;
public interface ISetter<TInterface> { ISetter<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue? value); }
public readonly struct Template<TInterface> : ISetter<TInterface>
{
    static readonly ConcurrentDictionary<Type, Dictionary<string, object?>> propertyCache = new();
    readonly Dictionary<string, object?> properties;
    public Template() => properties = propertyCache.GetOrAdd(typeof(TInterface), type =>
        DTOBuilder.GetTypeFor<TInterface>().GetProperties().ToDictionary(p => p.Name, p => (object?)null));
    public Template(TInterface instance) => properties = DTOBuilder.GetTypeFor<TInterface>()
        .GetProperties().ToDictionary(p => p.Name, p => p.GetValue(instance));

    public TValue? Get<TValue>(Expression<Func<TInterface, TValue>> property)
    {
        if (property.Body is MemberExpression expression)
            return (TValue?)properties[expression.Member.Name];
        return default;
    }
    public Template<TInterface> Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue? value)
    {
        if (property.Body is MemberExpression expression)
            properties[expression.Member.Name] = value;
        return this;
    }
    ISetter<TInterface> ISetter<TInterface>.Set<TValue>(Expression<Func<TInterface, TValue>> property, TValue? value) where TValue : default => Set(property, value);

    public Template<TCast> Cast<TCast>()
    {
        var template = new Template<TCast>();
        foreach (var propertyName in template.properties.Keys.Where(properties.ContainsKey))
            template.properties[propertyName] = properties[propertyName];
        return template;
    }
    public static implicit operator object?[]?(Template<TInterface> template) => template.properties.Values.ToArray();
}