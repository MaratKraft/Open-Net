using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using NetworkHub.App;
using NetworkHub.Core;
namespace NetworkHub.Desktop;
internal static class ModuleVersionsView
{
    internal static void Open(Shell shell,AppFiles files,string id,Window owner)
    {
        var window=SettingsLayout.Create(shell,L.T("Saved module versions"),420);window.Owner=owner;var body=SettingsLayout.Panel(window);SettingsLayout.Heading(body,L.T("Saved module versions"),L.T("Read-only inventory. Active modules, rollback copies and user files are not deleted."));
        try{var current=shell.Installer.Current(id);body.Children.Add(new TextBlock{Text=L.T("Current version")+": "+(current?.Version??L.T("Not installed")),TextWrapping=TextWrapping.Wrap});string folder=Paths.Owned(files.Data,"Backups/"+id);Paths.NoReparseAncestors(folder);int count=0;if(Directory.Exists(folder))foreach(string path in Directory.EnumerateFiles(folder,"*.json",SearchOption.TopDirectoryOnly)){if(++count>1000)throw new IOException("Too many saved version records");Paths.NoReparseAncestors(path);if(new FileInfo(path).Length>524288)throw new IOException("Version record is too large");var record=JsonSerializer.Deserialize<InstalledModule>(File.ReadAllText(path));if(record?.Module!=id)continue;body.Children.Add(new TextBlock{Text=record.Version+" · "+L.T("Saved rollback record"),Margin=new Thickness(0,12,0,0),TextWrapping=TextWrapping.Wrap});}if(count==0)body.Children.Add(new TextBlock{Text=L.T("No saved module versions."),Margin=new Thickness(0,12,0,0)});}catch(Exception ex){SettingsLayout.Error(window,body,"VersionsError").Text=L.Error(ex);}
        var done=SettingsLayout.Finish(window,"VersionsDone","Done");done.Click+=(_,_)=>window.Close();SettingsLayout.PinActions(window,SettingsLayout.Footer(window,done));LocalizedTree.Bind(window);ThemeManager.BindTree(window);window.Show();
    }
}
