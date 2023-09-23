namespace CodeGenerator.Models
{
    public class UserInput
    {
        public string Prompt { get; set; } = string.Empty;
        public string JiraIssueKey { get; set; } = string.Empty;
        public string APIDocumentationURL { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string ProjectLocation { get; set; } = string.Empty;
    }
}