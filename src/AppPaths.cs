using System;
using System.IO;
using System.Collections.Generic;

static class AppPaths {
 internal static string TestRoot;
 public static string Root {get{return Path.GetFullPath(TestRoot??AppDomain.CurrentDomain.BaseDirectory);}}
 public static string Data {get{return Path.Combine(Root,"data");}}
 public static string Config {get{return Path.Combine(Data,"config");}}
 public static string Logs {get{return Path.Combine(Data,"logs");}}
 public static string Results {get{return Path.Combine(Data,"results");}}
 public static string Screenshots {get{return Path.Combine(Data,"screenshots");}}
 public static string Profiles {get{return Path.Combine(Data,"profiles");}}
 public static string RunLog {get{return Path.Combine(Logs,DateTime.Now.ToString("yyyy-MM-dd")+".log");}}
 public static void Ensure(){foreach(var dir in new[]{Config,Logs,Results,Screenshots,Profiles})Directory.CreateDirectory(dir);}
 public static void Initialize(){Ensure();MigrateLegacy();}
 // Only known legacy files/directories inside this application are relocated.
 // Conflicts are retained under data/legacy; a destination is never overwritten.
 static void MoveFile(string source,string destination,List<string> moved){
  if(!File.Exists(source))return;
  if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)throw new IOException("迁移时遇到链接文件，请手动检查："+source);
  if(File.Exists(destination)){
   string relative=source.Substring(Root.TrimEnd(Path.DirectorySeparatorChar).Length+1);
   destination=Path.Combine(Data,"legacy",relative);
   if(File.Exists(destination))destination+="."+Guid.NewGuid().ToString("N")+".bak";
  }
  Directory.CreateDirectory(Path.GetDirectoryName(destination));File.Move(source,destination);
  moved.Add(Path.GetFileName(source)+" → "+destination.Substring(Root.TrimEnd(Path.DirectorySeparatorChar).Length+1));
 }
 static void MoveFolder(string source,string destination,List<string> moved){
  if(!Directory.Exists(source))return;
  if((File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)throw new IOException("迁移时遇到链接文件夹，请手动检查："+source);
  foreach(var file in Directory.GetFiles(source))MoveFile(file,Path.Combine(destination,Path.GetFileName(file)),moved);
  foreach(var child in Directory.GetDirectories(source))MoveFolder(child,Path.Combine(destination,Path.GetFileName(child)),moved);
  if(Directory.GetFileSystemEntries(source).Length==0)Directory.Delete(source);
 }
 public static void MigrateLegacy(){
  var moved=new List<string>();
  MoveFile(Path.Combine(Root,"建号配置.xml"),Path.Combine(Config,"建号配置.xml"),moved);
  foreach(var name in new[]{"运行记录.txt","实测记录.txt","test-error.txt","调试-尺寸.txt"})MoveFile(Path.Combine(Root,name),Path.Combine(Logs,name),moved);
  foreach(var file in Directory.GetFiles(Root,"调试-*.png"))MoveFile(file,Path.Combine(Screenshots,Path.GetFileName(file)),moved);
  MoveFile(Path.Combine(Root,"最后结果.png"),Path.Combine(Screenshots,"最后结果.png"),moved);
  MoveFolder(Path.Combine(Root,"records"),Results,moved);MoveFolder(Path.Combine(Root,"profiles"),Profiles,moved);
  if(moved.Count>0)File.AppendAllLines(Path.Combine(Logs,"迁移记录.log"),moved);
 }
}
