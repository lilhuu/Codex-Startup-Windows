using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
static class Launcher {
 [STAThread] static int Main(string[] args) {
  try {
   string root=AppDomain.CurrentDomain.BaseDirectory;
   string script=Path.Combine(root,"windows","launch.ps1");
   if(!File.Exists(script))throw new FileNotFoundException("启动文件不完整，请保留整个文件夹。",script);
   var info=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"));
   info.Arguments="-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+script+"\"";
   if(Array.IndexOf(args,"--settings")>=0)info.Arguments+=" -OpenSettings";
   info.UseShellExecute=false;info.CreateNoWindow=true;info.WindowStyle=ProcessWindowStyle.Hidden;info.WorkingDirectory=root;
   info.EnvironmentVariables["PSModulePath"]=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","Modules")+Path.PathSeparator+Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WindowsPowerShell","Modules");
   using(var helper=Process.Start(info)){helper.WaitForExit();return helper.ExitCode;}
  } catch(Exception error){MessageBox.Show(error.Message,"Codex Startup Windows",MessageBoxButtons.OK,MessageBoxIcon.Warning);return 1;}
 }
}
