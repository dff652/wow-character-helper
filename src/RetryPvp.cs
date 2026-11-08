// Build with the Windows .NET Framework C# compiler. No external packages.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text;

static class Native {
 // Task.Run workers must opt into physical coordinates independently of the WinForms UI thread.
 [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [ThreadStatic] static bool physicalCoordinates;
 public static void EnsurePhysicalCoordinates(){if(physicalCoordinates)return;if(SetThreadDpiAwarenessContext(new IntPtr(-4))==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"无法启用物理像素坐标，停止操作。");physicalCoordinates=true;}
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a,uint b,bool attach);
 [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
 [DllImport("user32.dll")] static extern bool PeekMessage(out Msg m,IntPtr h,uint first,uint last,uint remove);
 [StructLayout(LayoutKind.Sequential)] struct Msg{public IntPtr h;public uint message;public UIntPtr w;public IntPtr l;public uint time;public Point pt;public uint extra;}
 public static void FocusWindow(IntPtr h){
  Msg msg;PeekMessage(out msg,IntPtr.Zero,0,0,0);
  uint id;uint foregroundThread=GetWindowThreadProcessId(GetForegroundWindow(),out id),ownThread=GetCurrentThreadId(),targetThread=GetWindowThreadProcessId(h,out id);
  bool attached=foregroundThread!=0&&foregroundThread!=ownThread&&AttachThreadInput(ownThread,foregroundThread,true);
  bool targetAttached=targetThread!=0&&targetThread!=ownThread&&targetThread!=foregroundThread&&AttachThreadInput(ownThread,targetThread,true);
  try{if(IsIconic(h))ShowWindow(h,9);BringWindowToTop(h);SetForegroundWindow(h);}finally{if(targetAttached)AttachThreadInput(ownThread,targetThread,false);if(attached)AttachThreadInput(ownThread,foregroundThread,false);}
 }
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint id);
 [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out Rect r);
 public delegate bool WindowCallback(IntPtr h,IntPtr param);
 [DllImport("user32.dll")] public static extern bool EnumWindows(WindowCallback callback,IntPtr param);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,StringBuilder text,int count);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h,ref Point p);
 [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point p);
 [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h,uint flags);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int k);
 [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
 [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll",SetLastError=true)] public static extern uint SendInput(uint n,Input[] a,int size);
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int L,T,R,B; }
 [StructLayout(LayoutKind.Sequential)] public struct Input {public uint type; public Union u;}
 [StructLayout(LayoutKind.Explicit)] public struct Union {
  [FieldOffset(0)] public Mouse m; [FieldOffset(0)] public Key k;
 }
 [StructLayout(LayoutKind.Sequential)] public struct Mouse {public int x,y;public uint data,flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] public struct Key {public ushort vk,scan;public uint flags,time;public UIntPtr extra;}
 public static void Send(params Input[] a) {if(SendInput((uint)a.Length,a,Marshal.SizeOf(typeof(Input)))!=a.Length) throw new Exception("Windows 未接受输入，已停止。");}
 public static Input K(ushort vk,ushort scan,uint flags) {return new Input{type=1,u=new Union{k=new Key{vk=vk,scan=scan,flags=flags}}};}
 public static Input M(uint flags) {return new Input{type=0,u=new Union{m=new Mouse{flags=flags}}};}
}

