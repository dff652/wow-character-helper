using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace CharacterVision {
 static class GoldText {
  // A text-colour preprocessing pass, not a template for a particular sentence.
  public static Rectangle Region(int width,int height){return new Rectangle((int)(width*.3),(int)(height*.4),(int)(width*.4),(int)(height*.2));}
  public static Bitmap Extract(Bitmap input,Rectangle area){
   var result=new Bitmap(area.Width,area.Height,PixelFormat.Format32bppArgb);
   using(var g=Graphics.FromImage(result))g.DrawImage(input,new Rectangle(Point.Empty,result.Size),area,GraphicsUnit.Pixel);
   var data=result.LockBits(new Rectangle(Point.Empty,result.Size),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
   try{var pixels=new byte[data.Stride*data.Height];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
    for(int y=0;y<data.Height;y++)for(int x=0;x<data.Width;x++){int i=y*data.Stride+x*4;bool gold=pixels[i+2]>175&&pixels[i+1]>155&&pixels[i]<110&&pixels[i+1]>pixels[i]*1.4;byte value=(byte)(gold?0:255);pixels[i]=pixels[i+1]=pixels[i+2]=value;pixels[i+3]=255;}
    Marshal.Copy(pixels,0,data.Scan0,pixels.Length);
   }finally{result.UnlockBits(data);}return result;
  }
 }
}
