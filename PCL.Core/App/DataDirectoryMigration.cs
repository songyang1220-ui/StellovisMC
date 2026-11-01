using System;
using System.IO;

namespace PCL.Core.App;

internal static class DataDirectoryMigration
{
    internal static string Prepare(string executableDirectory)
    {
        var data = Path.Combine(executableDirectory, Paths.DataDirectoryName);
        var legacy = Path.Combine(executableDirectory, "PCL");
        if (Directory.Exists(legacy))
        {
            if (!Directory.Exists(data)) Directory.Move(legacy, data);
            else
            {
                CopyMissing(legacy, data);
                // Preserve conflicting old files instead of replacing the user's newer settings.
                Directory.Move(legacy, data + ".legacy-" + Guid.NewGuid().ToString("N"));
            }
        }
        Directory.CreateDirectory(data);
        return data;
    }

    private static void CopyMissing(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            var target = Path.Combine(destination, Path.GetFileName(file));
            if (!File.Exists(target)) File.Copy(file, target);
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Cannot merge a linked data directory: " + directory);
            CopyMissing(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