// Binary text matching: foreground strokes and background must both agree.
// Local search tolerates small UI shifts; an unsupported layout fails closed.
sealed class Mask {
 public int W,H; public byte[] V; public int[] Sum;public Mask Gold;
 public Mask(Bitmap b,bool gold=false) {
  W=b.Width;H=b.Height;V=new byte[W*H];Sum=new int[(W+1)*(H+1)];
  var d=b.LockBits(new Rectangle(0,0,W,H),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
  byte[] bytes=new byte[Math.Abs(d.Stride)*H];Marshal.Copy(d.Scan0,bytes,0,bytes.Length);b.UnlockBits(d);
  for(int y=0;y<H;y++)for(int x=0;x<W;x++) {
   int p=y*d.Stride+x*4;int blue=bytes[p],g=bytes[p+1],r=bytes[p+2];
   byte v=(byte)((gold?(r>190&&g>175&&blue<120):(r>155&&g>145&&(blue>145||blue<g*.8)))?1:0);V[y*W+x]=v;
   int q=(y+1)*(W+1)+x+1;Sum[q]=v+Sum[q-1]+Sum[q-W-1]-Sum[q-W-2];
  }
  if(!gold)Gold=new Mask(b,true);
 }
 public int Count(int x,int y,int w,int h) {int s=W+1;return Sum[(y+h)*s+x+w]-Sum[y*s+x+w]-Sum[(y+h)*s+x]+Sum[y*s+x];}
}
sealed class Patch {
 public string Name;public int X,Y,W,H;public Point[] On;
 public Patch(string line,string dir) {
  string[] v=line.Split('|');Name=v[0];X=int.Parse(v[1]);Y=int.Parse(v[2]);W=int.Parse(v[3]);H=int.Parse(v[4]);
  using(var b=new Bitmap(Path.Combine(dir,Name+".png"))) {var m=new Mask(b,Name.StartsWith("blocked")||Name.StartsWith("loading"));var ps=new List<Point>();for(int y=0;y<H;y++)for(int x=0;x<W;x++)if(m.V[y*W+x]!=0)ps.Add(new Point(x,y));On=ps.ToArray();}
  if(On.Length<15)throw new Exception("识别样本为空："+Name);
 }
 public Point? Find(Mask m,double threshold=0.78,int shiftX=0) {
  if((Name.StartsWith("blocked")||Name.StartsWith("loading"))&&m.Gold!=null)m=m.Gold;
  double best=threshold;Point? at=null;
  for(int y=Math.Max(0,Y-24);y<=Math.Min(m.H-H,Y+24);y++)for(int x=Math.Max(0,X+shiftX-24);x<=Math.Min(m.W-W,X+shiftX+24);x++) {
   int count=m.Count(x,y,W,H);if(count<On.Length*.65||count>On.Length*1.5)continue;
   int hit=0;foreach(var p in On) hit+=m.V[(y+p.Y)*m.W+x+p.X];
   double score=2.0*hit/(count+On.Length);if(score>best){best=score;at=new Point(x,y);}
  }
  return at;
 }
}
sealed class Vision {
 public Dictionary<string,Patch> P=new Dictionary<string,Patch>();
 public Vision(string dir){foreach(string line in File.ReadAllLines(Path.Combine(dir,"index.txt"))){var p=new Patch(line,dir);P.Add(p.Name,p);}}
 public IEnumerable<Patch> Candidates(string n){yield return P[n];for(int i=2;P.ContainsKey(n+i);i++)yield return P[n+i];}
 public bool Has(Mask m,string n){return Candidates(n).Any(p=>p.Find(m).HasValue);}
 public string State(Mask m){
  if(Has(m,"loading"))return "loading";
  if(Has(m,"blocked")||Has(m,"blockedOld"))return "blocked";
  if(Has(m,"rules")&&Has(m,"pvp"))return "rules";
  if(Has(m,"raceHeader")&&Has(m,"appearance"))return "race";
  if(Has(m,"custom")&&Has(m,"skin"))return "custom";
  if(Has(m,"ruleButton")&&Has(m,"create")&&m.Count(900,675,760,80)<40)return "list";
  return "unknown";
 }
}
sealed class Runner {
 readonly Vision v;readonly Action<string> log;readonly CancellationToken ct;readonly int interval,max;readonly bool restart;
 readonly CreationConfig config;readonly Vision profile;DateTime? endAt;
 public readonly FlowTrace Flow=new FlowTrace();
 public int AttemptLimit {get{return max;}}
 public volatile int AttemptNumber,BlockedCount;long nextTicks;public DateTime? NextAttemptUtc {get{long ticks=Interlocked.Read(ref nextTicks);return ticks==0?(DateTime?)null:new DateTime(ticks,DateTimeKind.Utc);}}
 string observedState;int observedWidth,observedHeight,step;readonly string session=DateTime.Now.ToString("yyyyMMdd-HHmmss");
 IntPtr target;Point origin;Mask latest;string state;double scaleX=1,scaleY=1;
 string GamePath {get{return config.GamePath;}}
 string LauncherPath {get{return config.LauncherPath;}}
 public Runner(Vision v,Action<string> log,CancellationToken ct,int interval,int max,bool restart):this(v,log,ct,new CreationConfig{IntervalSeconds=interval,MaxAttempts=max,Restart=restart}){}
 public Runner(Vision v,Action<string> log,CancellationToken ct,CreationConfig c){this.v=v;this.log=log;this.ct=ct;config=c.Copy();interval=c.IntervalSeconds;max=c.MaxAttempts;restart=c.Restart;if(!c.Builtin&&CalibrationStore.Ready(c))profile=new Vision(CalibrationStore.Dir(c));}
 void Check(){Native.EnsurePhysicalCoordinates();ct.ThrowIfCancellationRequested();if(endAt.HasValue&&DateTime.UtcNow>=endAt.Value)throw new Exception("已达到最长运行时长，已停止。");if((Native.GetAsyncKeyState(27)&0x8000)!=0||(Native.GetAsyncKeyState(120)&0x8000)!=0)throw new OperationCanceledException();if(target!=IntPtr.Zero&&Native.GetForegroundWindow()!=target){uint a,b;Native.GetWindowThreadProcessId(target,out a);var front=Native.GetForegroundWindow();Native.GetWindowThreadProcessId(front,out b);if(a!=0&&a==b)return;var title=new StringBuilder(512);Native.GetWindowText(front,title,512);throw new Exception("游戏或战网失去前台，已停止。当前窗口："+title);}}
 void Delay(int ms){var timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<ms){Check();int left=ms-(int)timer.ElapsedMilliseconds;if(left>0)Thread.Sleep(Math.Min(100,left));}Check();}
 void Bind(){
  target=UniqueWindow(GamePath);if(target==IntPtr.Zero)throw new Exception("游戏主窗口已消失，请检查游戏是否退出后重试。");Check();uint id;Native.GetWindowThreadProcessId(target,out id);
  using(var p=Process.GetProcessById((int)id))if(!string.Equals(p.MainModule.FileName,GamePath,StringComparison.OrdinalIgnoreCase))throw new Exception("请先切到《魔兽世界：无限》，再按 F8。");
 }
 List<Process> FindProcesses(string path){
  var found=new List<Process>();
  foreach(var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path))){
   try{if(string.Equals(p.MainModule.FileName,path,StringComparison.OrdinalIgnoreCase)){found.Add(p);continue;}}
   catch(System.ComponentModel.Win32Exception){p.Dispose();throw new Exception("无法核对游戏或战网进程，已停止。");}
   catch(InvalidOperationException){}
   p.Dispose();
  }
  return found;
 }
 bool GameExists(){var all=FindProcesses(GamePath);bool yes=all.Count>0;foreach(var p in all)p.Dispose();return yes;}
 GameStartupSnapshot InspectGameStartup(){
  var all=FindProcesses(GamePath);
  try{
   var snapshot=new GameStartupSnapshot{ProcessIds=all.Select(p=>p.Id).ToArray()};var ids=new HashSet<uint>(snapshot.ProcessIds.Select(x=>(uint)x));
   var windows=new List<IntPtr>();var titles=new List<string>();
   Native.EnumWindows((h,param)=>{uint id;Native.GetWindowThreadProcessId(h,out id);if(ids.Contains(id)&&Native.IsWindowVisible(h)){var text=new StringBuilder(512);Native.GetWindowText(h,text,512);titles.Add("PID "+id+"「"+text+"」");if(text.ToString()=="魔兽世界")windows.Add(h);}return true;},IntPtr.Zero);
   if(windows.Count>1)throw new Exception("发现多个无限版游戏主窗口，请只保留一个后重试。");
   snapshot.Window=windows.Count==1?windows[0]:IntPtr.Zero;if(titles.Count>0)snapshot.WindowSummary=string.Join(" / ",titles);return snapshot;
  }finally{foreach(var p in all)p.Dispose();}
 }
 public string StartupStatus(){return InspectGameStartup().Describe();}
 void EnsureGameReady(){
  Flow.Begin("launch","检查游戏窗口，必要时从战网启动。");target=IntPtr.Zero;Check();
  var h=GameStartup.Resolve(InspectGameStartup,Delay,log);
  if(h==IntPtr.Zero){LaunchGame();return;}
  Native.FocusWindow(h);Thread.Sleep(500);target=h;Bind();WaitForGamePage();Flow.Finish("已完成","已复用游戏窗口，页面就绪。");
 }
 IntPtr UniqueWindow(string path){
  var all=FindProcesses(path);try{var ids=new HashSet<uint>(all.Select(p=>(uint)p.Id));var windows=new List<IntPtr>();string title=path==GamePath?"魔兽世界":"战网";
   Native.EnumWindows((h,param)=>{uint id;Native.GetWindowThreadProcessId(h,out id);if(ids.Contains(id)&&Native.IsWindowVisible(h)){var text=new StringBuilder(512);Native.GetWindowText(h,text,512);if(text.ToString()==title)windows.Add(h);}return true;},IntPtr.Zero);
   if(windows.Count>1)throw new Exception("发现多个目标窗口，已停止。");return windows.Count==1?windows[0]:IntPtr.Zero;
  }finally{foreach(var p in all)p.Dispose();}
 }
 void ShowLauncher(){
  target=IntPtr.Zero;Check();var h=UniqueWindow(LauncherPath);
  if(h==IntPtr.Zero)throw new Exception("请先打开并登录战网，选择《魔兽世界：无限》WoW3 账号。");
  Native.FocusWindow(h);Thread.Sleep(500);target=h;Check();
 }
 Bitmap CaptureLauncher(){Check();Native.Rect r;if(!Native.GetWindowRect(target,out r)||r.R-r.L<700||r.B-r.T<500)throw new Exception("战网窗口尺寸异常。");origin=new Point(r.L,r.T);var b=new Bitmap(r.R-r.L,r.B-r.T,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(b))g.CopyFromScreen(origin,Point.Empty,b.Size);Check();return b;}
 public void SnapshotLauncher(){ShowLauncher();using(var b=CaptureLauncher())b.Save(Path.Combine(AppPaths.Screenshots,"调试-战网页面.png"));}
 public void SelectInfiniteLauncher(){
  ShowLauncher();Rectangle icon;using(var b=CaptureLauncher())icon=LauncherVision.Icon(b,Check);
  if(icon.IsEmpty)throw new Exception("未找到战网顶部的无限版图标，请将无限版加入收藏并保持图标可见。");
  ClickLauncher(new Point(origin.X+icon.Left+icon.Width/2,origin.Y+icon.Top+icon.Height/2));Delay(1500);
  using(var b=CaptureLauncher())b.Save(Path.Combine(AppPaths.Screenshots,"调试-战网选择无限.png"));
 }
 void ClickLauncher(Point p){Check();if(Native.GetAncestor(Native.WindowFromPoint(p),2)!=target)throw new Exception("战网控件被遮挡，已停止。");Native.SetCursorPos(p.X,p.Y);Native.Send(Native.M(2),Native.M(4));}
 public void SnapshotGame(){target=UniqueWindow(GamePath);if(target==IntPtr.Zero)throw new Exception("未找到游戏窗口。");Native.FocusWindow(target);Thread.Sleep(500);Check();Native.Rect r;Native.GetClientRect(target,out r);File.WriteAllText(Path.Combine(AppPaths.Logs,"调试-尺寸.txt"),r.R+"x"+r.B);using(var b=CaptureLauncher())b.Save(Path.Combine(AppPaths.Screenshots,"调试-游戏原图.png"));}
 void CloseBlockedGame(){Flow.Begin("exit","关闭游戏，并等待进程退出。");
  Wait("blocked",10);uint id;Native.GetWindowThreadProcessId(target,out id);
  using(var p=Process.GetProcessById((int)id)){
   if(!string.Equals(p.MainModule.FileName,GamePath,StringComparison.OrdinalIgnoreCase))throw new Exception("退出前核对游戏失败。");
   log("已确认阵营限制，正在正常退出游戏。");Check();
   if(!Native.PostMessage(target,0x10,IntPtr.Zero,IntPtr.Zero))throw new Exception("发送退出请求失败，已停止。");
   target=IntPtr.Zero;var end=DateTime.UtcNow.AddSeconds(30);
   while(!p.HasExited&&DateTime.UtcNow<end)Delay(200);
   if(!p.HasExited){
    if(UniqueWindow(GamePath)!=IntPtr.Zero)throw new Exception("游戏主窗口仍存在，已停止，不会强制关闭。");
    log("正常退出后主窗口已消失，清理该游戏的残留后台进程。");Check();p.Kill();
    end=DateTime.UtcNow.AddSeconds(10);while(!p.HasExited&&DateTime.UtcNow<end)Delay(200);
    if(!p.HasExited)throw new Exception("残留进程未退出，已停止，避免重复启动。");
   }
  }
 }
 Point LaunchButton(){
  Check();log("定位战网无限版图标，自动切换并核对 WoW3 (CN)。");SelectInfiniteLauncher();
  using(var b=CaptureLauncher()){var play=LauncherVision.Play(b,Check);return new Point(origin.X+play.X+play.Width/2,origin.Y+play.Y+play.Height/2);}
 }
 public void CheckLauncherOnly(){LaunchButton();log("PASS: 已选择无限版并核对账号和启动按钮；未启动游戏。");}
 public void LaunchOnly(){ShowLauncher();LaunchGame();log("PASS: 已进入无限版建角页面；未选择角色或提交建号。");}
 static bool SameProcess(IntPtr a,IntPtr b){if(a==IntPtr.Zero||b==IntPtr.Zero)return false;uint x,y;Native.GetWindowThreadProcessId(a,out x);Native.GetWindowThreadProcessId(b,out y);return x!=0&&x==y;}
 void LaunchGame(){Flow.Begin("launch","从战网启动并等待游戏页面。");
  Check();if(GameExists())throw new Exception("游戏仍在运行，已停止，避免重复启动。");
  var p=LaunchButton();
  if(Native.GetAncestor(Native.WindowFromPoint(p),2)!=target)throw new Exception("战网启动按钮被遮挡，已停止。");
  Check();Native.SetCursorPos(p.X,p.Y);Native.Send(Native.M(2),Native.M(4));log("已点击战网进入游戏，等待自动进入规则集页面。");
  IntPtr launcher=target;target=IntPtr.Zero;var end=DateTime.UtcNow.AddSeconds(120);
  while(DateTime.UtcNow<end){
   Check();IntPtr game=UniqueWindow(GamePath),front=Native.GetForegroundWindow();
   if(game!=IntPtr.Zero&&SameProcess(front,game)){target=game;Bind();break;}
   if(front!=IntPtr.Zero&&!SameProcess(front,launcher)&&!SameProcess(front,game)){var title=new StringBuilder(512);Native.GetWindowText(front,title,512);throw new Exception("启动期间切换到了其他窗口，已停止："+title);}
   Delay(300);
  }
  if(target==IntPtr.Zero)throw new Exception("游戏启动超时，已停止。");
  WaitForGamePage();
 }
 void WaitForGamePage(){
  var end=DateTime.UtcNow.AddSeconds(120);
  while(DateTime.UtcNow<end){Native.Rect bounds;if(!Native.GetClientRect(target,out bounds)||bounds.R<1280||bounds.B<720||Math.Abs(bounds.R/(double)bounds.B-16.0/9)>0.03){Delay(700);continue;}string st=Observe();if(st=="rules"||st=="list"||st=="race"||st=="custom"||st=="blocked")return;Delay(700);}
  throw new Exception("未进入可识别的建角页面，已停止。请手动处理登录、排队或错误提示。");
 }
 void RestartAfterDelay(){
  Cooldown("游戏已退出");ShowLauncher();LaunchGame();
 }
 void Cooldown(string reason){Flow.Begin("wait",reason+"，等待 "+interval+" 秒后重试。");Interlocked.Exchange(ref nextTicks,DateTime.UtcNow.AddSeconds(interval).Ticks);target=IntPtr.Zero;log(reason+"，等待 "+interval+" 秒。等待期间可切换窗口，F9 或 Esc 可停止。");Delay((interval-5)*1000);log("5 秒后自动切回，开始下一轮。");Delay(5000);Interlocked.Exchange(ref nextTicks,0);}
 void ReselectAfterDelay(){Cooldown("本轮受阵营限制");target=UniqueWindow(GamePath);if(target==IntPtr.Zero)throw new Exception("等待期间游戏已关闭，已停止。");Native.FocusWindow(target);Thread.Sleep(500);Check();}
 Bitmap Capture(){
  Check();Native.Rect r;if(!Native.GetClientRect(target,out r))throw new Exception("无法读取游戏客户区尺寸。");config.ValidateDisplay(r.R,r.B);if(r.R!=observedWidth||r.B!=observedHeight){observedWidth=r.R;observedHeight=r.B;log("显示检测："+r.R+"×"+r.B+" 物理像素；标准 16:9；模式："+config.DisplayMode);}
  origin=new Point(0,0);Native.ClientToScreen(target,ref origin);
  scaleX=r.R/2560.0;scaleY=r.B/1440.0;
  using(var raw=new Bitmap(r.R,r.B,PixelFormat.Format32bppArgb)){
   using(var g=Graphics.FromImage(raw))g.CopyFromScreen(origin,Point.Empty,raw.Size);
   var b=new Bitmap(2560,1440,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(b)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.Bilinear;g.DrawImage(raw,new Rectangle(0,0,2560,1440));}Check();return b;
  }
 }
 static string PageName(string value){switch(value){case "rules":return "规则集选择";case "list":return "角色列表";case "race":return "种族与职业";case "custom":return "外观与姓名";case "loading":return "加载中";case "blocked":return "阵营限制";default:return "暂未识别（等待页面稳定）";}}
 string Observe(){using(var b=Capture()){latest=new Mask(b);state=v.State(latest);if(state!=observedState){observedState=state;string file=session+"-"+(++step).ToString("D3")+"-"+state+".png";b.Save(Path.Combine(AppPaths.Screenshots,file));log("页面检查："+PageName(state)+"；截图："+file);}return state;}}
 public void SaveDiagnostic(string name){try{using(var b=Capture())b.Save(Path.Combine(AppPaths.Screenshots,name+".png"));}catch{}}
 void Wait(string expected,int seconds=60){var end=DateTime.UtcNow.AddSeconds(seconds);int stable=0;while(DateTime.UtcNow<end){if(Observe()==expected){if(++stable>=3)return;}else stable=0;Delay(600);}throw new Exception("等待页面超时："+expected+"；已停止，请检查游戏。");}
 void Require(string expected){if(Observe()!=expected)throw new Exception("页面不符合预期，已停止（预期 "+expected+"）。");}
 void Has(string n){if(!v.Has(latest,n))throw new Exception("未核对到 "+n+"，已停止。");}
 bool Identity(string n){if(profile!=null)return profile.Has(latest,n);string key=n=="faction"?"horde":n=="race"?"tauren":n=="gender"?"male":n=="class"?"druid":n;return v.Has(latest,key);}
 void VerifyIdentity(string n){if(!Identity(n))throw new Exception("未核对到配置中的 "+n+"，已停止；请重新预览校准。");}
 void Pick(int x,int y,string label,bool learning){for(int i=0;i<2;i++){Wait("race");Click(x,y);Wait("race");if(learning||Identity(label))return;}throw new Exception("选择后未核对到配置的 "+label+"，已停止。");}
 void SelectOption(int x,int y,string label){for(int i=0;i<2;i++){Wait("race");Click(x,y);Wait("race");if(v.Has(latest,label))return;}throw new Exception("选择后未核对到 "+label+"，已停止。");}
 void Click(int x,int y){
  Check();Point p=new Point(origin.X+(int)(x*scaleX),origin.Y+(int)(y*scaleY));
  if(Native.GetAncestor(Native.WindowFromPoint(p),2)!=target)throw new Exception("点击位置被其他窗口遮挡，已停止。");
  Native.SetCursorPos(p.X,p.Y);Delay(100);Check();Native.Send(Native.M(2),Native.M(4));ParkMouse();Delay(600);Observe();
 }
 void ParkMouse(){Check();Native.SetCursorPos(origin.X+(int)(600*scaleX),origin.Y+(int)(1100*scaleY));}
 void Button(string name,string expected){var end=DateTime.UtcNow.AddSeconds(30);while(DateTime.UtcNow<end){Require(expected);var p=v.Candidates(name).FirstOrDefault(t=>t.Find(latest).HasValue);if(p!=null){var at=p.Find(latest).Value;Click(at.X+p.W/2,at.Y+p.H/2);return;}Delay(700);}throw new Exception("按钮被遮挡或未识别："+name);}
 void Fill(int x,string text){
  Require("custom");Click(x,65);Require("custom");Check();
  Native.Send(Native.K(17,0,0),Native.K(65,0,0),Native.K(65,0,2),Native.K(17,0,2));
  Delay(150);foreach(char ch in text){Check();Native.Send(Native.K(0,ch,4),Native.K(0,ch,6));Delay(80);}ParkMouse();Delay(350);Require("custom");
 }
 void BackToRules(){Flow.Begin("rules","关闭限制提示，返回并打开规则集。");
  string st=Observe();
  if(st=="blocked"){
   var p=v.P.Values.First(t=>t.Name.StartsWith("blocked")&&t.Find(latest).HasValue);var at=p.Find(latest).Value;
   Click(at.X+350,at.Y+104);Wait("custom");st="custom";
  }
  if(st=="custom"){Button("back","custom");Wait("race");st="race";}
  if(st=="race"){Button("back","race");Wait("list");st="list";}
  if(st=="list"){Button("ruleButton","list");Wait("rules");st="rules";}
  if(st!="rules")throw new Exception("无法识别起始页面。请停在规则集、角色创建页或阵营限制提示上。");
 }
 CalibrationBundle PrepareCharacter(bool learning){
  Flow.Begin("rules","选择并确认 "+config.RuleSet+" 规则集。");log("选择规则集："+config.RuleSet+"。");int ruleX=new[]{773,1110,1450,1788}[config.RuleIndex];
  Require("rules");Click(ruleX,610);Require("rules");if(!v.P["pvpSelected"].Find(latest,0.78,ruleX-1110).HasValue)throw new Exception("未核对到所选规则集的高亮边框，已停止。");Button("select","rules");
  // With no characters, this client opens race selection immediately.
  var end=DateTime.UtcNow.AddSeconds(30);
  while(DateTime.UtcNow<end){string st=Observe();if(st=="race")break;if(st=="list"){Button("create","list");break;}Delay(600);}
  Wait("race");Flow.Begin("role",config.Faction+" / "+config.Race+" / "+config.Gender+" / "+config.ClassName);log("设置角色："+config.Faction+" / "+config.Race+" / "+config.Gender+" / "+config.ClassName);
  var racePoint=config.RacePoint;Pick(racePoint.X,racePoint.Y,"race",learning);
  Pick(config.GenderX,55,"gender",learning);var classPoint=config.ClassPoint;Pick(classPoint.X,classPoint.Y,"class",learning);
  Require("race");if(!learning){VerifyIdentity("race");VerifyIdentity("class");VerifyIdentity("gender");VerifyIdentity("faction");}
  var bundle=new CalibrationBundle{Config=config.Copy(),RaceImage=Capture()};
  try{
   Flow.Begin("names","打开外观页，填写并核对两个名字。");Button("appearance","race");Wait("custom");log("填写姓名："+config.FirstName+" / "+config.LastName);Fill(1075,config.FirstName);Fill(1485,config.LastName);
   Require("custom");if(!learning){VerifyIdentity("first");VerifyIdentity("last");}bundle.NameImage=Capture();return bundle;
  }catch{bundle.Dispose();throw;}
 }
 AttemptRecord Create(int attempt){
  using(var bundle=PrepareCharacter(false)){}
  Flow.Begin("submit","核对完成按钮，留档并提交建号请求。");
  // Only the enabled red completion button is allowed.
  if(latest==null)throw new Exception("缺少页面。");
  using(var b=Capture()){Color c=b.GetPixel(2270,1370);if(c.R<100||c.R<c.G*1.8)throw new Exception("完成按钮未启用，已停止。");}
  AttemptRecord record;using(var b=Capture())record=ResultsStore.Add(config,attempt,"待确认","准备发送建号请求。若程序中断，请先核对游戏；这条记录不代表请求已送达或创建成功。",b);
  SaveDiagnostic("调试-提交前");log("角色与姓名核对通过，提交创建。");Button("finish","custom");return record;
 }
 public CalibrationBundle Calibrate(){Flow.Begin("prepare","检查配置并准备不提交预览。");CombinationRules.ValidateSave(config);CombinationRules.ValidateInstalledBuild(config);FocusInitial();log("预览校准：不会点击完成或提交建号。");Delay(3000);EnsureGameReady();BackToRules();return PrepareCharacter(true);}
 void FocusInitial(){var h=UniqueWindow(GamePath);if(h!=IntPtr.Zero){Native.FocusWindow(h);Thread.Sleep(500);target=h;Check();}else if(GameExists()){target=IntPtr.Zero;Check();}else ShowLauncher();}
 void Record(AttemptRecord record,string status,string detail){using(var b=Capture())ResultsStore.Update(record,status,detail,b);}
 public void Run(bool activate=false){
  Flow.Begin("prepare","校验角色配置、窗口和显示尺寸。");
  if(!restart)Flow.Skip("exit","本模式保留游戏窗口。");CombinationRules.ValidateExecution(config);ResultsStore.ValidateNameRetry(config);if(!CalibrationStore.Ready(config))throw new Exception("这组角色信息尚未校准，请先运行预览校准并确认。");
  if(config.Scheduled&&config.StartAt>DateTime.Now){Flow.Begin("wait","等待预约开始时间。");Interlocked.Exchange(ref nextTicks,config.StartAt.ToUniversalTime().Ticks);log("已预约于 "+config.StartAt.ToString("yyyy-MM-dd HH:mm:ss")+" 开始。F9 可取消。");while(DateTime.Now<config.StartAt)Delay((int)Math.Min(1000,(config.StartAt-DateTime.Now).TotalMilliseconds+1));Interlocked.Exchange(ref nextTicks,0);activate=true;}
  if(config.MaxMinutes>0)endAt=DateTime.UtcNow.AddMinutes(config.MaxMinutes);
  CombinationRules.ValidateInstalledBuild(config);if(activate)FocusInitial();
  log("5 秒后开始，请切回游戏或战网无限服页面。F9 或 Esc 停止。");Delay(5000);
  EnsureGameReady();if(restart&&Observe()=="blocked"){CloseBlockedGame();RestartAfterDelay();}
  for(int i=1;i<=max;i++){AttemptNumber=i;
   log("第 "+i+" 轮：返回并重新选择 "+config.RuleSet+"。");BackToRules();var record=Create(i);Flow.Begin("result","等待并识别服务器返回结果。");
   var end=DateTime.UtcNow.AddSeconds(35);bool blocked=false;
   while(DateTime.UtcNow<end){if(Observe()=="blocked"){blocked=true;break;}Delay(700);}
   if(!blocked){Flow.Finish("待确认","没有识别到已知阵营限制；请人工核对游戏结果。");
    Record(record,"待确认","提交后未识别到阵营限制，请核对结果截图与游戏角色列表；自动重试已停止。");
    using(var b=Capture())b.Save(Path.Combine(AppPaths.Screenshots,"最后结果.png"));
    System.Media.SystemSounds.Exclamation.Play();throw new Exception("提交后没有出现已知阵营限制，已停止并保存截图。可能已创建成功，也可能是其他提示，请查看游戏；不会继续点击。");
   }
   Flow.Finish("被拦截","服务器提示阵营人口不平衡。");BlockedCount++;Record(record,"被拦截","服务器提示阵营人口不平衡。");SaveDiagnostic("调试-阵营拦截");log("第 "+i+" 轮仍受阵营限制。"+(i<max?"将继续定时尝试。":"已达尝试上限。"));
   if(restart){CloseBlockedGame();if(i<max)RestartAfterDelay();}
   else if(i<max)ReselectAfterDelay();
  }
 }
}
static class Program {
 [STAThread] static int Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try{
   AppPaths.Initialize();if(args.Length==1&&args[0]=="--migrate-data")return 0;
   string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"templates");var v=new Vision(dir);
   if(args.Length==2&&args[0]=="--layout-test")return LayoutChecks.Run(v,args[1]);
   if(args.Length==2&&args[0]=="--task-ui-test"){using(var form=new ConfigurationForm(v,true)){form.Show();var check=form.CheckTaskCancellation(args[1]);while(!check.IsCompleted){Application.DoEvents();Thread.Sleep(20);}check.GetAwaiter().GetResult();form.Close();}return 0;}
   if(args.Length==2&&args[0]=="--preview"){
    Application.EnableVisualStyles();using(var form=new ConfigurationForm(v,true)){form.Show();Application.DoEvents();form.SavePreview(args[1]);form.Close();}return 0;
   }
   if(args.Length==2&&args[0]=="--config-test")return ConfigurationChecks.Run(args[1]);
   if(args.Length==2&&args[0]=="--startup-test")return GameStartupChecks.Run(args[1]);
   if(args.Length==2&&args[0]=="--startup-status"){File.WriteAllText(args[1],new Runner(v,t=>{},CancellationToken.None,new CreationConfig()).StartupStatus());return 0;}
   if(args.Length==2&&args[0]=="--launcher-test")return LauncherChecks.Run(args[1]);
   if(args.Length==2&&args[0]=="--launcher-snapshots")return LauncherChecks.Snapshots(args[1]);
   if(args.Length==2&&args[0]=="--prepare-preview"){
    CreationConfig c;using(var f=File.OpenRead(args[1]))c=(CreationConfig)new System.Xml.Serialization.XmlSerializer(typeof(CreationConfig)).Deserialize(f);
    bool owned;using(var mutex=new Mutex(true,"Local\\WowPvpRetry",out owned)){if(!owned)return 3;using(var cancel=new CancellationTokenSource(TimeSpan.FromMinutes(5))){
     Action<string> logger=t=>File.AppendAllText(Path.Combine(Path.GetDirectoryName(args[1]),"preview-test.txt"),DateTime.Now.ToString("HH:mm:ss")+" "+t+Environment.NewLine);
     var runner=new Runner(v,logger,cancel.Token,c);using(var b=Task.Run(()=>runner.Calibrate()).GetAwaiter().GetResult()){b.RaceImage.Save(Path.Combine(Path.GetDirectoryName(args[1]),"preview-race.png"));b.NameImage.Save(Path.Combine(Path.GetDirectoryName(args[1]),"preview-names.png"));logger("预览结束；未提交建号；未保存校准确认。");}return 0;
    }}
   }
   if(args.Length==1&&args[0]=="--capture-launcher"){Task.Run(()=>new Runner(v,t=>{},CancellationToken.None,120,1,true).SnapshotLauncher()).GetAwaiter().GetResult();return 0;}
   if(args.Length==1&&(args[0]=="--launcher-check"||args[0]=="--launch-only")){using(var cancel=new CancellationTokenSource(TimeSpan.FromMinutes(5))){var runner=new Runner(v,t=>File.AppendAllText(Path.Combine(AppPaths.Logs,"启动验证.txt"),DateTime.Now.ToString("s")+" "+t+Environment.NewLine),cancel.Token,120,1,true);Task.Run(()=>{if(args[0]=="--launch-only")runner.LaunchOnly();else runner.CheckLauncherOnly();}).GetAwaiter().GetResult();}return 0;}
   if(args.Length==1&&args[0]=="--launcher-select-test"){Task.Run(()=>new Runner(v,t=>{},CancellationToken.None,120,1,true).SelectInfiniteLauncher()).GetAwaiter().GetResult();return 0;}
   if(args.Length==1&&args[0]=="--capture-game"){Task.Run(()=>new Runner(v,t=>{},CancellationToken.None,120,1,true).SnapshotGame()).GetAwaiter().GetResult();return 0;}
   // Bounded live test uses the same runner as the GUI; no external UI driver.
   if(args.Length==2&&(args[0]=="--run"||args[0]=="--run-reselect")){
    int rounds=int.Parse(args[1]);if(rounds<1||rounds>2)throw new Exception("实测模式仅允许 1 或 2 轮。");
    bool owned;using(var mutex=new Mutex(true,"Local\\WowPvpRetry",out owned)){
     if(!owned)return 3;
     Action<string> logger=t=>File.AppendAllText(Path.Combine(AppPaths.Logs,"实测记录.txt"),DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+"  "+t+Environment.NewLine);
     using(var cancel=new CancellationTokenSource(TimeSpan.FromMinutes(12))){bool reboot=args[0]=="--run";var runner=new Runner(v,logger,cancel.Token,120,rounds,reboot);
      try{logger("开始实测，共 "+rounds+" 轮，模式："+(reboot?"退出重进":"返回规则集")+"。F9 或 Esc 可停止。");Task.Run(()=>runner.Run(true)).GetAwaiter().GetResult();logger("实测正常结束。");return 0;}
      catch(OperationCanceledException){logger("实测已取消。");return 4;}
      catch(Exception ex){runner.SaveDiagnostic("调试-异常页面");logger("实测停止："+ex.Message);return 5;}
     }
    }
   }
   // Offline verification never binds a window, captures the desktop, or sends input.
   if(args.Length==2&&args[0]=="--test"){
    var results=new List<string>();bool good=true;
    foreach(string f in Directory.GetFiles(args[1],"*.png"))using(var b=new Bitmap(f)){var m=new Mask(b);string st=v.State(m);results.Add(Path.GetFileName(f)+" = "+st+" : "+string.Join(",",v.P.Keys.Where(n=>v.Has(m,n))));}
    string[,] tests={{"blocked-new.png","blocked"},{"blocked.png","blocked"},{"rules.png","rules"},{"rules-restarted.png","rules"},{"selected-new.png","race"},{"race-base.png","race"},{"custom-empty.png","custom"},{"ready-clean.png","custom"},{"list.png","list"},{"list-native.png","list"},{"list-native-full.png","list"},{"race-native.png","race"},{"selected-native-full.png","race"},{"loading-native.png","loading"},{"loading-native-full.png","loading"},{"blocked-native.png","blocked"},{"blocked-native-full.png","blocked"}};
    for(int i=0;i<tests.GetLength(0);i++)using(var b=new Bitmap(Path.Combine(args[1],tests[i,0]))){if(v.State(new Mask(b))!=tests[i,1])good=false;}
    using(var b=new Bitmap(2560,1440)){if(v.State(new Mask(b))!="unknown")good=false;}
    using(var b=new Bitmap(Path.Combine(args[1],"race-base.png"))){var m=new Mask(b);if(v.Has(m,"tauren")||v.Has(m,"druid"))good=false;}
    using(var b=new Bitmap(Path.Combine(args[1],"custom-empty.png"))){var m=new Mask(b);if(v.Has(m,"first")||v.Has(m,"last"))good=false;}
    using(var b=new Bitmap(Path.Combine(args[1],"rules-restarted.png"))){if(v.Has(new Mask(b),"pvpSelected"))good=false;}
    using(var b=new Bitmap(Path.Combine(args[1],"race-native.png"))){var m=new Mask(b);if(v.Has(m,"tauren")||v.Has(m,"druid")||v.Has(m,"male")||v.Has(m,"horde"))good=false;}
    string[,] checks={{"ready-clean.png","first,last,finish,back"},{"ready-final-clean.png","first,last,finish,back"},{"blocked-final.png","blockedOld,first,last"},{"selected-new.png","tauren,druid,horde,male,back,appearance"},{"selected.png","tauren,druid,horde,male,back,appearance"},{"rules.png","rules,pvp,pvpSelected,select"}};
    for(int i=0;i<checks.GetLength(0);i++)using(var b=new Bitmap(Path.Combine(args[1],checks[i,0]))){var m=new Mask(b);foreach(string n in checks[i,1].Split(','))if(!v.Has(m,n)){good=false;results.Add("Missing "+n+" in "+checks[i,0]);}}
    results.Add(good?"PASS":"FAIL");File.WriteAllLines(Path.Combine(args[1],"verification.txt"),results);return good?0:1;
   }
   bool created;using(var mutex=new Mutex(true,"Local\\WowPvpRetry",out created)){if(!created)return 1;Application.EnableVisualStyles();Application.Run(new ConfigurationForm(v));}return 0;
  }catch(Exception ex){if(args.Length>0){File.WriteAllText(Path.Combine(AppPaths.Logs,"test-error.txt"),ex.ToString());return 2;}MessageBox.Show(ex.Message,"重试工具");return 1;}
 }
}
