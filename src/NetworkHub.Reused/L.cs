using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace NetworkHub.App;
// English keys are the fallback text. Resources can be added without screen changes.
public static class L
{
    static readonly Dictionary<string,Dictionary<string,string>> resources=new();
    static L()
    {
        foreach(string name in Assembly.GetExecutingAssembly().GetManifestResourceNames())
        {
            if(!name.StartsWith("OpenNet.Locale.",StringComparison.Ordinal)||!name.EndsWith(".json"))continue;
            string language=name.Substring("OpenNet.Locale.".Length).Replace(".json","");
            using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)!;
            resources[language]=JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;
        }
    }
    public static string Language {get;private set;}="en";
    public static event Action? Changed;
    public static void Set(string? language){Language=language!=null&&resources.ContainsKey(language)?language:"en";Changed?.Invoke();}
    public static string T(string english)=>resources.TryGetValue(Language,out var dictionary)&&dictionary.TryGetValue(english,out var text)?text:english;
    public static IReadOnlyDictionary<string,string> Russian=>resources["ru"];
    public static string Current(string text){string? key=Russian.ContainsKey(text)?text:Russian.FirstOrDefault(x=>x.Value==text).Key;return key==null?text:T(key);}
    public static string Error(Exception ex)
    {
        if(ex is System.Net.Http.HttpRequestException or OperationCanceledException)return NetworkErrors.Message(ex);
        // Our known validation messages are translated where constructed; never expose OS stack traces.
        if(ex is System.ComponentModel.Win32Exception w&&w.NativeErrorCode==1223)return T("The UAC request was cancelled.");
        if(ex is UnauthorizedAccessException)return T("Access was denied. Choose a writable folder or check file permissions; see the diagnostic log.");
        if(ex is System.Text.Json.JsonException)return T("Configuration data is damaged. Restore a backup or save the settings again; see the diagnostic log.");
        string message=ex.Message;
        if(Russian.ContainsKey(message)||Russian.Values.Contains(message))return Current(message);
        // Untranslated technical exceptions belong in the log, with a short localized remedy in the UI.
        return T("The operation could not be completed. Check the selected files and diagnostic log, then retry.");
    }
}
