using System;
using System.IO;
namespace CyberGolden;
public static class FolderFeeder
{
    public static bool IsOrdinaryLocalEmptyFolder(string? candidate)
    {
        try
        {
            if(string.IsNullOrWhiteSpace(candidate)||!Path.IsPathFullyQualified(candidate)||candidate.StartsWith(@"\\"))return false;
            var path=Path.GetFullPath(candidate);
            if(!Directory.Exists(path))return false;
            var drive=new DriveInfo(Path.GetPathRoot(path)!);
            if(drive.DriveType is not (DriveType.Fixed or DriveType.Removable))return false;
            var attributes=File.GetAttributes(path);
            if((attributes&(FileAttributes.ReparsePoint|FileAttributes.System))!=0)return false;
            // Read-only enumeration; never create/delete/copy/move/rename or open for writing.
            using var entries=Directory.EnumerateFileSystemEntries(path).GetEnumerator();
            return !entries.MoveNext();
        }
        catch{return false;}
    }
}
