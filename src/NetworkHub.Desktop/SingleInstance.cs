using NetworkHub.App;
using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace NetworkHub.Desktop;
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly string pipe;
    private readonly CancellationTokenSource cancel=new();
    private readonly Action<string> log;
    private Task? listener;
    public bool Primary {get;}
    public int Activations {get;private set;}
    public string LastError {get;private set;}="";
    public SingleInstance(string data,Action<string>? log=null)
    {
        this.log=log??(_=>{});
        using var identity=WindowsIdentity.GetCurrent();
        string key=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.User!.Value+"|"+data.ToUpperInvariant()))).Substring(0,24);
        pipe="OpenNet-0.2-"+key;mutex=new Mutex(false,"Local\\"+pipe);
        try{Primary=mutex.WaitOne(0);}catch(AbandonedMutexException){Primary=true;this.log("Recovered abandoned instance mutex.");}
    }
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int processId);
    public bool ActivateExisting()
    {
        try
        {
            using var client=new NamedPipeClientStream(".",pipe,PipeDirection.InOut,PipeOptions.CurrentUserOnly);
            client.Connect(3000); using var reader=new StreamReader(client,new UTF8Encoding(false),false,1024,true); using var writer=new StreamWriter(client,new UTF8Encoding(false),1024,true) {AutoFlush=true};
            string? Read(){var task=reader.ReadLineAsync();if(!task.Wait(3000))throw new TimeoutException(L.T("The first instance did not respond. Check its tray icon."));return task.Result;}
            writer.WriteLine("hello"); if(int.TryParse(Read(),out int pid)) AllowSetForegroundWindow(pid);
            writer.WriteLine("activate"); return Read()=="ok";
        }
        catch(Exception ex) {LastError=ex.Message;return false;}
    }
    public void Listen(Action activate)
    {
        listener=Task.Run(async()=>
        {
            while(!cancel.IsCancellationRequested)
            {
                try
                {
                    using var server=new NamedPipeServerStream(pipe,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(cancel.Token);
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel.Token); timeout.CancelAfter(3000);
                    using var reader=new StreamReader(server,new UTF8Encoding(false),false,1024,true); using var writer=new StreamWriter(server,new UTF8Encoding(false),1024,true) {AutoFlush=true};
                    if(await reader.ReadLineAsync(timeout.Token)!="hello") continue;
                    await writer.WriteLineAsync(Environment.ProcessId.ToString());
                    if(await reader.ReadLineAsync(timeout.Token)!="activate") continue;
                    Activations++; activate(); await writer.WriteLineAsync("ok");
                }
                catch(OperationCanceledException){if(cancel.IsCancellationRequested)break;log("Activation client timed out.");}
                catch(Exception ex){log("Activation pipe: "+ex.Message);if(cancel.IsCancellationRequested)break;await Task.Delay(100);}
            }
        });
    }
    public void Dispose() {cancel.Cancel();if(listener!=null&&!listener.Wait(1500))log("Activation listener shutdown pending.");if(Primary)mutex.ReleaseMutex();mutex.Dispose();if(listener==null||listener.IsCompleted)cancel.Dispose();}
}




