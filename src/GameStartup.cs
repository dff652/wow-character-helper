using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

sealed class GameStartupSnapshot {
 public IntPtr Window;
 public int[] ProcessIds=new int[0];
 public Dictionary<int,long> StartedUtcTicks=new Dictionary<int,long>();
 public bool HasVisibleWindows;
 public bool OldEnough;
 public string WindowSummary="无可见窗口";
 public string Describe(){return "游戏进程 PID："+(ProcessIds.Length==0?"无":string.Join(", ",ProcessIds.Select(x=>x.ToString())))+"；窗口："+WindowSummary;}
}

static class GameStartup {
 // A process without a usable window may be starting or shutting down. Never bind it or launch a duplicate.
 public static bool SameProcesses(GameStartupSnapshot a,GameStartupSnapshot b){
  return a.ProcessIds.Length==b.ProcessIds.Length&&a.ProcessIds.All(id=>b.ProcessIds.Contains(id)&&a.StartedUtcTicks.ContainsKey(id)&&b.StartedUtcTicks.ContainsKey(id)&&a.StartedUtcTicks[id]==b.StartedUtcTicks[id]);
 }
 public static IntPtr Resolve(Func<GameStartupSnapshot> inspect,Action<int> wait,Action<string> log,int timeoutMs=30000,Action<GameStartupSnapshot> recover=null){
  int waited=0;bool announced=false,eligible=true;GameStartupSnapshot initial=null;
  while(true){
   var state=inspect();
   if(state.Window!=IntPtr.Zero){log("检测到游戏主窗口。"+state.Describe());return state.Window;}
   if(state.ProcessIds.Length==0){log("游戏未运行，将从战网启动。");return IntPtr.Zero;}
   if(initial==null)initial=state;
   eligible=eligible&&!state.HasVisibleWindows&&SameProcesses(initial,state);
   if(!announced){log("检测到游戏进程，但主窗口尚未就绪，等待最多 "+timeoutMs/1000+" 秒。"+state.Describe());announced=true;}
   if(waited>=timeoutMs){
    if(recover!=null&&eligible&&state.OldEnough){
     log("持续无窗口，正在核对并清理无限版残留进程。"+state.Describe());recover(state);
     var after=inspect();
     if(after.ProcessIds.Length!=0)throw new InvalidOperationException("残留清理后仍有游戏进程，停止启动。"+after.Describe());
     log("残留进程已退出，将从战网重新启动。");return IntPtr.Zero;
    }
    throw new InvalidOperationException("检测到 Wow.exe 后台进程，但游戏主窗口在等待后仍未出现。"+state.Describe()+"。进程过新、身份变化或存在其他窗口，未自动清理；请检查游戏状态后重试。");
   }
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
   var stale=new GameStartupSnapshot{ProcessIds=new[]{123},StartedUtcTicks=new Dictionary<int,long>{{123,1}},OldEnough=true};
   bool cleaned=false;waits=0;
   check(GameStartup.Resolve(()=>cleaned?absent:stale,wait,s=>{},600,s=>cleaned=true)==IntPtr.Zero&&cleaned&&waits==2,"stable old windowless process is recovered once after grace period");
   Action<GameStartupSnapshot,string> denied=(snapshot,name)=>{
    bool called=false,stopped=false;try{GameStartup.Resolve(()=>snapshot,wait,s=>{},600,s=>called=true);}catch(InvalidOperationException){stopped=true;}check(stopped&&!called,name);
   };
   denied(new GameStartupSnapshot{ProcessIds=new[]{123},StartedUtcTicks=new Dictionary<int,long>{{123,1}}},"new process is never terminated");
   denied(new GameStartupSnapshot{ProcessIds=new[]{123},StartedUtcTicks=new Dictionary<int,long>{{123,1}},OldEnough=true,HasVisibleWindows=true},"other visible game window prevents recovery");
   waits=0;bool changedStopped=false,changedKilled=false;
   var replaced=new GameStartupSnapshot{ProcessIds=new[]{123},StartedUtcTicks=new Dictionary<int,long>{{123,2}},OldEnough=true};
   try{GameStartup.Resolve(()=>waits==0?stale:replaced,wait,s=>{},600,s=>changedKilled=true);}catch(InvalidOperationException){changedStopped=true;}
   check(changedStopped&&!changedKilled,"reused PID or changed process identity prevents recovery");
   bool failedStopped=false;int attempts=0;
   try{GameStartup.Resolve(()=>stale,wait,s=>{},600,s=>attempts++);}catch(InvalidOperationException){failedStopped=true;}
   check(failedStopped&&attempts==1,"failed cleanup stops without launching or repeated termination");
   bool cleanupCancelled=false;try{GameStartup.Resolve(()=>stale,wait,s=>{},600,s=>{throw new OperationCanceledException();});}catch(OperationCanceledException){cleanupCancelled=true;}
   check(cleanupCancelled,"cancellation during recovery prevents launch");
   lines.Add("PASS");File.WriteAllLines(Path.Combine(root,"startup-verification.txt"),lines);return 0;
  }catch(Exception ex){lines.Add(ex.ToString());File.WriteAllLines(Path.Combine(root,"startup-verification.txt"),lines);return 1;}
 }
}
