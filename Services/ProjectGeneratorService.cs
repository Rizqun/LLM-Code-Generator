using Test.Helpers;
using Test.Models;

namespace Test.Services
{
    public class ProjectGeneratorService
    {
        public static async Task<string> Create(UserInput userInput)
        {
            // Copy whole template
            var sourceFolder = Path.Combine(FileHelper.AssemblyDirectory, "Templates/Project");
            var targetFolder = userInput.ProjectLocation;

            FileHelper.Copy(sourceFolder, targetFolder);

            await CreateSolution(userInput);
            await RenameCsprojAndAddToSolution(userInput);

            return targetFolder;
        }

        private static async Task<string> CreateSolution(UserInput userInput)
        {
            if (File.Exists(Path.Combine(userInput.ProjectLocation, $"{userInput.ProjectName}.sln")))
                return "";

            return await CommandHelper.Execute("dotnet", $"new sln -n {userInput.ProjectName} -o \"{userInput.ProjectLocation}\"");
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
