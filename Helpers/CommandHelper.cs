using System.Diagnostics;
using System.Text;

namespace CodeGenerator.Helpers
{
    public class CommandHelper
    {
        public async static Task<(string output, string error)> Execute(string fileName, string args)
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
                    var errorReader = process.StandardError;
                    while (!errorReader.EndOfStream)
                    {
                        var line = errorReader.ReadLine();
                        errorBuilder.AppendLine(line);
                    }

                    var reader = process.StandardOutput;
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();

                        if (!string.IsNullOrEmpty(line))
                        {
                            if (line.Contains("error"))
                            {
                                errorBuilder.AppendLine(line);
                            }
                            else
                            {
                                returnValue.AppendLine(line);
                            }
                        }
                    }
                }
            }

            var error = errorBuilder.ToString();

            return (returnValue.ToString(), error);
        }
    }
}
