using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocoBuilder;
public class DTOConverter<TInterface> : JsonConverter<TInterface>, IEqualityComparer<string>
{
    static readonly ConcurrentDictionary<Type, IEnumerable<KeyValuePair<string, Type>>> propertyTypeCache = new();
    
    public override TInterface? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var jsonDoc = JsonDocument.ParseValue(ref reader);
        var targetType = DTOBuilder.GetTypeFor<TInterface>();
        var (types, values) = GetConstructorSignature(targetType, options.PropertyNameCaseInsensitive ? this : null);

        foreach (var node in jsonDoc.RootElement.EnumerateObject())
            values[node.Name] = node.Value.Deserialize(types[node.Name], options);

        var instance = Activator.CreateInstance(targetType, values.Values.ToArray())!;
        return (TInterface)instance;
    }
    public override void Write(Utf8JsonWriter writer, TInterface value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, Convert.ChangeType(value, DTOBuilder.GetTypeFor<TInterface>()), options);

    static (Dictionary<string, Type>, Dictionary<string, object?>) GetConstructorSignature(Type targetType, IEqualityComparer<string>? comparer)
    {
        var propertyTypes = propertyTypeCache.GetOrAdd(targetType, t =>
        {
            var propertyTypes = t.GetProperties();
            return propertyTypes.Select(t => new KeyValuePair<string, Type>(t.Name, t.PropertyType));
        });
        var types = new Dictionary<string, Type>(propertyTypes, comparer);
        var values = new Dictionary<string, object?>(propertyTypes.Select(valueFactory), comparer);
        return (types, values);
        static KeyValuePair<string, object?> valueFactory(KeyValuePair<string, Type> registration) => new(registration.Key, null);
    }
    bool IEqualityComparer<string>.Equals(string? x, string? y) => x?.ToLower() == y?.ToLower();
    int IEqualityComparer<string>.GetHashCode([DisallowNull] string obj) => obj.ToLower().GetHashCode();
}
