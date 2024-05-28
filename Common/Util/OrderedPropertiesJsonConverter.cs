using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace QuantConnect.Util
{
    /// <summary>
    /// Sorts Object T properties when serializing to JSON.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OrderedPropertiesJsonConverter<T> : JsonConverter<T>
    {

        public override T? ReadJson(JsonReader reader, Type typeToConvert, T? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return serializer.Deserialize<T>(reader);
        }

        public override void WriteJson(JsonWriter writer, T value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            var properties = typeof(T).GetProperties().OrderBy(p => p.Name).ToList();

            foreach (var property in properties)
            {
                var propertyValue = property.GetValue(value);

                writer.WritePropertyName(property.Name);

                serializer.Serialize(writer, propertyValue);
            }

            writer.WriteEndObject();
        }
    }
}
