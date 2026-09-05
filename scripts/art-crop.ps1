# Shared deterministic sprite extraction. Connectivity removes neighbouring-cell fragments;
# source bounds remain explicit so a wrong grid cannot silently become a tiny valid icon.
Add-Type -AssemblyName System.Drawing
if (-not ('ArtCrop' -as [type])) {
Add-Type -ReferencedAssemblies 'System.Drawing.Common','System.Drawing.Primitives','System.Runtime','System.Collections' -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
public static class ArtCrop {
    public static string Validate(Bitmap image, int columns, int rows, int count, bool gutters, bool unique) {
        if(columns<=0||rows<=0||image.Width%columns!=0||image.Height%rows!=0||count>columns*rows)return "invalid grid";
        int w=image.Width/columns,h=image.Height/rows;
        var hashes=new HashSet<ulong>();
        for(int i=0;i<columns*rows;i++) {
            int left=i%columns*w,top=i/columns*h,visible=0; ulong hash=14695981039346656037UL;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){
                Color c=image.GetPixel(left+x,top+y);if(c.A>8)visible++;
                hash=unchecked((hash^(uint)(c.A>8?c.ToArgb():0))*1099511628211UL);
                if(gutters&&c.A>8&&(x==0||y==0||x==w-1||y==h-1))return "cell "+i+" touches its crop boundary";
            }
            if(i<count&&visible<10)return "cell "+i+" is empty or incomplete";
            if(i>=count&&visible>0)return "unused cell "+i+" contains pixels";
            if(i<count&&unique&&!hashes.Add(hash))return "cell "+i+" duplicates another identity";
        }
        return "";
    }
    public static Bitmap Extract(Bitmap image, Rectangle cell) {
        bool[] visited = new bool[cell.Width * cell.Height];
        int largest=0; Rectangle best=Rectangle.Empty;
        var queue=new Queue<int>();
        var pixels=new List<int>(); var bestPixels=new List<int>();
        for(int y=0;y<cell.Height;y++) for(int x=0;x<cell.Width;x++) {
            int id=y*cell.Width+x;
            if(visited[id] || image.GetPixel(cell.X+x,cell.Y+y).A<=8) continue;
            visited[id]=true;queue.Enqueue(id); pixels.Clear();
            int count=0,left=x,right=x,top=y,bottom=y;
            while(queue.Count>0) {
                int p=queue.Dequeue(),px=p%cell.Width,py=p/cell.Width;count++;pixels.Add(p);
                left=Math.Min(left,px);right=Math.Max(right,px);top=Math.Min(top,py);bottom=Math.Max(bottom,py);
                for(int dy=-1;dy<=1;dy++) for(int dx=-1;dx<=1;dx++) {
                    int nx=px+dx,ny=py+dy;
                    if(nx<0||ny<0||nx>=cell.Width||ny>=cell.Height)continue;
                    int ni=ny*cell.Width+nx;
                    if(visited[ni]||image.GetPixel(cell.X+nx,cell.Y+ny).A<=8)continue;
                    visited[ni]=true;queue.Enqueue(ni);
                }
            }
            if(count>largest){largest=count;best=new Rectangle(cell.X+left,cell.Y+top,right-left+1,bottom-top+1);bestPixels=new List<int>(pixels);}
        }
        if(largest<16) throw new InvalidOperationException("Missing sprite in source rectangle "+cell);
        // The generated masters are not perfectly aligned to an equal grid. The cell selects
        // the subject; flood the whole master to recover portions extending over that cell.
        int seed=bestPixels[0],sx=cell.X+seed%cell.Width,sy=cell.Y+seed/cell.Width;
        var whole=new HashSet<int>();queue.Clear();queue.Enqueue(sy*image.Width+sx);whole.Add(sy*image.Width+sx);
        int fullLeft=sx,fullRight=sx,fullTop=sy,fullBottom=sy;
        while(queue.Count>0){int p=queue.Dequeue(),x=p%image.Width,y=p/image.Width;
            fullLeft=Math.Min(fullLeft,x);fullRight=Math.Max(fullRight,x);fullTop=Math.Min(fullTop,y);fullBottom=Math.Max(fullBottom,y);
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){
                int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=image.Width||ny>=image.Height)continue;
                int ni=ny*image.Width+nx;if(whole.Contains(ni)||image.GetPixel(nx,ny).A<=8)continue;
                whole.Add(ni);queue.Enqueue(ni);
            }
        }
        best=new Rectangle(fullLeft,fullTop,fullRight-fullLeft+1,fullBottom-fullTop+1);
        var result=new Bitmap(best.Width,best.Height);
        foreach(int p in whole){int x=p%image.Width,y=p/image.Width;result.SetPixel(x-best.X,y-best.Y,image.GetPixel(x,y));}
        return result;
    }
}
'@
}
