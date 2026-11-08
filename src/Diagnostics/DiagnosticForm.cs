using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CharacterVision {
 static class ReadOnlyCapture {
  [StructLayout(LayoutKind.Sequential)]struct Rect{public int L,T,R,B;}
  [DllImport("user32.dll")]public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr h,out uint id);
  [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr h,out Rect r);
  [DllImport("user32.dll")]static extern bool ClientToScreen(IntPtr h,ref Point point);
  public static void Save(string path){
   var window=GetForegroundWindow();uint id;GetWindowThreadProcessId(window,out id);
   using(var p=Process.GetProcessById((int)id))if(!string.Equals(Path.GetFileName(p.MainModule.FileName),"Wow.exe",StringComparison.OrdinalIgnoreCase))throw new Exception("前台不是 Wow.exe，未截图。请在倒计时内切换到游戏。");
   Rect r;var point=Point.Empty;if(!GetClientRect(window,out r)||!ClientToScreen(window,ref point)||r.R<300||r.B<200)throw new Exception("无法读取游戏客户区。");
   using(var b=new Bitmap(r.R,r.B)){using(var g=Graphics.FromImage(b))g.CopyFromScreen(point,Point.Empty,b.Size);if(GetForegroundWindow()!=window)throw new Exception("截图时前台发生变化，请重试。");b.Save(path);}
  }
 }
 sealed class DiagnosticForm:Form {
  readonly Button open=new Button{Text="导入截图并识别",AutoSize=true},capture=new Button{Text="3 秒后读取前台游戏",AutoSize=true},folder=new Button{Text="打开诊断记录",AutoSize=true};
  readonly Label status=new Label{Text="就绪。仅识别画面，不点击游戏、不提交建号。",AutoSize=true,Dock=DockStyle.Fill};
  readonly PictureBox picture=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(25,30,38)};
  readonly TextBox summary=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,WordWrap=true};
  readonly string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data","diagnostics");
  public DiagnosticForm(string initialImage=null){Text="建角识别诊断 · 0.1.0 预览 · 只读";Font=new Font("Microsoft YaHei UI",11);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(1150,760);MinimumSize=new Size(780,520);
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(12)};layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(layout);
   var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top};buttons.Controls.AddRange(new Control[]{open,capture,folder});layout.Controls.Add(buttons,0,0);layout.Controls.Add(status,0,1);
   var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Vertical};split.Panel1.Controls.Add(picture);split.Panel2.Controls.Add(summary);layout.Controls.Add(split,0,2);Shown+=async(s,e)=>{split.SplitterDistance=(int)(split.Width*.6);if(initialImage!=null)await Recognize(initialImage);};
   open.Click+=async(s,e)=>{using(var dialog=new OpenFileDialog{Filter="截图|*.png;*.jpg;*.jpeg;*.bmp"})if(dialog.ShowDialog(this)==DialogResult.OK)await Recognize(dialog.FileName);};
   capture.Click+=async(s,e)=>{SetBusy(true);try{for(int i=3;i>0;i--){status.Text=i+" 秒后截图，请切换到游戏；不会自动激活窗口。";await Task.Delay(1000);if(IsDisposed)return;}Directory.CreateDirectory(root);string path=Path.Combine(root,"capture-"+Guid.NewGuid().ToString("N")+".png");ReadOnlyCapture.Save(path);await Recognize(path);}catch(Exception ex){if(!IsDisposed)status.Text=ex.Message;}finally{if(!IsDisposed)SetBusy(false);}};
   folder.Click+=(s,e)=>{Directory.CreateDirectory(root);Process.Start(new ProcessStartInfo(root){UseShellExecute=true});};
  }
  void SetBusy(bool busy){open.Enabled=capture.Enabled=!busy;}
  async Task Recognize(string path){SetBusy(true);status.Text="正在本地识别；无需联网。";try{var result=await Task.Run(()=>Recognition.Run(path,root));if(IsDisposed)return;using(var image=new Bitmap(Path.Combine(result.Session,"annotated.png"))){var old=picture.Image;picture.Image=new Bitmap(image);if(old!=null)old.Dispose();}summary.Text=Recognition.Summary(result);status.Text="已保存诊断记录："+Path.GetFileName(result.Session);}catch(Exception ex){if(!IsDisposed){status.Text="识别未完成";summary.Text=ex.Message;}}finally{if(!IsDisposed)SetBusy(false);}}
  protected override void Dispose(bool disposing){if(disposing&&picture.Image!=null){picture.Image.Dispose();picture.Image=null;}base.Dispose(disposing);}
 }
 static class DiagnosticProgram {
  [STAThread]static int Main(string[] args){ReadOnlyCapture.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   try{if(args.Length==3&&args[0]=="--recognize"){Recognition.Run(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]));return 0;}
    if(args.Length==1&&args[0]=="--self-test"){DiagnosticChecks.Run();return 0;}
    Application.Run(new DiagnosticForm(args.Length==2&&args[0]=="--open"?Path.GetFullPath(args[1]):null));return 0;
   }catch(Exception ex){string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data","diagnostics");Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"last-error.txt"),ex.ToString());if(args.Length==0)MessageBox.Show(ex.Message);return 1;}
  }
 }
}
