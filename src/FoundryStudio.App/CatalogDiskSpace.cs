using System.IO;

namespace FoundryStudio.App;

public static class CatalogDiskSpace
{
    public static double? GetFreeDiskGb(string? cacheDirectory)
    {
        if (string.IsNullOrWhiteSpace(cacheDirectory))
        {
            return null;
        }

        try
        {
            var probe = new DirectoryInfo(cacheDirectory);
            while (probe is not null && !probe.Exists)
            {
                probe = probe.Parent;
            }

            if (probe is null)
            {
                return null;
            }

            var drive = new DriveInfo(probe.FullName);
            return drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
