using Newtonsoft.Json;

namespace PDFFiller.Models
{
    internal class PageDTO
    {
        [JsonProperty("page_number")]
        public int PageNumber { get; set; }

        [JsonProperty("fields")]
        public required List<FieldDTO> Fields { get; set; }
    }
}
