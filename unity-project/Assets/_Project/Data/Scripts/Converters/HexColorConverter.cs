using System;
using Newtonsoft.Json;
using UnityEngine;

namespace KINETICS5.Data
{
    /// <summary>
    /// Convertisseur Newtonsoft.Json pour <see cref="Color"/> stockée au format
    /// hexadécimal CSS ("<c>#RRGGBB</c>" ou "<c>#RRGGBBAA</c>").
    /// Tolère également la forme tableau <c>[r,g,b]</c> / <c>[r,g,b,a]</c> en 0..1.
    /// </summary>
    /// <remarks>
    /// Utilisé pour les champs <c>themeColor</c> (agents) et <c>ambientColor</c>
    /// (régions) afin de respecter la palette NON-NÉGOCIABLE du projet
    /// (ex: <c>#1AA1CE</c> cyan).
    /// </remarks>
    public sealed class HexColorConverter : JsonConverter<Color>
    {
        /// <inheritdoc />
        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Null:
                    return existingValue;
                case JsonToken.String:
                {
                    var s = (string?)reader.Value;
                    if (!string.IsNullOrEmpty(s) && ColorUtility.TryParseHtmlString(s, out Color c))
                    {
                        return c;
                    }
                    return existingValue;
                }
                case JsonToken.StartArray:
                {
                    var arr = serializer.Deserialize<float[]>(reader);
                    if (arr != null && arr.Length >= 3)
                    {
                        float a = arr.Length >= 4 ? arr[3] : 1f;
                        return new Color(arr[0], arr[1], arr[2], a);
                    }
                    return existingValue;
                }
                case JsonToken.StartObject:
                {
                    var obj = serializer.Deserialize<FloatColorDto>(reader);
                    if (obj != null)
                    {
                        return new Color(obj.R, obj.G, obj.B, obj.A <= 0f ? 1f : obj.A);
                    }
                    return existingValue;
                }
                default:
                    return existingValue;
            }
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            writer.WriteValue("#" + ColorUtility.ToHtmlStringRGBA(value));
        }

        private sealed class FloatColorDto
        {
            [JsonProperty("r")] public float R { get; set; }
            [JsonProperty("g")] public float G { get; set; }
            [JsonProperty("b")] public float B { get; set; }
            [JsonProperty("a")] public float A { get; set; } = 1f;
        }
    }
}
