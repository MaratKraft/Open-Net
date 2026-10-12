using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
namespace NetworkHub.Desktop;
public static class SafeStub
{
    public static int Run(string[] args)
    {
        string Value(string key){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:"";}
        string gate=Value("--gate"),stop=Value("--stop");if(gate==""||stop=="")return 2;
        bool hang=Array.IndexOf(args,"--hang")>=0;var timer=Stopwatch.StartNew();
        while(!File.Exists(gate)){if(timer.ElapsedMilliseconds>10000)return 98;Thread.Sleep(25);}
        if(Array.IndexOf(args,"--child")>=0)
        {
            var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
            foreach(string item in new[]{"--safe-stub","--gate",gate,"--stop",stop,"--exit-code","0"})info.ArgumentList.Add(item);
            if(hang)info.ArgumentList.Add("--hang");using var child=Process.Start(info);
        }
        while(hang||!File.Exists(stop))Thread.Sleep(25);
        return int.TryParse(Value("--exit-code"),out int code)?code:0;
    }
}




