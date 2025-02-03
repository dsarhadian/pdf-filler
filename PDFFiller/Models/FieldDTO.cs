using Newtonsoft.Json;

namespace PDFFiller.Models
{
    internal class FieldDTO
    {
        [JsonProperty("name")]
        public required string Name { get; set; }

        [JsonProperty("value")]
        public required string Value { get; set; }
    }
}
