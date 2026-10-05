using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

// Use the Unicode shell interface: WScript.Shell rejects target paths outside
// the system ANSI code page even when C# source strings are correctly decoded.
public sealed class ShellShortcut : IDisposable {
 readonly string file;
 IShellLinkW link;
 public ShellShortcut(string path){
  file=Path.GetFullPath(path);
  link=(IShellLinkW)new ShellLink();
  try{if(File.Exists(file))((IPersistFile)link).Load(file,0);}
  catch{Dispose();throw;}
 }
 public string TargetPath {get{var text=new StringBuilder(32768);link.GetPath(text,text.Capacity,IntPtr.Zero,4);return text.ToString();}set{link.SetPath(value);}}
 public string WorkingDirectory {get{var text=new StringBuilder(32768);link.GetWorkingDirectory(text,text.Capacity);return text.ToString();}set{link.SetWorkingDirectory(value);}}
 public string Arguments {get{var text=new StringBuilder(32768);link.GetArguments(text,text.Capacity);return text.ToString();}set{link.SetArguments(value);}}
 public string Description {get{var text=new StringBuilder(32768);link.GetDescription(text,text.Capacity);return text.ToString();}set{link.SetDescription(value);}}
 public short Hotkey {get{short key;link.GetHotkey(out key);return key;}}
 public string IconLocation {
  get{var text=new StringBuilder(32768);int index;link.GetIconLocation(text,text.Capacity,out index);return text.ToString()+","+index;}
  set{int separator=value.LastIndexOf(','),index;if(separator<0||!Int32.TryParse(value.Substring(separator+1),out index))throw new ArgumentException("Invalid icon location");link.SetIconLocation(value.Substring(0,separator),index);}
 }
 public void Save(){var storage=(IPersistFile)link;storage.Save(file,true);storage.SaveCompleted(file);}
 public void Dispose(){if(link!=null){Marshal.FinalReleaseComObject(link);link=null;}}

 [ComImport,Guid("00021401-0000-0000-C000-000000000046")]
 class ShellLink {}
 [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IShellLinkW {
  void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int capacity,IntPtr findData,uint flags);
  void GetIDList(out IntPtr list);
  void SetIDList(IntPtr list);
  void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int capacity);
  void SetDescription([MarshalAs(UnmanagedType.LPWStr)]string text);
  void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int capacity);
  void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)]string path);
  void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder arguments,int capacity);
  void SetArguments([MarshalAs(UnmanagedType.LPWStr)]string arguments);
  void GetHotkey(out short key);
  void SetHotkey(short key);
  void GetShowCmd(out int command);
  void SetShowCmd(int command);
  void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int capacity,out int index);
  void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)]string path,int index);
  void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)]string path,uint reserved);
  void Resolve(IntPtr window,uint flags);
  void SetPath([MarshalAs(UnmanagedType.LPWStr)]string path);
 }
}
