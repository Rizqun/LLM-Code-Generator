using CodeGenerator.Helpers;
using CodeGenerator.Models;
using System.Text.RegularExpressions;

namespace CodeGenerator.Services
{
    public class ProjectGeneratorService
    {
        public static async Task<string> Create(UserInput userInput)
        {
            // Copy whole template
            var sourceFolder = Path.Combine(FileHelper.AssemblyDirectory, "Templates/Project");
            var targetFolder = userInput.ProjectLocation;

            if (Directory.Exists(targetFolder))
                Directory.Delete(targetFolder, true);

            FileHelper.Copy(sourceFolder, targetFolder);

            await CreateSolution(userInput);
            await RenameCsprojAndAddToSolution(userInput);

            return targetFolder;
        }

        public static void NormalizeGeneratedFile(UserInput userInput)
        {
            NormalizeCsprojFile(userInput);
            NormalizeService(userInput);
            NormalizeProgram(userInput);
        }

        public static void NormalizeCsprojFile(UserInput userInput)
        {
            var csprojFile = Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj");

            string content = File.ReadAllText(csprojFile);
            content = content.Trim();
            content = content.Replace("Version=\"5.0.0\"", "Version=\"2.0.0\"");
            string modifiedContent = content;

            if (!content.StartsWith("<Project"))
            {
                if (content.StartsWith("<"))
                {
                    modifiedContent = "<Project Sdk=\"Microsoft.NET.Sdk\">\n" + content + "\n</Project>";
                }
                else
                {
                    string pattern = @"<Project[^>]*>.*?</Project>";
                    Match match = Regex.Match(modifiedContent, pattern, RegexOptions.Singleline);

                    if (match.Success)
                    {
                        modifiedContent = match.Value;
                    }
                }
            }

            File.WriteAllText(csprojFile, modifiedContent);
        }

        public static void NormalizeService(UserInput userInput)
        {
            var serviceFile = Path.Combine(userInput.ProjectLocation, $"Service.cs");

            var content = File.ReadAllText(serviceFile);
            var modifiedContent = content.Replace("namespace Project", $"namespace {userInput.ProjectName}");

            File.WriteAllText(serviceFile, modifiedContent);
        }

        public static void NormalizeProgram(UserInput userInput)
        {
            var programFile = Path.Combine(userInput.ProjectLocation, $"Program.cs");

            var content = File.ReadAllText(programFile);
            var modifiedContent = content.Replace("using Project", $"using {userInput.ProjectName}");

            File.WriteAllText(programFile, modifiedContent);
        }

        private static async Task CreateSolution(UserInput userInput)
        {
            await CommandHelper.Execute("dotnet", $"new sln -n {userInput.ProjectName} -o \"{userInput.ProjectLocation}\"");
        }

        private static async Task RenameCsprojAndAddToSolution(UserInput userInput)
        {
            var sourceProjectFile = Path.Combine(userInput.ProjectLocation, "Project.csproj");
            var targetProjectFile = Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj");
            File.Move(sourceProjectFile, targetProjectFile);

            await CommandHelper.Execute("dotnet", $"sln \"{userInput.ProjectLocation}/{userInput.ProjectName}.sln\" add \"{targetProjectFile}\"");
        }
    }
}
