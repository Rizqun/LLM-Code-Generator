using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeGenerator.AI.Plugins
{
    public class CodeGenerator
    {
        [SKFunction, Description("Get the final prompt used to get the input")]
        public string GeneratePromptToGetInput(SKContext context)
        {
            var appEndpoint = context.Variables["appEndpoint"];
            var appRequestBody = string.Empty;

            if (context.Variables.ContainsKey("appRequestBody"))
                appRequestBody = context.Variables["appRequestBody"];

            var sb = new StringBuilder();

            sb.AppendLine("We will hit the following API endpoint:");
            sb.AppendLine($"---\n{appEndpoint}\n---");

            if (!string.IsNullOrEmpty(appRequestBody))
            {
                sb.AppendLine("\nWe also have the following request body:");
                sb.AppendLine($"---\n{appRequestBody}\n---");
            }

            context.Variables.Set("inputPrompt", sb.ToString());

            return context.Variables["inputPrompt"];
        }

        [SKFunction, Description("Clean up response from AI")]
        public string CleanUpAIResponse(SKContext context)
        {
            string fileType = context.Variables["fileType"];
            string pattern = @$"```{fileType}\s*(.*?)```";
            Match match = Regex.Match(context.Variables["INPUT"], pattern, RegexOptions.Singleline);

            if (match.Success)
            {
                context.Variables.Set("INPUT", match.Groups[1].Value.Trim());
            }

            context.Variables["content"] = context.Variables["INPUT"];

            if (context.Variables.ContainsKey("path"))
                File.Delete(context.Variables["path"]);

            return context.Variables["INPUT"];
        }
    }
}
