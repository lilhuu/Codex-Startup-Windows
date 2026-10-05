using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32;
using System.IO.Compression;
using System.Text;
using System.Linq;
static class InstallerIntegrationTests {
 static int checks;
 static void Check(bool value,string description){if(!value)throw new Exception(description);checks++;Console.WriteLine("PASS "+description);}
 static MemoryStream FailedUpdate(){var stream=new MemoryStream();using(var archive=new ZipArchive(stream,ZipArchiveMode.Create,true))using(var writer=new StreamWriter(archive.CreateEntry("CodexStartup.exe").Open(),new UTF8Encoding(false))){writer.Write("new runtime that must roll back");}stream.Position=0;return stream;}
 static int Main(string[] args){
  string temporary=Path.Combine(Path.GetTempPath(),"CodexStartupIntegration-"+Guid.NewGuid().ToString("N")),root=Path.Combine(temporary,"用户资料 \uD83D\uDE00 空格","应用"),desktop=Path.Combine(temporary,"桌面"),menu=Path.Combine(temporary,"开始菜单");
  string registry="Software\\CodexStartupWindowsTests\\"+Guid.NewGuid().ToString("N");
  try{
   var assembly=Assembly.LoadFile(Path.GetFullPath(args[0]));
   using(var payload=assembly.GetManifestResourceStream("payload.zip")){Check(payload!=null,"installer contains embedded runtime ZIP");InstallCore.Install(payload,root,null,Path.GetFullPath(args[0]));}
   Check(File.Exists(Path.Combine(root,"CodexStartup.exe"))&&File.Exists(Path.Combine(root,"image-settings.js")),"real embedded runtime installs into Unicode path");
   Check(File.Exists(Path.Combine(root,"CodexStartupSetup.exe")),"installed setup retained for uninstall");
   Console.WriteLine("Diagnostic shortcut path: "+Path.Combine(desktop,"Codex Startup Windows.lnk"));
   Console.WriteLine("Diagnostic target path: "+Path.Combine(root,"CodexStartup.exe"));
   InstallIntegration.Register(root,desktop,menu,registry);
   byte[] originalLauncher=File.ReadAllBytes(Path.Combine(root,"CodexStartup.exe")),originalLink=File.ReadAllBytes(Path.Combine(desktop,"Codex Startup Windows.lnk"));
   bool failed=false;
   using(var update=FailedUpdate()){try{InstallCore.Install(update,root,null,Path.GetFullPath(args[0]),()=>InstallIntegration.Register(root,desktop,menu,registry,name=>{if(name=="registry")throw new IOException("injected registry failure");}));}catch(IOException){failed=true;}}
   Check(failed,"registry failure after shortcut writes aborts the complete install");
   Check(File.ReadAllBytes(Path.Combine(root,"CodexStartup.exe")).SequenceEqual(originalLauncher),"shell failure restores original installed runtime");
   Check(File.ReadAllBytes(Path.Combine(desktop,"Codex Startup Windows.lnk")).SequenceEqual(originalLink),"shell failure restores original shortcut bytes");
   using(var start=new ShellShortcut(Path.Combine(desktop,"Codex Startup Windows.lnk")))
   using(var settings=new ShellShortcut(Path.Combine(desktop,"Codex Startup Windows 设置.lnk"))){
    Check(start.TargetPath==Path.Combine(root,"CodexStartup.exe"),"desktop shortcut targets installed launcher");
    Check(settings.Arguments=="--settings","settings shortcut selects settings mode");
    Check(start.IconLocation.StartsWith(root),"shortcut icon uses stable installed path");
    settings.Arguments="--user-customized-argument";settings.Save();
   }
   using(var key=Registry.CurrentUser.OpenSubKey(registry)){Check((string)key.GetValue("InstallLocation")==root,"uninstall registration points to the user install");}
   InstallIntegration.Remove(root,desktop,menu,registry);
   Check(!File.Exists(Path.Combine(desktop,"Codex Startup Windows.lnk")),"uninstall removes owned shortcut");
   Check(File.Exists(Path.Combine(desktop,"Codex Startup Windows 设置.lnk")),"uninstall preserves shortcut arguments customized by user");
   using(var key=Registry.CurrentUser.OpenSubKey(registry)){Check(key==null,"uninstall removes its registry entry");}
   InstallCore.Uninstall(root,null);Check(!Directory.Exists(root),"real runtime uninstall removes an empty installation");
   Console.WriteLine("Installer integration checks="+checks);return 0;
  }catch(Exception error){Console.Error.WriteLine(error);return 1;}
  finally{Registry.CurrentUser.DeleteSubKeyTree(registry,false);if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
 }
}
