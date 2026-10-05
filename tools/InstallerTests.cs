using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;
static class InstallerTests {
 static int checks;
 static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
 static void Throws(Action action,string message){bool threw=false;try{action();}catch{threw=true;}Check(threw,message);}
 static MemoryStream Zip(params string[] pairs){var output=new MemoryStream();using(var archive=new ZipArchive(output,ZipArchiveMode.Create,true)){for(int i=0;i<pairs.Length;i+=2){using(var writer=new StreamWriter(archive.CreateEntry(pairs[i]).Open(),new UTF8Encoding(false))){writer.Write(pairs[i+1]);}}}output.Position=0;return output;}
 static void Put(string root,string name,string text){string path=Path.Combine(root,name);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text);}
 static int Main(){
  string temporary=Path.Combine(Path.GetTempPath(),"CodexStartupTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temporary);
  try{
   foreach(string entry in new[]{"../outside.txt","/absolute.txt","C:\\outside.txt","nested/../../outside.txt","assets/file.txt:stream","assets\\..\\outside.txt"}){Throws(()=>InstallCore.ResolveEntry(temporary,entry),"reject archive path "+entry);}
   string root=Path.Combine(temporary,"first");
   using(var zip=Zip("CodexStartup.exe","launcher-v1","assets/art.jpg","art-v1","obsolete.txt","old")){InstallCore.Install(zip,root);}
   Check(File.ReadAllText(Path.Combine(root,"CodexStartup.exe"))=="launcher-v1","first installation writes runtime");
   Check(File.Exists(Path.Combine(root,InstallCore.ManifestName)),"first installation records owned files");
   Put(root,"custom.txt","user data");
   using(var zip=Zip("CodexStartup.exe","launcher-v2","assets/art.jpg","art-v2")){InstallCore.Install(zip,root);}
   Check(File.ReadAllText(Path.Combine(root,"CodexStartup.exe"))=="launcher-v2","upgrade replaces owned files");
   Check(!File.Exists(Path.Combine(root,"obsolete.txt")),"upgrade removes obsolete owned files");
   Check(File.ReadAllText(Path.Combine(root,"custom.txt"))=="user data","upgrade preserves unowned data");
   string manifest=File.ReadAllText(Path.Combine(root,InstallCore.ManifestName));
   using(var zip=Zip("CodexStartup.exe","broken-v3","assets/art.jpg","broken-art")){Throws(()=>InstallCore.Install(zip,root,name=>{if(name=="assets/art.jpg")throw new IOException("injected failure");}),"interrupted update fails");}
   Check(File.ReadAllText(Path.Combine(root,"CodexStartup.exe"))=="launcher-v2"&&File.ReadAllText(Path.Combine(root,"assets/art.jpg"))=="art-v2","failed update rolls back runtime");
   Check(File.ReadAllText(Path.Combine(root,InstallCore.ManifestName))==manifest,"failed update retains ownership manifest");
   using(var zip=Zip("CodexStartup.exe","broken-registration","assets/art.jpg","changed")){Throws(()=>InstallCore.Install(zip,root,null,null,()=>{throw new IOException("registration failed");}),"shell registration failure aborts installation");}
   Check(File.ReadAllText(Path.Combine(root,"CodexStartup.exe"))=="launcher-v2"&&File.ReadAllText(Path.Combine(root,InstallCore.ManifestName))==manifest,"shell registration failure rolls back runtime and manifest");
   using(var zip=Zip("CodexStartup.exe","v3","custom.txt","overwrite")){Throws(()=>InstallCore.Install(zip,root),"update cannot overwrite unowned file");}
   string foreign=Path.Combine(temporary,"foreign");Put(foreign,"other.exe","other app");
   using(var zip=Zip("CodexStartup.exe","v1")){Throws(()=>InstallCore.Install(zip,foreign),"cannot install into an unrelated nonempty directory");}
   using(var zip=Zip("../escaped.txt","bad")){Throws(()=>InstallCore.Install(zip,Path.Combine(temporary,"escape")),"extract rejects traversal before writing");}
   Check(!File.Exists(Path.Combine(temporary,"escaped.txt")),"no writes outside install destination");
   InstallCore.Uninstall(root,null);
   Check(!File.Exists(Path.Combine(root,"CodexStartup.exe"))&&!File.Exists(Path.Combine(root,InstallCore.ManifestName)),"uninstall removes owned runtime and marker");
   Check(File.ReadAllText(Path.Combine(root,"custom.txt"))=="user data","uninstall preserves unowned data");
   Throws(()=>InstallCore.Uninstall(foreign,null),"uninstall refuses unrelated directory");
   Console.WriteLine("Installer checks="+checks);return 0;
  }catch(Exception error){Console.Error.WriteLine(error);return 1;}
  finally{if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
 }
}
