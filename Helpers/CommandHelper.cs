using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace CodeGenerator.Helpers
{
    public class CommandHelper
    {
        public async static Task<string> Execute(string fileName, string args)
        {
            var returnValue = new StringBuilder();
            var errorBuilder = new StringBuilder();

            var info = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                Arguments = args,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(info))
            {
                if (process != null)
                {
                    var reader = process.StandardOutput;
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        returnValue.AppendLine(line);
                    }

                    var errorReader = process.StandardError;
                    while (!errorReader.EndOfStream)
                    {
                        var line = errorReader.ReadLine();
                        errorBuilder.AppendLine(line);
                    }
                }
            }

            var error = errorBuilder.ToString();

            if (!string.IsNullOrEmpty(error))
            {
                throw new Exception(error);
            }

            return returnValue.ToString();
        }
    }
}
