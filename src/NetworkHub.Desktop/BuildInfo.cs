using System.Reflection;
namespace NetworkHub.Desktop;
internal static class BuildInfo
{
    internal static string Version=>(typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??typeof(BuildInfo).Assembly.GetName().Version?.ToString()??"").Split('+')[0];
    internal static string Caption=>"Open Net "+Version.Replace("-alpha."," Alpha.")+" · DNS Experiment";
}
