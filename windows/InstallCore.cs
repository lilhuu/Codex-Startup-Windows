using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;

public sealed class InstalledFiles {
 public string Product {get;set;}
 public string Version {get;set;}
 public string[] Files {get;set;}
}
public static class InstallCore {
 public const string Product="CodexStartupWindows";
 public const string Version="1.0.0";
 public const string ManifestName="installed-files.json";
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
 public static string ResolveEntry(string root,string entry){
  if(String.IsNullOrWhiteSpace(entry)||Path.IsPathRooted(entry)||entry.IndexOf(':')>=0)throw new InvalidDataException("Archive entry is not relative: "+entry);
  string normalized=entry.Replace('\\','/');
  if(normalized.Split('/').Any(part=>part==".."||part=="."||part.Length==0||part.EndsWith(".")||part.EndsWith(" ")))throw new InvalidDataException("Unsafe archive entry: "+entry);
  string boundary=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  string destination=Path.GetFullPath(Path.Combine(root,normalized.Replace('/',Path.DirectorySeparatorChar)));
  if(!destination.StartsWith(boundary,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Archive path escapes destination");
  string current=destination;
  while(current!=null&&current.Length>=boundary.Length-1){
   if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("Refusing a linked destination: "+current);
   if(String.Equals(current,boundary.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))break;
   current=Path.GetDirectoryName(current);
  }
  return destination;
 }
 static InstalledFiles ReadManifest(string root){
  string path=ResolveEntry(root,ManifestName);
  if(!File.Exists(path))throw new IOException("This folder is not an installed Codex Startup Windows project.");
  var result=Json.Deserialize<InstalledFiles>(File.ReadAllText(path,Encoding.UTF8));
  if(result==null||result.Product!=Product||result.Files==null||result.Files.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=result.Files.Length)throw new InvalidDataException("Invalid installation ownership manifest");
  foreach(string name in result.Files){ResolveEntry(root,name);if(name.Equals(ManifestName,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid manifest entry");}
  return result;
 }
 public static InstalledFiles Install(Stream payload,string target,Action<string> beforeWrite=null,string setupPath=null,Action afterWrite=null){
  target=Path.GetFullPath(target);
  var previous=File.Exists(Path.Combine(target,ManifestName))?ReadManifest(target):null;
  if(previous==null&&Directory.Exists(target)&&Directory.EnumerateFileSystemEntries(target).Any())throw new IOException("Installation folder contains unrelated files.");
  string parent=Path.GetDirectoryName(target);Directory.CreateDirectory(parent);
  string stage=Path.Combine(parent,".codex-startup-stage-"+Guid.NewGuid().ToString("N"));
  string backup=Path.Combine(parent,".codex-startup-backup-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(stage);Directory.CreateDirectory(backup);
  var changed=new List<string>();bool created=!Directory.Exists(target),committed=false;
  try{
   var files=new List<string>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   using(var zip=new ZipArchive(payload,ZipArchiveMode.Read,true)){
    foreach(var entry in zip.Entries){
     if(entry.FullName.EndsWith("/")){continue;}
     string name=entry.FullName.Replace('\\','/');string output=ResolveEntry(stage,name);
     if(!seen.Add(name)||name.Equals(ManifestName,StringComparison.OrdinalIgnoreCase)||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("Duplicate or invalid archive entry: "+name);
     Directory.CreateDirectory(Path.GetDirectoryName(output));
     using(var input=entry.Open())using(var file=File.Create(output)){input.CopyTo(file);}
     files.Add(name);
    }
   }
   if(files.Count==0)throw new InvalidDataException("Empty runtime archive");
   if(setupPath!=null){string name="CodexStartupSetup.exe";if(!seen.Add(name))throw new InvalidDataException("Setup cannot be embedded in itself");File.Copy(setupPath,ResolveEntry(stage,name));files.Add(name);}
   var owned=new HashSet<string>(previous==null?new string[0]:previous.Files,StringComparer.OrdinalIgnoreCase);
   foreach(string name in files){string destination=ResolveEntry(target,name);if(File.Exists(destination)&&!owned.Contains(name))throw new IOException("Would overwrite an unrelated file: "+name);}
   var installed=new InstalledFiles {Product=Product,Version=Version,Files=files.ToArray()};
   File.WriteAllText(ResolveEntry(stage,ManifestName),Json.Serialize(installed),new UTF8Encoding(false));
   Directory.CreateDirectory(target);
   foreach(string name in files.Concat(new[]{ManifestName})){
    string destination=ResolveEntry(target,name),saved=ResolveEntry(backup,name);
    Directory.CreateDirectory(Path.GetDirectoryName(destination));Directory.CreateDirectory(Path.GetDirectoryName(saved));
    if(beforeWrite!=null)beforeWrite(name);
    if(File.Exists(destination))File.Replace(ResolveEntry(stage,name),destination,saved);
    else File.Move(ResolveEntry(stage,name),destination);
    changed.Add(name);
   }
   foreach(string name in owned.Where(name=>!seen.Contains(name))){
    string destination=ResolveEntry(target,name),saved=ResolveEntry(backup,name);
    if(File.Exists(destination)){Directory.CreateDirectory(Path.GetDirectoryName(saved));File.Move(destination,saved);changed.Add(name);}
   }
   if(afterWrite!=null)afterWrite();
   committed=true;return installed;
  }catch(Exception failure){
   var errors=new List<Exception>();
   foreach(string name in changed.AsEnumerable().Reverse()){
    try{string saved=ResolveEntry(backup,name),destination=ResolveEntry(target,name);if(File.Exists(saved)){if(File.Exists(destination))File.Replace(saved,destination,null);else File.Move(saved,destination);}else if(File.Exists(destination))File.Delete(destination);}catch(Exception error){errors.Add(error);}
   }
   if(errors.Count>0){errors.Insert(0,failure);throw new AggregateException("Installation failed; recovery copies retained at "+backup,errors);}
   if(created)RemoveEmptyDirectories(target);
   throw;
  }finally{
   if(Directory.Exists(stage))Directory.Delete(stage,true);
   // A backup with files after rollback failure is retained for manual recovery.
   if(Directory.Exists(backup)&&(committed||!Directory.EnumerateFiles(backup,"*",SearchOption.AllDirectories).Any()))Directory.Delete(backup,true);
  }
 }
 public static void Uninstall(string root,string runningPath){
  root=Path.GetFullPath(root);var manifest=ReadManifest(root);
  // Check all paths before removing any files.
  foreach(string name in manifest.Files)ResolveEntry(root,name);
  foreach(string name in manifest.Files){string path=ResolveEntry(root,name);if(runningPath!=null&&path.Equals(Path.GetFullPath(runningPath),StringComparison.OrdinalIgnoreCase))continue;if(File.Exists(path))File.Delete(path);}
  File.Delete(ResolveEntry(root,ManifestName));RemoveEmptyDirectories(root);
 }
 static void RemoveEmptyDirectories(string root){
  if(!Directory.Exists(root))return;
  foreach(string directory in Directory.GetDirectories(root)){if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)==0)RemoveEmptyDirectories(directory);}
  if(!Directory.EnumerateFileSystemEntries(root).Any())Directory.Delete(root);
 }
}
