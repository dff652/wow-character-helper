using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

sealed class FlowStep {
 public string Key,Title,Action="等待执行",Status="未开始";public double Seconds;public int Visits;
 public FlowStep Copy(){return (FlowStep)MemberwiseClone();}
}
sealed class FlowTrace {
 readonly object gate=new object();readonly Func<double> now;readonly List<FlowStep> steps=new List<FlowStep>();FlowStep current;double entered;
 public FlowTrace(Func<double> time=null){var clock=Stopwatch.StartNew();now=time??(()=>clock.Elapsed.TotalSeconds);string[] keys={"prepare","launch","rules","role","names","submit","result","exit","wait"};string[] names={"准备检查","进入游戏","选择规则集","设置角色","填写姓名","提交建号","核对结果","退出游戏","等待重试"};for(int i=0;i<keys.Length;i++)steps.Add(new FlowStep{Key=keys[i],Title=names[i]});}
 void Close(){if(current==null)return;current.Seconds+=now()-entered;if(current.Status=="进行中")current.Status="已完成";current=null;}
 public void Begin(string key,string action){lock(gate){Close();current=steps.Single(s=>s.Key==key);current.Action=action;current.Status="进行中";current.Visits++;entered=now();}}
 public void Skip(string key,string action){lock(gate){var step=steps.Single(s=>s.Key==key);step.Status="无需执行";step.Action=action;}}
 public void Finish(string status,string action){lock(gate){if(current==null)return;current.Status=status;current.Action=action;Close();}}
 public FlowStep[] Snapshot(){lock(gate){return steps.Select(s=>{var copy=s.Copy();if(s==current)copy.Seconds+=now()-entered;return copy;}).ToArray();}}
}

sealed class FlowProgressView:Control {
 FlowStep[] steps=new FlowTrace().Snapshot();int selected=-1;readonly ToolTip tips=new ToolTip();
 public FlowProgressView(){DoubleBuffered=true;Dock=DockStyle.Top;BackColor=Color.White;Margin=new Padding(16,10,16,12);Font=new Font("Microsoft YaHei UI",11F);}
 public void UpdateSteps(FlowStep[] value){steps=value;Invalidate();}
 float ScaleFactor{get{return Font.SizeInPoints/11f*DeviceDpi/96f;}}
 int Columns{get{return Math.Max(1,Math.Min(5,(int)(Width/(170*ScaleFactor))));}}
 protected override void OnResize(EventArgs e){base.OnResize(e);FitHeight();}
 protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);FitHeight();}
 void FitHeight(){if(steps==null||Width<=0)return;int h=(int)((36+Math.Ceiling(steps.Length/(double)Columns)*96)*ScaleFactor);if(Height!=h)Height=h;Invalidate();}
 Rectangle Card(int i){float scale=ScaleFactor;int columns=Columns,w=Width/columns;return new Rectangle(i%columns*w+5,(int)(34*scale)+i/columns*(int)(96*scale),w-10,(int)(88*scale));}
 static Color Accent(string status){return status=="进行中"?Color.FromArgb(28,105,196):status=="已完成"?Color.FromArgb(36,125,83):status=="被拦截"||status=="待确认"?Color.FromArgb(177,113,17):status=="已停止"||status=="异常"?Color.FromArgb(185,60,58):Color.FromArgb(125,135,148);}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);float s=ScaleFactor;TextRenderer.DrawText(e.Graphics,"运行进度",Font,new Rectangle(8,3,Width-16,(int)(30*s)),Color.FromArgb(35,51,71),TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
  for(int i=0;i<steps.Length;i++){var step=steps[i];var rect=Card(i);Color color=Accent(step.Status);using(var brush=new SolidBrush(step.Status=="进行中"?Color.FromArgb(234,243,255):Color.FromArgb(248,250,252)))e.Graphics.FillRectangle(brush,rect);using(var pen=new Pen(i==selected?color:Color.FromArgb(219,225,233),i==selected?2:1))e.Graphics.DrawRectangle(pen,rect);using(var brush=new SolidBrush(color))e.Graphics.FillRectangle(brush,rect.X,rect.Y,4,rect.Height);
   var text=new Rectangle(rect.X+10,rect.Y+6,rect.Width-20,(int)(25*s));using(var bold=new Font(Font,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,(i+1)+"  "+step.Title,bold,text,color,TextFormatFlags.EndEllipsis);
   text.Y+=(int)(25*s);text.Height=(int)(25*s);TextRenderer.DrawText(e.Graphics,step.Status+" · "+step.Seconds.ToString("0.0")+" 秒",Font,text,color,TextFormatFlags.EndEllipsis);
   text.Y+=(int)(25*s);TextRenderer.DrawText(e.Graphics,step.Action,Font,text,Color.FromArgb(64,73,84),TextFormatFlags.EndEllipsis);
  }
 }
 protected override void OnMouseClick(MouseEventArgs e){base.OnMouseClick(e);for(int i=0;i<steps.Length;i++)if(Card(i).Contains(e.Location)){selected=i;tips.SetToolTip(this,steps[i].Title+"："+steps[i].Action+"\n状态："+steps[i].Status+"；累计耗时："+steps[i].Seconds.ToString("0.0")+" 秒；执行次数："+steps[i].Visits);Invalidate();break;}}
 protected override void Dispose(bool disposing){if(disposing)tips.Dispose();base.Dispose(disposing);}
}
