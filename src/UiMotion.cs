using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace DesktopNotes {
    public static class UiMotion {
        public static bool Enabled=true;
        public static void Appear(FrameworkElement element) {
            if(!Enabled)return;var translate=new TranslateTransform(0,9);element.RenderTransform=translate;
            element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(180)) {FillBehavior=FillBehavior.Stop});
            translate.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(9,0,TimeSpan.FromMilliseconds(220)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
        }
        public static BitmapSource Snapshot(FrameworkElement element) {
            return Snapshot(element,1);
        }
        public static BitmapSource Snapshot(FrameworkElement element,double scale) {
            int width=(int)Math.Ceiling(Math.Min(960,Math.Max(1,(element.ActualWidth>0 ? element.ActualWidth:element.Width)*scale)));
            int height=(int)Math.Ceiling(Math.Min(720,Math.Max(1,(element.ActualHeight>0 ? element.ActualHeight:element.Height)*scale)));
            var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) {
                var brush=new VisualBrush(element) {Stretch=Stretch.Fill};
                dc.DrawRectangle(brush,null,new Rect(0,0,width,height));
            }
            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
        }
        public static void Shatter(FrameworkElement element,Canvas canvas,Point origin) {
            Shatter(element,canvas,origin,false);
        }
        public static void ShatterStep(FrameworkElement element,Canvas canvas,Point origin) {
            Shatter(element,canvas,origin,true);
        }
        public static void ShatterStep(FrameworkElement element,Canvas canvas,Point origin,double scale) {Shatter(element,canvas,origin,true,scale);}
        static void Shatter(FrameworkElement element,Canvas canvas,Point origin,bool compact,double scale=1) {
            if(!Enabled)return;
            BitmapSource image;try {image=Snapshot(element,scale);}catch {return;}
            double width=image.PixelWidth,height=image.PixelHeight;int columns=compact ? Math.Max(3,Math.Min(8,(int)Math.Ceiling(width/28))):6,rows=compact ? Math.Max(1,Math.Min(4,(int)Math.Ceiling(height/16))):4;
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)for(int half=0;half<2;half++) {
                double w=width/columns,h=height/rows;
                var brush=new ImageBrush(image) {ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(x*w,y*h,w,h),Stretch=Stretch.Fill};
                var piece=new Polygon {Points=half==0 ? new PointCollection {new Point(0,0),new Point(w,0),new Point(0,h)}:new PointCollection {new Point(w,0),new Point(w,h),new Point(0,h)},Fill=brush,Width=w,Height=h,IsHitTestVisible=false,RenderTransformOrigin=new Point(.5,.5)};
                Canvas.SetLeft(piece,origin.X+x*w);Canvas.SetTop(piece,origin.Y+y*h);canvas.Children.Add(piece);
                int seed=x*13+y*7+half*3;var rotate=new RotateTransform();var shift=new TranslateTransform();var group=new TransformGroup();group.Children.Add(rotate);group.Children.Add(shift);piece.RenderTransform=group;
                var duration=TimeSpan.FromMilliseconds((compact ? 300:480)+(seed%5)*(compact ? 20:30));
                double dx=compact ? (x-(columns-1)/2.0)*5+(seed%5)-2:(x-2.5)*15+(seed%9)-4;
                shift.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(0,dx,duration) {EasingFunction=new QuadraticEase {EasingMode=EasingMode.EaseOut}});
                // The drop target sits at the bottom. Scatter back into the window,
                // otherwise nearly every fragment is clipped immediately by that edge.
                double dy=compact ? -Math.Min(42,8+height*.22+seed%11):-75-y*12-(seed%17);
                shift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,dy,duration) {EasingFunction=new QuadraticEase {EasingMode=EasingMode.EaseOut}});
                rotate.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(0,(seed%2==0 ? 1:-1)*((compact ? 7:15)+seed%(compact ? 14:25)),duration));
                var fade=new DoubleAnimation(1,0,duration);var current=piece;fade.Completed+=delegate {canvas.Children.Remove(current);};piece.BeginAnimation(UIElement.OpacityProperty,fade);
            }
        }
    }
}
