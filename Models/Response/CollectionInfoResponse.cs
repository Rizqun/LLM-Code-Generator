using System.Text.Json.Serialization;

namespace CodeGenerator.Models.Response
{
    public class CollectionInfo
    {
        public Result Result { get; set; } = new Result();
    }

    public class Result
    {
        public string Status { get; set; } = string.Empty;
        [JsonPropertyName("optimizer_status")]
        public string OptimizerStatus { get; set; } = string.Empty;
        [JsonPropertyName("vectors_count")]
        public int VectorsCount { get; set; }
    }
}
