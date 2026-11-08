using System;
using System.Windows;
using System.Windows.Media;
namespace DesktopNotes {
    public partial class MainWindow {
        internal double UiScaleFactor {get {return .5+Math.Max(0,Math.Min(100,Service.State.Settings.UiSize))/100;}}
        internal bool TrySetUiSize(double size) {
            if(Double.IsNaN(size) || Double.IsInfinity(size))return false;
            double previous=Service.State.Settings.UiSize;Service.State.Settings.UiSize=Math.Max(0,Math.Min(100,size));ApplyUiSize();
            if(Save())return true;Service.State.Settings.UiSize=previous;ApplyUiSize();return false;
        }
        void ApplyUiSize() {
            if(columns==null)return;double factor=UiScaleFactor;
            var transform=columns.LayoutTransform as ScaleTransform;if(transform==null || Math.Abs(transform.ScaleX-factor)>.001)columns.LayoutTransform=new ScaleTransform(factor,factor);
            sidebarColumn.MinWidth=100;columns.ColumnDefinitions[1].MinWidth=220/factor;ApplySidebarWidth();
            if(trashDrop!=null) {trashDrop.Width=150*factor;trashDrop.Height=54*factor;trashDrop.CornerRadius=new CornerRadius(20*factor);((FrameworkElement)trashDrop.Child).LayoutTransform=new ScaleTransform(factor,factor);}
        }
        Rect BoundsInRoot(FrameworkElement element) {return element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));}
    }
}

