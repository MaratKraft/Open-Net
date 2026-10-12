using System;
using System.Diagnostics;
namespace NetworkHub.Desktop;

// The destination is fixed; no user input is passed to the Windows shell.
public static class RepositoryLink
{
    public const string Address="https://github.com/MaratKraft/Open-Net";
    public static ProcessStartInfo Plan()=>new(Address){UseShellExecute=true,Verb="open"};
    public static void Open(){using var process=Process.Start(Plan());}
}
