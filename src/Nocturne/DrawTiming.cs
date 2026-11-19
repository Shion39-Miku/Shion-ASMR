using System;
using System.Diagnostics;
namespace Nocturne;
internal static class DrawTiming
{
 internal static event Action<string,double>? Sample;
 internal static long Begin()=>Sample==null?0:Stopwatch.GetTimestamp();
 internal static void End(string name,long begin){if(begin!=0)Sample?.Invoke(name,Stopwatch.GetElapsedTime(begin).TotalMilliseconds);}
}
