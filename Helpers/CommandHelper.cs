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

            using (var process = new Process())
            {
                process.StartInfo = info;

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        if (e.Data.Contains("error"))
                        {
                            errorBuilder.AppendLine(e.Data);
                        }
                        else
                        {
                            returnValue.AppendLine(e.Data);
                        }
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for the process to exit.
                await process.WaitForExitAsync();

                return (returnValue.ToString(), errorBuilder.ToString());
            }
        }
    }
}
