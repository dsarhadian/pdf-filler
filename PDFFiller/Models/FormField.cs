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

    public enum TextAlign
    {
        TopLeft = 0,
        TopCenter = 1,
        TopRight = 2,
        CenterLeft = 3,
        CenterCenter = 4,
        CenterRight = 5,
        BottomLeft = 6,
        BottomCenter = 7,
        BottomRight = 8,
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

        [JsonProperty("page_number")]
        public float? PageNumber { get; set; }

        [JsonProperty("font_size")]
        public float? FontSize { get; set; }

        [JsonProperty("text_align")]
        public TextAlign TextAlign { get; set; }

        public int TabOrder { get; set; }

        [JsonProperty("field_type")]
        public string? JsonFieldType { get; set; }

        [JsonProperty("width")]
        public float? JsonWidth { get; set; }

        [JsonProperty("height")]
        public float? JsonHeight { get; set; }

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
