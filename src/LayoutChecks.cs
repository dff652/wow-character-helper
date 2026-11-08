using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;

static class LayoutChecks {
 public static int Run(Vision vision,string root){
  Directory.CreateDirectory(root);var log=new List<string>();
  try{
   int[,] cases={{1280,720,100},{1920,1080,100},{1920,1080,125},{2560,1440,150},{3840,2160,200},{1366,768,150}};
   for(int i=0;i<cases.GetLength(0);i++)using(var form=new ConfigurationForm(vision,true)){
    form.Show();Application.DoEvents();float scale=cases[i,2]/100f;form.SetPreviewScale(scale);form.MinimumSize=Size.Empty;
    form.ClientSize=new Size(Math.Min((int)(1000*scale),cases[i,0]-60),Math.Min((int)(760*scale),cases[i,1]-100));Application.DoEvents();form.CheckLayout();form.CheckFooterRecovery();
    string key=cases[i,0]+"x"+cases[i,1]+"-"+cases[i,2];form.SavePreview(Path.Combine(root,key+".png"));log.Add("PASS: simulated layout "+key);
    if(i==1){form.SelectTab(2);form.SavePreview(Path.Combine(root,"results.png"));form.SelectTab(4);form.SavePreview(Path.Combine(root,"about.png"));form.SelectTab(5);form.SavePreview(Path.Combine(root,"tasks.png"));}
    form.Close();
   }
   log.Add("PASS (simulated control scaling; not physical monitor or game verification)");File.WriteAllLines(Path.Combine(root,"layout-verification.txt"),log);return 0;
  }catch(Exception ex){log.Add(ex.ToString());File.WriteAllLines(Path.Combine(root,"layout-verification.txt"),log);return 1;}
 }
}
