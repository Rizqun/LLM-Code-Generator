namespace CodeGenerator.Models.Response
{
    public class GetIssueResponse
    {
        public Field Fields { get; set; } = new Field();
    }

    public class Field
    {
        public Description Description { get; set; } = new Description();
    }

    public class Description
    {
        public string Type { get; set; } = string.Empty;
        public List<Description>? Content { get; set; } = new List<Description>();
        public string? Text { get; set; } 
    }
}
