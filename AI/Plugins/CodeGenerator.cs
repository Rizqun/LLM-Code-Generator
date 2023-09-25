using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace CodeGenerator.AI.Plugins
{
    public class CodeGenerator
    {
        private readonly IKernel _kernel;

        public CodeGenerator(IKernel kernel)
        {
            _kernel = kernel;
        }

        [SKFunction, Description("Add the output from previous function to the 'content' context variable")]
        public string ExtractFilePathAndContent(SKContext context)
        {
            context.Variables["content"] = context.Variables["input"];

            var fileType = "csharp";

            if (context.Variables["path"].EndsWith("csproj"))
            {
                fileType = "xml";
            }

            string pattern = @$"```{fileType}\s*(.*?)```";
            Match match = Regex.Match(context.Variables["input"], pattern, RegexOptions.Singleline);

            if (match.Success)
            {
                context.Variables["content"] = match.Groups[1].Value.Trim();
            }
            else
            {
                context.Variables["content"] = context.Variables["input"];
            }

            // delete the file first so it can write from clean slate
            File.Delete(context.Variables["path"]);

            return context.Variables["content"];
        }
    }
}
