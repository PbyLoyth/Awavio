namespace Awavio.Core;

static class AtomicFile
{
    /// <summary>Writes to a sibling temp file, then renames it over the target,
    /// so a crash never leaves a half-written file behind.</summary>
    public static void WriteAllText(string path, string contents)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, contents);
        File.Move(temp, path, overwrite: true);
    }
}
