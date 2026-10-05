using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

static class Setup {
 [STAThread] static int Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  bool uninstall=Array.IndexOf(args,"--uninstall")>=0,worker=Array.IndexOf(args,"--uninstall-worker")>=0;
  if(uninstall&&!worker){
   try{
     if(MessageBox.Show("卸载 Codex Startup Windows？\n\n将删除本项目的程序和快捷方式，官方 Codex 不受影响。已保存图片与文字仍保留。","卸载启动动画",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return 0;
     string temporary=Path.Combine(Path.GetTempPath(),"CodexStartupUninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(Assembly.GetExecutingAssembly().Location,temporary);
     Process.Start(new ProcessStartInfo(temporary,"--uninstall-worker"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});return 0;
   }catch(Exception error){MessageBox.Show("卸载失败，请关闭动画启动器后重试。\n\n"+error.Message,"Codex Startup Windows",MessageBoxButtons.OK,MessageBoxIcon.Warning);return 1;}
  }
  bool acquired=false;
  using(var mutex=new Mutex(false,"Local\\CodexStartupWindowsSetup")){
   try{try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
    if(!acquired){MessageBox.Show("安装或卸载程序已经在运行。","Codex Startup Windows");return 1;}
    if(worker){
     Thread.Sleep(1500);
     InstallCore.Uninstall(InstallIntegration.DefaultRoot,null);
     InstallIntegration.Remove(InstallIntegration.DefaultRoot,InstallIntegration.Desktop,InstallIntegration.Menu,InstallIntegration.RegistryPath);
     MessageBox.Show("已卸载启动动画。官方 Codex 和已保存的图片设置保留。","Codex Startup Windows",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }else Application.Run(new SetupWindow());
   }catch(Exception error){MessageBox.Show(error.Message,"Codex Startup Windows",MessageBoxButtons.OK,MessageBoxIcon.Warning);return 1;}
   finally{if(acquired)mutex.ReleaseMutex();}
  }
  return 0;
 }
}
sealed class SetupWindow:Form {
 readonly Label heading=new Label(),description=new Label(),status=new Label();
 readonly ProgressBar progress=new ProgressBar();readonly Button primary=new Button(),close=new Button();
 bool busy=true,installed;
 public SetupWindow(){
  Text="Codex Startup Windows · "+InstallCore.Version;ClientSize=new Size(560,370);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",9);BackColor=Color.White;
  try{Icon=Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);}catch{}
  heading.SetBounds(28,26,500,34);heading.Text="给 Codex 一个你喜欢的开场";heading.Font=new Font(Font.FontFamily,17,FontStyle.Bold);
  description.SetBounds(30,80,500,110);description.Text="自定义头像、背景图片或 GIF 动图，以及欢迎文字。\n\n安装到当前用户目录，自动创建启动与设置入口。\n使用本机官方 Codex，无需管理员权限或另装 Node。";
  progress.SetBounds(30,207,500,8);progress.Style=ProgressBarStyle.Marquee;
  status.SetBounds(30,228,500,65);status.Text="正在安装，请稍候…";
  primary.SetBounds(292,312,156,34);primary.Text="启动并选择图片";primary.Enabled=false;primary.FlatStyle=FlatStyle.System;
  close.SetBounds(460,312,70,34);close.Text="关闭";close.Enabled=false;
  Controls.AddRange(new Control[]{heading,description,progress,status,primary,close});
  close.Click+=(sender,eventArgs)=>Close();primary.Click+=(sender,eventArgs)=>StartSettings();
  FormClosing+=(sender,eventArgs)=>{if(busy)eventArgs.Cancel=true;};
  Shown+=(sender,eventArgs)=>ThreadPool.QueueUserWorkItem(state=>Install());
 }
 void Install(){
  try{
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")){
    if(stream==null)throw new IOException("安装器缺少程序资源。");
    InstallCore.Install(stream,InstallIntegration.DefaultRoot,null,Assembly.GetExecutingAssembly().Location,()=>InstallIntegration.Register(InstallIntegration.DefaultRoot,InstallIntegration.Desktop,InstallIntegration.Menu,InstallIntegration.RegistryPath));
   }
   BeginInvoke((Action)(()=>{busy=false;installed=true;progress.Style=ProgressBarStyle.Blocks;progress.Value=100;status.Text="安装完成。首次使用前，请先在菜单或托盘中彻底退出 Codex。\n然后点击下方按钮，在官方窗口里选择图片。";primary.Enabled=true;close.Enabled=true;}));
  }catch(Exception error){
   BeginInvoke((Action)(()=>{busy=false;progress.Style=ProgressBarStyle.Blocks;status.Text="安装未完成："+error.Message;close.Enabled=true;primary.Text="打开安装目录";primary.Enabled=Directory.Exists(InstallIntegration.DefaultRoot);}));
  }
 }
 void StartSettings(){
  try{
   if(!installed){Process.Start(new ProcessStartInfo("explorer.exe","\""+InstallIntegration.DefaultRoot+"\""){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden});return;}
   Process.Start(new ProcessStartInfo(Path.Combine(InstallIntegration.DefaultRoot,"CodexStartup.exe"),"--settings"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});Close();
  }catch(Exception error){MessageBox.Show(error.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}
 }
}
