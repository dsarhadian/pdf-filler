namespace PDFFiller
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
        FIELD_TYPE_SIGNATURE = 7
    }

    public class FormField
    {
        public string? Name { get; set; }
        public FieldType? Type { get; set; }
        public string? Value { get; set; }
    }
}