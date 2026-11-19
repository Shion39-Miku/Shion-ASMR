using System;using System.Diagnostics;using System.IO;using System.Text;using System.Reflection;
[assembly:AssemblyTitle("月咲 Shion")][assembly:AssemblyProduct("月咲 Shion")]
class Launcher
{
 static string Quote(string value){var b=new StringBuilder("\"");int slashes=0;foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"'){b.Append('\\',slashes*2+1);b.Append(c);slashes=0;continue;}b.Append('\\',slashes);slashes=0;b.Append(c);}b.Append('\\',slashes*2);b.Append('"');return b.ToString();}
 [STAThread] static void Main(string[] args){var root=AppDomain.CurrentDomain.BaseDirectory;var path=Path.Combine(root,"runtime","Shion.exe");var b=new StringBuilder();foreach(var arg in args){if(b.Length>0)b.Append(' ');b.Append(Quote(arg));}try{Process.Start(new ProcessStartInfo(path,b.ToString()){WorkingDirectory=Path.GetDirectoryName(path),UseShellExecute=false});}catch(Exception e){System.Windows.Forms.MessageBox.Show("无法启动月咲 Shion："+e.Message+"\n请保留完整发行目录。","月咲 Shion");}}
}
