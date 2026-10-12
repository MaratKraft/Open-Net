using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace NetworkHub.App;
public static class StrategyList
{
    public const string Preferred="general (ALT13).bat";
    public static string[] Read(string folder)=>Directory.Exists(folder)?Directory.EnumerateFiles(folder,"general*.bat").Select(Path.GetFileName).Where(x=>x!=null).Cast<string>().OrderBy(x=>x.Equals("general.bat",StringComparison.OrdinalIgnoreCase)?0:x.Equals("general (ALT).bat",StringComparison.OrdinalIgnoreCase)?1:2).ThenBy(x=>x,NaturalComparer.Instance).ToArray():Array.Empty<string>();
    sealed class NaturalComparer:IComparer<string>
    {
        public static readonly NaturalComparer Instance=new();
        public int Compare(string? a,string? b)
        {
            var x=Regex.Split(a??"",@"(\d+)");var y=Regex.Split(b??"",@"(\d+)");
            for(int i=0;i<Math.Min(x.Length,y.Length);i++)
            {
                int order;
                if(i%2==1){string left=x[i].TrimStart('0'),right=y[i].TrimStart('0');order=left.Length.CompareTo(right.Length);if(order==0)order=StringComparer.Ordinal.Compare(left,right);}
                else order=StringComparer.OrdinalIgnoreCase.Compare(x[i],y[i]);
                if(order!=0)return order;
            }
            int result=x.Length.CompareTo(y.Length);return result!=0?result:StringComparer.OrdinalIgnoreCase.Compare(a,b);
        }
    }
}
