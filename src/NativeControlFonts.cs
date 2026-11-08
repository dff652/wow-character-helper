using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed partial class ConfigurationForm {
 readonly List<IntPtr> nativeFonts=new List<IntPtr>();
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
 void SetNativeFonts(){
  var old=nativeFonts.ToArray();nativeFonts.Clear();ApplyNativeFont(this);foreach(var handle in old)DeleteObject(handle);PerformLayout();
 }
 void ApplyNativeFont(Control parent){foreach(Control child in parent.Controls){
  if(child is TextBoxBase||child is ComboBox||child is DateTimePicker||child is NumericUpDown||child is TabControl){
   child.Font=new Font(Font.FontFamily,11f*(previewScale??(DeviceDpi/96f)));
   using(var font=new Font(child.Font.FontFamily,child.Font.SizeInPoints,child.Font.Style)){var handle=font.ToHfont();nativeFonts.Add(handle);SendMessage(child.Handle,0x30,handle,new IntPtr(1));}
   if(child is ComboBox||child is DateTimePicker||child is NumericUpDown||(child is TextBox&&!((TextBox)child).Multiline)){child.MinimumSize=new Size(0,(int)Math.Ceiling(30*(previewScale??(DeviceDpi/96f))));}
   var tab=child as TabControl;if(tab!=null)tab.ItemSize=new Size(0,(int)Math.Ceiling(28*(previewScale??(DeviceDpi/96f))));
  }
  ApplyNativeFont(child);
 }}
 void DisposeNativeFonts(){foreach(var handle in nativeFonts)DeleteObject(handle);nativeFonts.Clear();}
}
