namespace CodeGenerator.Models.Project
{
    public class ProjectProperty
    {
        public APIEndpoint ApiEndpoint { get; set; } = new APIEndpoint();
        public Dictionary<string, string> Input { get; set; } = new Dictionary<string, string>();
        public List<RequestBody>? RequestBody { get; set; }
    }

    public class APIEndpoint
    {
        public string Method { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public bool NeedRequestBody { get; set; }
    }

    public class RequestBody
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
    }
}
