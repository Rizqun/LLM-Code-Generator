using System.Reflection;

namespace Test.Helpers
{
    public class FileHelper
    {
        public static string AssemblyDirectory
        {
            get
            {
                return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "/";
            }
        }

        public static void Copy(string sourceDirectory, string targetDirectory)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);
            DirectoryInfo diTarget = new DirectoryInfo(targetDirectory);

            CopyAll(diSource, diTarget);
        }

        public static void CopyAll(DirectoryInfo source, DirectoryInfo target)
        {
            Directory.CreateDirectory(target.FullName);

            foreach (FileInfo fi in source.GetFiles())
            {
                try
                {
                    fi.CopyTo(Path.Combine(target.FullName, fi.Name), false);
                }
                catch (IOException)
                {
                    // file already exist
                }
            }

            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }
    }
}
