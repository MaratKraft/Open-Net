using System.IO;
using System.Linq;
using System.Reflection;
namespace NetworkHub.Desktop;
public static class Notices
{
    public static string Text(){var assembly=Assembly.GetExecutingAssembly();return string.Join("\n\n",assembly.GetManifestResourceNames().Where(x=>x.StartsWith("OpenNet.Notice.")).OrderBy(x=>x).Select(name=>{using var stream=assembly.GetManifestResourceStream(name)!;using var reader=new StreamReader(stream);return name+"\n\n"+reader.ReadToEnd();}));}
}
