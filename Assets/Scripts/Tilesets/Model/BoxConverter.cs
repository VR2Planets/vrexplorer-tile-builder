using System;
using Newtonsoft.Json;

namespace Model
{
    public class BoxConverter : JsonConverter
    {
        public override bool CanWrite => true;
        public override bool CanRead => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Box);
        }

        public override void WriteJson(JsonWriter writer,
            object value, JsonSerializer serializer)
        {
            var box = (Box)value;
            
            writer.WriteStartObject();
            writer.WritePropertyName("box");
            writer.WriteStartArray();
            for (int i = 0; i < box.values.Length; i++)
            {
                writer.WriteValue(box.values[i]);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader,
            Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            throw new InvalidOperationException("Use default serialization.");
        }
    }
}