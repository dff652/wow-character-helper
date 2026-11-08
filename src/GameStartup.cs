using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

sealed class GameStartupSnapshot {
 public IntPtr Window;
 public int[] ProcessIds=new int[0];
 public string WindowSummary="无可见窗口";
 public string Describe(){return "游戏进程 PID："+(ProcessIds.Length==0?"无":string.Join(", ",ProcessIds.Select(x=>x.ToString())))+"；窗口："+WindowSummary;}
}

static class GameStartup {
 // A process without a usable window may be starting or shutting down. Never bind it or launch a duplicate.
 public static IntPtr Resolve(Func<GameStartupSnapshot> inspect,Action<int> wait,Action<string> log,int timeoutMs=30000){
  int waited=0;bool announced=false;
  while(true){
   var state=inspect();
   if(state.Window!=IntPtr.Zero){log("检测到游戏主窗口。"+state.Describe());return state.Window;}
   if(state.ProcessIds.Length==0){log("游戏未运行，将从战网启动。");return IntPtr.Zero;}
   if(!announced){log("检测到游戏进程，但主窗口尚未就绪，等待最多 "+timeoutMs/1000+" 秒。"+state.Describe());announced=true;}
   if(waited>=timeoutMs)throw new InvalidOperationException("检测到 Wow.exe 后台进程，但游戏主窗口在等待后仍未出现。"+state.Describe()+"。可能正在退出或已残留；请在任务管理器核对并结束对应的无限版进程后重试。战网窗口已经打开也不能消除此状态；工具未重复启动或强制结束游戏。");
   int delay=Math.Min(300,timeoutMs-waited);wait(delay);waited+=delay;
  }
 }
}

static class GameStartupChecks {
 public static int Run(string root){
  Directory.CreateDirectory(root);var lines=new List<string>();
  Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);lines.Add("PASS: "+name);};
  var absent=new GameStartupSnapshot();var pending=new GameStartupSnapshot{ProcessIds=new[]{123}};
  var ready=new GameStartupSnapshot{ProcessIds=new[]{123},Window=new IntPtr(456)};
  try{
   int waits=0;Action<int> wait=ms=>waits++;
   check(GameStartup.Resolve(()=>absent,wait,s=>{})==IntPtr.Zero&&waits==0,"no process selects launcher immediately");
   check(GameStartup.Resolve(()=>ready,wait,s=>{})==ready.Window&&waits==0,"existing window is reused immediately");
   check(GameStartup.Resolve(()=>waits<2?pending:ready,wait,s=>{})==ready.Window&&waits==2,"starting process waits for its window");
   waits=0;check(GameStartup.Resolve(()=>waits<2?pending:absent,wait,s=>{})==IntPtr.Zero&&waits==2,"exiting process must disappear before launcher is selected");
   waits=0;bool timeout=false;try{GameStartup.Resolve(()=>pending,wait,s=>{},600);}catch(InvalidOperationException ex){timeout=ex.Message.Contains("123")&&ex.Message.Contains("后台进程");}check(timeout&&waits==2,"windowless process times out with PID, without choosing launch or bind");
   bool cancelled=false;try{GameStartup.Resolve(()=>pending,ms=>{throw new OperationCanceledException();},s=>{});}catch(OperationCanceledException){cancelled=true;}check(cancelled,"cancellation stops the startup wait");
   bool ambiguous=false;try{GameStartup.Resolve(()=>{throw new InvalidOperationException("multiple windows");},wait,s=>{});}catch(InvalidOperationException){ambiguous=true;}check(ambiguous,"ambiguous target is not bypassed");
   lines.Add("PASS");File.WriteAllLines(Path.Combine(root,"startup-verification.txt"),lines);return 0;
  }catch(Exception ex){lines.Add(ex.ToString());File.WriteAllLines(Path.Combine(root,"startup-verification.txt"),lines);return 1;}
 }
}
