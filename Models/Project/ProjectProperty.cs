namespace CodeGenerator.Models.Project
{
    public class ProjectProperty
    {
        public APIEndpoint ApiEndpoint { get; set; } = new APIEndpoint();
    }

    public class APIEndpoint
    {
        public string Method { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public bool NeedRequestBody { get; set; }
    }
}
