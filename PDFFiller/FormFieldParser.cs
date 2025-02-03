using Newtonsoft.Json;
using PDFFiller.Models;

namespace PDFFiller
{
    class FormFieldParser
    {
        public static Dictionary<int, List<FormField>>? ParseJson(string json)
        {
            var pageDTOs = JsonConvert.DeserializeObject<List<PageDTO>>(json);

            if (pageDTOs == null)
            {
                return null;
            }

            var result = pageDTOs.ToDictionary(
                pageDto => pageDto.PageNumber,
                pageDto =>
                    pageDto
                        .Fields.Select(fieldDto => new FormField
                        {
                            Name = fieldDto.Name,
                            Value = fieldDto.Value,
                            Type = FieldType.FIELD_TYPE_NONE,
                            PageNumber = pageDto.PageNumber,
                        })
                        .ToList()
            );

            return result;
        }
    }
}
