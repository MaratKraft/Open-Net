using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using NetworkHub.App;
namespace NetworkHub.Desktop;
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        #if OPENNET_SAFE_PREVIEW
        if(args.Any(a=>a is "--elevated" or "--dns-elevated" or "--dns-diagnostic" or "--installer-test" or "--self-test" or "--headless-tests" or "--autostart"))return 2;
#endif
        long startupTicks=System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            if(args.Length>0&&args[0]=="--dns-diagnostic")return DnsDiagnostics.Run(args);
            if(args.Length>0&&args[0]=="--dns-elevated")return DnsElevated.Run(args);
            if(args.Length>0&&args[0]=="--elevated")return ElevatedActions.Run(args);
#if OPENNET_TESTING
            if(args.Length>0&&args[0]=="--safe-stub")return SafeStub.Run(args.Skip(1).ToArray());
            #if OPENNET_SAFE_PREVIEW
            bool test=true,selfTest=false;
#else
            bool test=args.Contains("--test-mode"),selfTest=args.Contains("--self-test");
#endif
#else
            bool test=false;
#endif
            string root=Path.GetDirectoryName(Environment.ProcessPath!)!;string? data=null;
            bool autostart=args.Contains("--autostart");
            for(int i=0;i<args.Length;i++)
            {
                if(args[i] is "--autostart" or "--startup-probe" or "--close-probe")continue;
#if OPENNET_TESTING
                if(args[i] is "--test-mode" or "--self-test" or "--installer-test" or "--headless-tests")continue;
                if(args[i]=="--data-dir"&&i+1<args.Length){data=Path.GetFullPath(args[++i]);continue;}
#endif
                throw new ArgumentException(L.T("Unknown parameter. Start Open Net without command-line arguments."));
            }
#if OPENNET_TESTING
            if(args.Contains("--headless-tests")&&!test)throw new ArgumentException("Headless tests require safe mode.");
            if(selfTest&&!test)throw new ArgumentException(L.T("Self-test is allowed only in safe mode."));
            if(args.Contains("--installer-test")&&!selfTest)throw new ArgumentException("Installer-test requires self-test.");
            SelfTests.NetworkRequested=args.Contains("--installer-test");
            if(data!=null&&(!test||!NetworkHub.Core.Paths.Within(data,Path.Combine(root,"test-results"))))throw new ArgumentException(L.T("Test data must be in test-results next to the EXE."));
#endif
            #if OPENNET_SAFE_PREVIEW
            data ??=Path.Combine(root,"test-results","preview");
#endif
            var files=PortableStartup.Open(root,data);files.Log("Startup 0.4.0-alpha.1; executable="+Environment.ProcessPath+"; cwd="+Environment.CurrentDirectory+"; self-contained runtime="+System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
            AppIdentity.Apply(files.Log);
            using var instance=new SingleInstance(files.Data,files.Log);
            if(!instance.Primary)
            {
                if(autostart||instance.ActivateExisting())return 0;
                files.Log("Activation failed: "+instance.LastError);if(test)return 1;
                MessageBox.Show(L.T("Open Net is already open but did not respond. Check its tray icon."),"Open Net");return 1;
            }
            var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var shell=new Shell(files,test,instance);
#if OPENNET_TESTING
            if(args.Contains("--headless-tests")){try{return TestsHeadless.Run(shell,files);}finally{shell.Exit();shell.Runtime.CloseShell();shell.Dispose();}}
#endif
            app.DispatcherUnhandledException+=(_,e)=>{files.Log("UI: "+e.Exception);shell.ShowMessage(L.T("An interface error occurred. Check the diagnostic log and reopen the window."));e.Handled=true;};
            instance.Listen(()=>app.Dispatcher.BeginInvoke(new Action(shell.Restore)));
            bool tested=false;
#if OPENNET_TESTING
            if(selfTest)shell.Window.ContentRendered+=async(_,_)=>{if(tested)return;tested=true;await SelfTests.Run(shell,files,instance);};
#endif
            async Task Probe()
            {
                if(tested)return;tested=true;await Task.Delay(2500);await shell.Refresh();
                string path=Path.Combine(files.Data,"Logs","startup-probe.json");Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new{Title=shell.Window.Title,Visible=shell.Window.IsVisible,Tray=shell.Tray.Visible,Data=files.Data,Modules=files.Modules,StorageMode=files.Portable?"portable":"normal",StartedInTray=autostart,Activations=instance.Activations,Observations=shell.Observations,SettingsPath=files.SettingsPath,Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,TestMode=test,LaunchCount=shell.Runtime.LaunchCount,Language=shell.Settings.Language,ConfirmStartStop=shell.Settings.ConfirmStartStop,StartWithWindows=shell.Settings.StartWithWindows,Summary=shell.Find<System.Windows.Controls.TextBlock>("Summary").Text,ColdStartMilliseconds=System.Diagnostics.Stopwatch.GetElapsedTime(startupTicks).TotalMilliseconds-2500,ZapretVersion=shell.Installer.Current("zapret")?.Version,TgVersion=shell.Installer.Current("tg")?.Version}));
                if(args.Contains("--close-probe"))shell.Window.Close();else shell.Exit();
            }
            if(args.Contains("--startup-probe")&&!autostart)shell.Window.ContentRendered+=async(_,_)=>await Probe();
            try
            {
                if(autostart){app.MainWindow=shell.Window;shell.StartHidden();if(args.Contains("--startup-probe"))app.Dispatcher.BeginInvoke(new Action(async()=>await Probe()));app.Run();}
                else app.Run(shell.Window);
                return shell.ExitCode;
            }
            finally{shell.Runtime.CloseShell();shell.Dispose();files.Log("Shell exit; network processes left under explicit user control.");}
        }
        catch(Exception ex)
        {
#if OPENNET_TESTING
            if(args.Contains("--self-test")){int i=Array.IndexOf(args,"--data-dir");if(i>=0&&i+1<args.Length){string dir=Path.GetFullPath(args[i+1]),root=Path.GetDirectoryName(Environment.ProcessPath!)!;if(NetworkHub.Core.Paths.Within(dir,Path.Combine(root,"test-results")))File.WriteAllText(Path.Combine(dir,"startup-error.txt"),ex.ToString());}return 1;}
#endif
            string details="",diagnosticRoot=Path.GetDirectoryName(Environment.ProcessPath!)!;
            #if !OPENNET_SAFE_PREVIEW
            if(!File.Exists(Path.Combine(diagnosticRoot,"portable.flag")))try{string diagnostic=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Open Net","Startup-errors");Directory.CreateDirectory(diagnostic);File.WriteAllText(Path.Combine(diagnostic,DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+".log"),ex.ToString());details=L.T("\nDetails: ")+diagnostic;}catch(IOException){}catch(UnauthorizedAccessException){}
            #endif
            MessageBox.Show(NetworkHub.Core.LogSafety.Redact(L.Error(ex))+details,L.T("Open Net — error"),MessageBoxButton.OK,MessageBoxImage.Error);return 1;
        }
    }
}
