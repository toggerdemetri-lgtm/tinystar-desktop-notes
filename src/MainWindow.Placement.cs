using System;
using System.Linq;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace DesktopNotes {
    public partial class MainWindow {
        // Settings keep the user's preferred geometry. A display fallback is only
        // the current visible geometry, and must not replace those preferences. Geometry is committed only by explicit user drag, resize or reset actions.
        bool temporaryPlacement,applyingPlacement,placementChecked;
        DispatcherTimer placementRetryTimer;
        void RememberPlacement() {
            if(!ready || temporaryPlacement || applyingPlacement)return;
            var p=Service.State.Settings;var b=Host.Bounds();
            p.Left=b[0];p.Top=b[1];p.Width=ActualWidth;p.Height=ActualHeight;
        }
        void AcceptCurrentPlacement() {
            temporaryPlacement=false;if(placementRetryTimer!=null)placementRetryTimer.Stop();
            RememberPlacement();StartupTrace.Record("placement-user",Store,Service.State,this);
        }
        void KeepOnScreen() {
            if(exiting || retired)return;
            var p=Service.State.Settings;
            double scale=ready ? Host.Scale:DesktopHost.WindowScale(this);
            var screens=Forms.Screen.AllScreens.Select(s=>s.WorkingArea).ToArray();
            bool fallback=!ScreenPlacement.TitleVisible(p.Left,p.Top,p.Width,scale,screens);
            bool changed=!placementChecked || temporaryPlacement!=fallback;
            temporaryPlacement=fallback;placementChecked=true;
            double x=p.Left,y=p.Top,width=Math.Max(MinWidth,p.Width),height=Math.Max(MinHeight,p.Height);
            if(fallback) {
                var area=Forms.Screen.PrimaryScreen.WorkingArea;
                width=Math.Max(MinWidth,Math.Min(width,area.Width/scale));height=Math.Max(MinHeight,Math.Min(height,area.Height/scale));
                x=Math.Max(area.Left/scale,area.Right/scale-width-32);y=area.Top/scale+Math.Min(60,Math.Max(0,area.Height/scale-height));
            }
            applyingPlacement=true;
            try {
                Width=width;Height=height;
                if(ready) {
                    var b=Host.Bounds();if(Math.Abs(b[0]-x)>.5 || Math.Abs(b[1]-y)>.5)Host.Move(x,y);
                }else {Left=x;Top=y;}
            }finally {applyingPlacement=false;}
            if(fallback && ready) {
                if(placementRetryTimer==null) {
                    placementRetryTimer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(1)};
                    placementRetryTimer.Tick+=delegate {if(exiting || retired)placementRetryTimer.Stop();else if(!moving && !grips.Any(g=>g.IsDragging))KeepOnScreen();};
                }
                if(!placementRetryTimer.IsEnabled)placementRetryTimer.Start();
            }else if(placementRetryTimer!=null)placementRetryTimer.Stop();
            if(changed)StartupTrace.Record(fallback ? "placement-fallback":"placement-restored",Store,Service.State,this);
        }
        internal object PlacementDiagnostic {
            get {
                var p=Service.State.Settings;
                return new {Preferred=new[]{p.Left,p.Top,p.Width,p.Height},Actual=ready ? Host.Bounds():new[]{Left,Top,Width,Height},Temporary=temporaryPlacement,Ready=ready,Scale=ready ? Host.Scale:DesktopHost.WindowScale(this),WorkAreas=Forms.Screen.AllScreens.Select(s=>new[]{s.WorkingArea.Left,s.WorkingArea.Top,s.WorkingArea.Width,s.WorkingArea.Height}).ToArray()};
            }
        }
    }
}

