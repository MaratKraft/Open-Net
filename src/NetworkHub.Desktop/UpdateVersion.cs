using System;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace NetworkHub.Desktop;

// Strict SemVer precedence, independent of release dates and build metadata.
internal sealed class UpdateVersion : IComparable<UpdateVersion>
{
    static readonly Regex Pattern=new(@"^v?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$",RegexOptions.CultureInvariant);
    readonly BigInteger[] core;
    readonly string[] pre;
    UpdateVersion(BigInteger[] core,string[] pre){this.core=core;this.pre=pre;}
    internal static bool TryParse(string? text,out UpdateVersion? result)
    {
        result=null;if(text==null||text.Length>256)return false;var match=Pattern.Match(text);if(!match.Success)return false;
        var identifiers=match.Groups[4].Success?match.Groups[4].Value.Split('.'):Array.Empty<string>();
        if(identifiers.Any(x=>x.Length>1&&x[0]=='0'&&x.All(char.IsAsciiDigit)))return false;
        result=new UpdateVersion(Enumerable.Range(1,3).Select(i=>BigInteger.Parse(match.Groups[i].Value,System.Globalization.CultureInfo.InvariantCulture)).ToArray(),identifiers);return true;
    }
    public int CompareTo(UpdateVersion? other)
    {
        if(other==null)return 1;
        for(int i=0;i<3;i++){int c=core[i].CompareTo(other.core[i]);if(c!=0)return c;}
        if(pre.Length==0||other.pre.Length==0)return pre.Length==other.pre.Length?0:pre.Length==0?1:-1;
        for(int i=0;i<Math.Min(pre.Length,other.pre.Length);i++)
        {
            bool a=pre[i].All(char.IsAsciiDigit),b=other.pre[i].All(char.IsAsciiDigit);
            int c=a&&b?BigInteger.Parse(pre[i],System.Globalization.CultureInfo.InvariantCulture).CompareTo(BigInteger.Parse(other.pre[i],System.Globalization.CultureInfo.InvariantCulture)):a!=b?(a?-1:1):string.CompareOrdinal(pre[i],other.pre[i]);
            if(c!=0)return c;
        }
        return pre.Length.CompareTo(other.pre.Length);
    }
}
