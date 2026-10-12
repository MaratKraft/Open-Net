using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NetworkHub.App;

namespace NetworkHub.Desktop;
public static class PortableStartup
{
    public static AppFiles Open(string root,string? testData=null)
    {
        if(testData!=null)return new AppFiles(root,testData);
        bool portable=File.Exists(Path.Combine(root,"portable.flag"));
        string data=portable?Path.Combine(root,"Data"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Open Net");
        try
        {
            Directory.CreateDirectory(data);string probe=Path.Combine(data,Guid.NewGuid().ToString("N")+".probe");
            using(var stream=new FileStream(probe,FileMode.CreateNew,FileAccess.Write,FileShare.None,1,FileOptions.DeleteOnClose))stream.WriteByte(1);
            string modules=Path.Combine(portable?root:data,"Modules");Directory.CreateDirectory(modules);
            using(var stream=new FileStream(Path.Combine(modules,Guid.NewGuid().ToString("N")+".probe"),FileMode.CreateNew,FileAccess.Write,FileShare.None,1,FileOptions.DeleteOnClose))stream.WriteByte(1);
            var files=new AppFiles(root,data);
            foreach(string name in new[]{"Config","Logs","Cache","Downloads","Backups"})Directory.CreateDirectory(Path.Combine(data,name));
            return files;
        }
        catch(Exception ex) when(ex is UnauthorizedAccessException or IOException)
        {
            throw new IOException(L.T(portable?"The portable folder is not writable. Move the complete Open Net folder to a writable location; no AppData fallback was used.":"The user data folder is not writable. Check its permissions and free space."),ex);
        }
    }
    public static void ImportSettings(AppFiles files,string path)
    {
        var settings=JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path),new JsonSerializerOptions{UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow})??throw new InvalidDataException(L.T("The settings file is damaged. Choose a current Open Net settings file."));
        string backup=Path.Combine(files.Data,"Backups","Imports",DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));Directory.CreateDirectory(backup);
        if(File.Exists(files.SettingsPath))File.Copy(files.SettingsPath,Path.Combine(backup,"current-settings.json"));
        File.Copy(path,Path.Combine(backup,"settings.json"));
        settings.ZapretDirectory=files.Resolve(settings.ZapretDirectory);settings.TgExe=files.Resolve(settings.TgExe);
        files.Save(settings);files.Log("Imported current settings with backup: "+backup);
    }
}




