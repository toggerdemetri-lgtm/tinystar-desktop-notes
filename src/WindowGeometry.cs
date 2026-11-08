using System;
namespace DesktopNotes {
    [Flags] public enum ResizeEdges { Left=1,Top=2,Right=4,Bottom=8 }
    public struct WindowBounds {
        public double X,Y,Width,Height;
        public WindowBounds(double x,double y,double width,double height) {X=x;Y=y;Width=width;Height=height;}
    }
    public static class WindowGeometry {
        public static WindowBounds SnapToScreen(WindowBounds window,WindowBounds physicalArea,double scale,double threshold) {
            if(Double.IsNaN(scale) || Double.IsInfinity(scale) || scale<=0)throw new ArgumentOutOfRangeException("scale");
            return Snap(window,new WindowBounds(physicalArea.X/scale,physicalArea.Y/scale,physicalArea.Width/scale,physicalArea.Height/scale),threshold);
        }
        public static double SmoothPosition(double current,double target,ref double velocity,double smoothTime,double dt) {
            double omega=2/Math.Max(.001,smoothTime),change=current-target,decay=Math.Exp(-omega*dt),temp=(velocity+omega*change)*dt;
            double next=target+(change+temp)*decay;velocity=(velocity-omega*temp)*decay;
            if((target-current)*(target-next)<0) {velocity=0;return target;}return next;
        }
        public static WindowBounds Snap(WindowBounds window,WindowBounds area,double threshold) {
            double left=Math.Abs(window.X-area.X),right=Math.Abs(window.X+window.Width-area.X-area.Width);
            double top=Math.Abs(window.Y-area.Y),bottom=Math.Abs(window.Y+window.Height-area.Y-area.Height);
            if(left<=threshold && left<=right)window.X=area.X;else if(right<=threshold)window.X=area.X+area.Width-window.Width;
            if(top<=threshold && top<=bottom)window.Y=area.Y;else if(bottom<=threshold)window.Y=area.Y+area.Height-window.Height;
            return window;
        }
        public static WindowBounds Resize(WindowBounds start,double dx,double dy,ResizeEdges edges,double minWidth,double minHeight) {
            double width=start.Width,height=start.Height,x=start.X,y=start.Y;
            if((edges&ResizeEdges.Left)!=0) {width=Math.Max(minWidth,start.Width-dx);x=start.X+start.Width-width;}
            else if((edges&ResizeEdges.Right)!=0)width=Math.Max(minWidth,start.Width+dx);
            if((edges&ResizeEdges.Top)!=0) {height=Math.Max(minHeight,start.Height-dy);y=start.Y+start.Height-height;}
            else if((edges&ResizeEdges.Bottom)!=0)height=Math.Max(minHeight,start.Height+dy);
            return new WindowBounds(x,y,width,height);
        }
    }
    public sealed class NoteDragSession {
        readonly string id;readonly double x,y;bool ended;
        public bool Active {get;private set;}
        public NoteDragSession(string id,double x,double y) {this.id=id;this.x=x;this.y=y;}
        public bool Move(double currentX,double currentY,double threshold) {if(ended)return false;if(Math.Abs(currentX-x)>=threshold || Math.Abs(currentY-y)>=threshold)Active=true;return Active;}
        public string Drop(bool insideTrash) {if(ended)return null;ended=true;return Active && insideTrash ? id:null;}
        public void Cancel() {ended=true;Active=false;}
    }
}
