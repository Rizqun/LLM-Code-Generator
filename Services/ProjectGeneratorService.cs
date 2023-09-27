﻿using CodeGenerator.Helpers;
using CodeGenerator.Models;

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

        public static void NormalizeCsprojFile(string path)
        {
            string content = File.ReadAllText(path);
            content = content.Trim();
            content = content.Replace("Version=\"5.0.0\"", "Version=\"2.0.0\"");
            string modifiedContent = content;

            if (!content.StartsWith("<Project"))
                modifiedContent = "<Project Sdk=\"Microsoft.NET.Sdk\">\n" + content + "\n</Project>";

            File.WriteAllText(path, modifiedContent);
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
