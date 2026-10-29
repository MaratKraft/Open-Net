using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
namespace NetworkHub.Desktop;

// Read-only inventory: a hosts entry overrides DNS; it does not prove a site is broken.
internal static class HostsInspection
{
 internal static readonly string[] Domains={"chatgpt.com","openai.com","oaistatic.com","oaiusercontent.com","gemini.google.com","generativelanguage.googleapis.com","alkalimakersuite-pa.clients6.google.com"};
 internal static string[] Parse(IEnumerable<string> lines)
 {
  var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var line in lines){var content=line.Split('#')[0];var parts=content.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);if(parts.Length<2||!IPAddress.TryParse(parts[0],out _))continue;foreach(var raw in parts.Skip(1)){var host=raw.TrimEnd('.');if(Domains.Any(d=>host.Equals(d,StringComparison.OrdinalIgnoreCase)||host.EndsWith("."+d,StringComparison.OrdinalIgnoreCase)))found.Add(host);}}
  return found.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
 }
#if OPENNET_TESTING
 internal static Func<string[]>? ReadForTests;
#endif
 internal static string[] Read(){
#if OPENNET_TESTING
  if(ReadForTests!=null)return ReadForTests();
#endif
  return Parse(File.ReadLines(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"drivers","etc","hosts")));
 }
}
