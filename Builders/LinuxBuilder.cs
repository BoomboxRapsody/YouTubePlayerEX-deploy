using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NekoPlayer.Desktop.Deploy.Uploaders;

namespace NekoPlayer.Desktop.Deploy.Builders
{
    public class LinuxBuilder : Builder
    {
        private const string app_dir = "NekoPlayer.AppDir";
        private const string app_name = "NekoPlayer";
        private const string os_name = "linux";

        private readonly string stagingTarget;
        private readonly string publishTarget;

        public LinuxBuilder(string version)
            : base(version)
        {
            stagingTarget = Path.Combine(Program.StagingPath, app_dir);
            publishTarget = Path.Combine(stagingTarget, "usr", "bin");
        }

        protected override string TargetFramework => "net10.0";
        protected override string RuntimeIdentifier => $"{os_name}-x64";

        public override Uploader CreateUploader()
        {
            // Temporarily fix for zstd (current default) not being supported on some systems: https://github.com/ppy/osu/issues/30175
            // Todo: Remove with the next velopack release.
            const string extra_args = " --compression gzip";

            return new LinuxVelopackUploader(app_name, os_name, RuntimeIdentifier, RuntimeIdentifier, extraArgs: extra_args, stagingPath: stagingTarget);
        }

        // https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
        private static void copyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            var dir = new DirectoryInfo(sourceDir);

            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            DirectoryInfo[] dirs = dir.GetDirectories();

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    copyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        public override void Build()
        {
            if (Directory.Exists(stagingTarget))
                Directory.Delete(stagingTarget, true);

            copyDirectory(Path.Combine(Program.TemplatesPath, app_dir), stagingTarget, true);

            File.CreateSymbolicLink(Path.Combine(stagingTarget, ".DirIcon"), "youtube-player-ex.png");

            File.AppendAllLines(Path.Combine(stagingTarget, "NekoPlayer.desktop"), new[]
            {
                $"X-AppImage-Version={Version}"
            });

            Program.RunCommand("chmod", $"+x {stagingTarget}/AppRun");

            RunDotnetPublish(outputDir: publishTarget);
            AttachSatoriGC(outputDir: publishTarget);
        }
    }
}
