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

        public static async Task NormalizeGeneratedFile(UserInput userInput)
        {
            await NormalizeCsprojFile(userInput);
            await NormalizeService(userInput);
            await NormalizeProgram(userInput);
        }

        public static async Task NormalizeCsprojFile(UserInput userInput)
        {
            var csprojFile = Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.csproj");

            string content = await File.ReadAllTextAsync(csprojFile);
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

            await File.WriteAllTextAsync(csprojFile, modifiedContent);
        }

        public static async Task NormalizeService(UserInput userInput)
        {
            var serviceFile = Path.Combine(userInput.ProjectLocation, $"Service.cs");

            var content = await File.ReadAllTextAsync(serviceFile);
            var modifiedContent = content.Replace("namespace Project", $"namespace {userInput.ProjectName}");

            await File.WriteAllTextAsync(serviceFile, modifiedContent);
        }

        public static async Task NormalizeProgram(UserInput userInput)
        {
            var programFile = Path.Combine(userInput.ProjectLocation, $"Program.cs");

            var content = await File.ReadAllTextAsync(programFile);
            var modifiedContent = content.Replace("using Project", $"using {userInput.ProjectName}");

            await File.WriteAllTextAsync(programFile, modifiedContent);
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
