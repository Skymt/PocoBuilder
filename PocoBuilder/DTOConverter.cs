using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PocoBuilder;
public class DTOConverter<TInterface> : JsonConverter<TInterface>
{
    public override TInterface? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var jsonDoc = JsonDocument.ParseValue(ref reader);
        var properties = DTOBuilder.GetTypeFor<TInterface>().GetProperties();
        Dictionary<string, Type> types = options.PropertyNameCaseInsensitive
            ? new Dictionary<string, Type>(new CaseInsensitiveComparer())
            : new Dictionary<string, Type>();
        Dictionary<string, object?> values = options.PropertyNameCaseInsensitive
            ? new Dictionary<string, object?>(new CaseInsensitiveComparer())
            : new Dictionary<string, object?>();
        foreach(var prop in properties)
        {
            types.Add(prop.Name, prop.PropertyType);
            values.Add(prop.Name, null);
        }

        foreach (var node in jsonDoc.RootElement.EnumerateObject())
        {
            values[node.Name] = node.Value.Deserialize(types[node.Name], options);
        }

        var result = Activator.CreateInstance(DTOBuilder.GetTypeFor<TInterface>(), values.Values.ToArray())!;
        return (TInterface)result;
    }
    public override void Write(Utf8JsonWriter writer, TInterface value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, Convert.ChangeType(value, DTOBuilder.GetTypeFor<TInterface>()), options);
    class CaseInsensitiveComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => x?.ToLower() == y?.ToLower();
        public int GetHashCode([DisallowNull] string obj) => obj.ToLower().GetHashCode();
    }
}
