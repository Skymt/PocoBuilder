using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocoBuilder;
public class DTOConverter<TInterface> : JsonConverter<TInterface>, IEqualityComparer<string>
{
    static readonly Type targetType = DTOBuilder.GetTypeFor<TInterface>();
    static readonly IEnumerable<KeyValuePair<string, Type>> propertyTypes;
    static DTOConverter()
    {
        propertyTypes = targetType.GetProperties().Select(p => new KeyValuePair<string, Type>(p.Name, p.PropertyType));
    }
    public override TInterface? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        IEqualityComparer<string>? comparer = options.PropertyNameCaseInsensitive ? this : null;
        Dictionary<string, Type> types = new(propertyTypes, comparer);
        Dictionary<string, object?> values = new(propertyTypes.Select(valueFactory), comparer);
        static KeyValuePair<string, object?> valueFactory(KeyValuePair<string, Type> registration) => new(registration.Key, null);

        var jsonDoc = JsonDocument.ParseValue(ref reader);
        foreach (var node in jsonDoc.RootElement.EnumerateObject())
            values[node.Name] = node.Value.Deserialize(types[node.Name], options);

        var instance = Activator.CreateInstance(targetType, values.Values.ToArray())!;
        return (TInterface)instance;
    }
    public override void Write(Utf8JsonWriter writer, TInterface value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, Convert.ChangeType(value, DTOBuilder.GetTypeFor<TInterface>()), options);

    bool IEqualityComparer<string>.Equals(string? x, string? y) => x?.ToLower() == y?.ToLower();
    int IEqualityComparer<string>.GetHashCode([DisallowNull] string obj) => obj.ToLower().GetHashCode();
}
