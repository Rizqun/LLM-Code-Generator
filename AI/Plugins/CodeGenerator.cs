using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.SkillDefinition;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Test.AI.Plugins
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
            context["content"] = context["input"];

            var fileType = "csharp";

            if (context["path"].EndsWith("csproj"))
            {
                fileType = "xml";
            }

            string pattern = @$"```{fileType}\s*(.*?)```";
            Match match = Regex.Match(context["input"], pattern, RegexOptions.Singleline);

            if (match.Success)
            {
                context["content"] = match.Groups[1].Value.Trim();
            }
            else
            {
                context["content"] = context["input"];
            }

            // delete the file first so it can write from clean slate
            //File.Delete(context["path"]);

            return context["content"];
        }
    }
}
