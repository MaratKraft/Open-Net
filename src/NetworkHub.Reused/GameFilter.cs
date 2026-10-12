using NetworkHub.App;
using System;
using System.IO;
using System.Text.RegularExpressions;
using NetworkHub.Core;
namespace NetworkHub.App;
public static class GameFilter
{
    public static (string Tcp,string Udp) Read(string bundle)
    {
        string path=Path.Combine(bundle,"utils","game_filter.enabled");
        if(!File.Exists(path))return ("12","12");
        Paths.NoReparseAncestors(path);
        if(new FileInfo(path).Length>4096)throw new InvalidDataException(L.T("Invalid GameFilter settings. Review utils/game_filter.enabled before starting."));
        string mode="disabled",tcp="1024-65535",udp="1024-65535";
        foreach(string raw in File.ReadAllLines(path))
        {
            string line=raw.Trim();if(line==""||line.StartsWith(';'))continue;
            var parts=line.Split('=',2);string key=parts[0].ToLowerInvariant(),value=parts.Length==2?parts[1].Replace(" ",""):"";
            if(key=="mode")mode=value.ToLowerInvariant();
            else if(key=="all"&&value=="")mode="all";
            else if(key is "tcp" or "udp")
            {
                if(value=="")mode=key;
                else{Validate(value);if(key=="tcp")tcp=value;else udp=value;}
            }
            else throw new InvalidDataException(L.T("Invalid GameFilter settings. Review utils/game_filter.enabled before starting."));
        }
        if(mode is not ("disabled" or "all" or "tcp" or "udp"))throw new InvalidDataException(L.T("Invalid GameFilter settings. Review utils/game_filter.enabled before starting."));
        return (mode is "all" or "tcp"?tcp:"12",mode is "all" or "udp"?udp:"12");
    }
    static void Validate(string value)
    {
        foreach(string item in value.Split(','))
        {
            if(!Regex.IsMatch(item,@"^[1-9][0-9]{0,4}(-[1-9][0-9]{0,4})?$"))throw new InvalidDataException(L.T("Invalid GameFilter settings. Review utils/game_filter.enabled before starting."));
            var range=item.Split('-');int a=int.Parse(range[0]),b=range.Length==2?int.Parse(range[1]):a;
            if(a>65535||b>65535||a>b)throw new InvalidDataException(L.T("Invalid GameFilter settings. Review utils/game_filter.enabled before starting."));
        }
    }
}

