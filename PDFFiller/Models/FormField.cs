using Newtonsoft.Json;

namespace PDFFiller.Models
{
    public enum FieldType
    {
        FIELD_TYPE_NONE = 0,
        FIELD_TYPE_PUSHBUTTON = 1,
        FIELD_TYPE_CHECKBOX = 2,
        FIELD_TYPE_RADIOBUTTON = 3,
        FIELD_TYPE_TEXT = 4,
        FIELD_TYPE_LIST = 5,
        FIELD_TYPE_COMBO = 6,
        FIELD_TYPE_SIGNATURE = 7,
    }

    public class FormField
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        public FieldType? Type { get; set; }

        [JsonProperty("value")]
        public string? Value { get; set; }

        public float? Left { get; set; }
        public float? Right { get; set; }
        public float? Top { get; set; }
        public float? Bottom { get; set; }
        public float? PageNumber { get; set; }

        [JsonProperty("font_size")]
        public float? FontSize { get; set; }

        public int TabOrder { get; set; }

        public float Width
        {
            get
            {
                if (Right.HasValue && Left.HasValue)
                {
                    return Right.Value - Left.Value;
                }
                else
                {
                    return 0;
                }
            }
        }

        public float Height
        {
            get
            {
                if (Top.HasValue && Bottom.HasValue)
                {
                    return Top.Value - Bottom.Value;
                }
                else
                {
                    return 0;
                }
            }
        }
    }
}
