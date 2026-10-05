using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;

public static class InstallIntegration {
 public const string RegistryPath="Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\CodexStartupWindows";
 public static string DefaultRoot {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexStartupWindows","app");}}
 public static string Desktop {get{return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);}}
 public static string Menu {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Codex Startup Windows");}}
 static string[] Names={"Codex Startup Windows.lnk","Codex Startup Windows 设置.lnk"};
 static bool Owned(dynamic link,string target,bool settings){return String.Equals((string)link.TargetPath,target,StringComparison.OrdinalIgnoreCase)&&(string)link.Arguments==(settings?"--settings":"")&&String.Equals((string)link.WorkingDirectory,Path.GetDirectoryName(target),StringComparison.OrdinalIgnoreCase)&&String.Equals((string)link.IconLocation,target+",0",StringComparison.OrdinalIgnoreCase)&&String.IsNullOrEmpty((string)link.Hotkey);}
 static void Shortcut(string file,string target,bool settings){
  Directory.CreateDirectory(Path.GetDirectoryName(file));
  dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));dynamic link=null;
  try{
   link=shell.CreateShortcut(file);
   if(File.Exists(file)&&!String.Equals((string)link.TargetPath,target,StringComparison.OrdinalIgnoreCase))throw new IOException("已有同名快捷方式指向其他程序："+file);
   if(File.Exists(file)&&!Owned(link,target,settings))return;
   link.TargetPath=target;link.WorkingDirectory=Path.GetDirectoryName(target);link.Arguments=settings?"--settings":"";link.IconLocation=target+",0";link.Description=settings?"选择头像、背景图片、动图和启动文字":"启动官方 Codex 并播放自定义动画";link.Save();
  }finally{if(link!=null)Marshal.FinalReleaseComObject(link);Marshal.FinalReleaseComObject(shell);}
 }
 static void RegisterUnsafe(string root,string desktop,string menu,string registryPath,Action<string> beforeWrite){
  string target=Path.Combine(root,"CodexStartup.exe");string setup=Path.Combine(root,"CodexStartupSetup.exe");
  if(!File.Exists(target)||!File.Exists(setup)||!File.Exists(Path.Combine(root,InstallCore.ManifestName)))throw new IOException("安装文件不完整");
  foreach(string directory in new[]{desktop,menu})for(int i=0;i<Names.Length;i++){string file=Path.Combine(directory,Names[i]);if(beforeWrite!=null)beforeWrite(file);Shortcut(file,target,i==1);}
  if(beforeWrite!=null)beforeWrite("registry");
  using(var key=Registry.CurrentUser.CreateSubKey(registryPath)){
   key.SetValue("DisplayName","Codex Startup Windows");key.SetValue("DisplayVersion",InstallCore.Version);key.SetValue("Publisher","Codex Startup Windows community project");key.SetValue("InstallLocation",root);key.SetValue("DisplayIcon",target+",0");key.SetValue("UninstallString","\""+setup+"\" --uninstall");key.SetValue("NoModify",1,RegistryValueKind.DWord);key.SetValue("NoRepair",1,RegistryValueKind.DWord);
  }
 }
 public static void Register(string root,string desktop,string menu,string registryPath,Action<string> beforeWrite=null){
  var files=new Dictionary<string,byte[]>();
  foreach(string directory in new[]{desktop,menu})foreach(string name in Names){string file=Path.Combine(directory,name);files.Add(file,File.Exists(file)?File.ReadAllBytes(file):null);}
  var values=new Dictionary<string,object>();var kinds=new Dictionary<string,RegistryValueKind>();bool existed;
  using(var key=Registry.CurrentUser.OpenSubKey(registryPath)){
   existed=key!=null;
   if(existed){if(!String.Equals((string)key.GetValue("InstallLocation"),root,StringComparison.OrdinalIgnoreCase))throw new IOException("已有卸载注册信息属于其他程序或安装位置。");foreach(string name in key.GetValueNames()){values[name]=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);kinds[name]=key.GetValueKind(name);}}
  }
  bool hadDesktop=Directory.Exists(desktop),hadMenu=Directory.Exists(menu);
  try{RegisterUnsafe(root,desktop,menu,registryPath,beforeWrite);}
  catch(Exception failure){
   var errors=new List<Exception>();
   foreach(var entry in files){try{if(entry.Value!=null)File.WriteAllBytes(entry.Key,entry.Value);else if(File.Exists(entry.Key))File.Delete(entry.Key);}catch(Exception error){errors.Add(error);}}
   try{
    if(!existed)Registry.CurrentUser.DeleteSubKeyTree(registryPath,false);
    else using(var key=Registry.CurrentUser.CreateSubKey(registryPath)){foreach(string name in key.GetValueNames().Where(name=>!values.ContainsKey(name)))key.DeleteValue(name);foreach(var value in values)key.SetValue(value.Key,value.Value,kinds[value.Key]);}
   }catch(Exception error){errors.Add(error);}
   foreach(string directory in new[]{hadDesktop?null:desktop,hadMenu?null:menu}){try{if(directory!=null&&Directory.Exists(directory)&&Directory.GetFileSystemEntries(directory).Length==0)Directory.Delete(directory);}catch(Exception error){errors.Add(error);}}
   if(errors.Count>0){errors.Insert(0,failure);throw new AggregateException("安装入口回滚未完成。",errors);}throw;
  }
 }
 public static void Remove(string root,string desktop,string menu,string registryPath){
  string target=Path.Combine(root,"CodexStartup.exe");
  foreach(string directory in new[]{desktop,menu})for(int i=0;i<Names.Length;i++){
   string name=Names[i];
   string file=Path.Combine(directory,name);if(!File.Exists(file))continue;
   dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));dynamic link=null;
   try{link=shell.CreateShortcut(file);if(Owned(link,target,i==1))File.Delete(file);}finally{if(link!=null)Marshal.FinalReleaseComObject(link);Marshal.FinalReleaseComObject(shell);}
  }
  if(Directory.Exists(menu)&&Directory.GetFileSystemEntries(menu).Length==0)Directory.Delete(menu);
  using(var key=Registry.CurrentUser.OpenSubKey(registryPath)){if(key!=null&&!String.Equals((string)key.GetValue("InstallLocation"),root,StringComparison.OrdinalIgnoreCase))throw new IOException("卸载注册信息属于其他安装位置。");}
  Registry.CurrentUser.DeleteSubKeyTree(registryPath,false);
 }
}
