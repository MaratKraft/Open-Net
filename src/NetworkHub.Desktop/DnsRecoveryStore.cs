using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using NetworkHub.Core;
using NetworkHub.Dns;

namespace NetworkHub.Desktop;

// Authoritative recovery is machine-local even in Portable mode. The UI has read access only.
// User-writable Data backups are informational and are never accepted as restore instructions.
internal static class DnsRecoveryStore
{
    static SecurityIdentifier Admin=>new(WellKnownSidType.BuiltinAdministratorsSid,null);
    static SecurityIdentifier System=>new(WellKnownSidType.LocalSystemSid,null);
    internal static string Owner()=>WindowsIdentity.GetCurrent().User!.Value;
    static string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Open Net DNS Recovery");
    internal static string DirectoryFor(string owner){var sid=new SecurityIdentifier(owner);return Path.Combine(Root,sid.Value);}
    internal static IDnsJournalStore Open(string owner,bool create)
    {
        string directory=DirectoryFor(owner);Paths.NoReparseAncestors(directory);
        if(create){
            if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException();
            CreateProtected(Root,null);CreateProtected(directory,new SecurityIdentifier(owner));
        }
        if(Directory.Exists(Root))Validate(Root,null);
        if(Directory.Exists(directory))Validate(directory,new SecurityIdentifier(owner));
        return new DnsFileJournalStore(directory,owner,path=>ProtectFile(path,new SecurityIdentifier(owner)),adapter=>ValidateRecord(adapter,owner));
    }
    internal static void ProtectFile(string path,SecurityIdentifier reader){var acl=new FileSecurity();acl.SetAccessRuleProtection(true,false);acl.SetOwner(Admin);foreach(var sid in new[]{Admin,System})acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,AccessControlType.Allow));acl.AddAccessRule(new FileSystemAccessRule(reader,FileSystemRights.Read,AccessControlType.Allow));new FileInfo(path).SetAccessControl(acl);}
    internal static void MarkUsed(string owner,string token)
    {
        if(!Guid.TryParseExact(token,"N",out _))throw new InvalidDataException("Invalid DNS nonce");
        string path=Path.Combine(DirectoryFor(owner),"nonce-"+token+".used");Paths.NoReparseAncestors(path);
        if(File.Exists(path))throw new DnsException("This DNS request has already been processed. Refresh and retry.");
        using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough);ProtectFile(path,new SecurityIdentifier(owner));file.WriteByte(1);file.Flush(true);
    }
    static DirectorySecurity Security(SecurityIdentifier? reader)
    {
        var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);acl.SetOwner(Admin);
        var inheritance=InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit;
        foreach(var sid in new[]{Admin,System})acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,inheritance,PropagationFlags.None,AccessControlType.Allow));
        if(reader!=null)acl.AddAccessRule(new FileSystemAccessRule(reader,FileSystemRights.ReadAndExecute,inheritance,PropagationFlags.None,AccessControlType.Allow));
        else acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null),FileSystemRights.ReadAndExecute,InheritanceFlags.None,PropagationFlags.None,AccessControlType.Allow));
        return acl;
    }
    static void CreateProtected(string path,SecurityIdentifier? reader){if(!Directory.Exists(path))new DirectoryInfo(path).Create(Security(reader));Validate(path,reader);}
    static void Validate(string path,SecurityIdentifier? reader)
    {
        Paths.NoReparseAncestors(path);var acl=new DirectoryInfo(path).GetAccessControl();var owner=acl.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier??throw new InvalidDataException("Missing DNS recovery owner");
        if(!owner.Equals(Admin)&&!owner.Equals(System))throw new InvalidDataException("The DNS recovery folder is not protected. Application was blocked.");
        foreach(FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier))){
            if(rule.AccessControlType!=AccessControlType.Allow)continue;
            var sid=(SecurityIdentifier)rule.IdentityReference;
            if(sid.Equals(Admin)||sid.Equals(System))continue;
            const FileSystemRights write=FileSystemRights.Write|FileSystemRights.Delete|FileSystemRights.DeleteSubdirectoriesAndFiles|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership;
            if((rule.FileSystemRights&write)!=0||reader!=null&&!sid.Equals(reader))throw new InvalidDataException("The DNS recovery folder permits untrusted access. Application was blocked.");
        }
    }
    internal static void ValidateRecord(Guid adapter,string owner)
    {
        string path=Path.Combine(DirectoryFor(owner),adapter.ToString("D")+".json");if(!File.Exists(path))return;Paths.NoReparseAncestors(path);
        var acl=new FileInfo(path).GetAccessControl();var recordOwner=acl.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier??throw new InvalidDataException("Missing DNS recovery owner");
        if(!recordOwner.Equals(Admin)&&!recordOwner.Equals(System))throw new InvalidDataException("The DNS recovery record is not protected.");
        foreach(FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier))){if(rule.AccessControlType!=AccessControlType.Allow)continue;var sid=(SecurityIdentifier)rule.IdentityReference;if(sid.Equals(Admin)||sid.Equals(System))continue;const FileSystemRights writes=FileSystemRights.Write|FileSystemRights.Delete|FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership;if((rule.FileSystemRights&writes)!=0||sid.Value!=owner)throw new InvalidDataException("The DNS recovery record permits untrusted access.");}
    }
}
