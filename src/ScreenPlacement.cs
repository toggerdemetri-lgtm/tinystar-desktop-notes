using System;
using System.Drawing;
using System.Linq;
namespace DesktopNotes {
    public static class ScreenPlacement {
        public static bool TitleVisible(double x,double y,double width,double scale,Rectangle[] screens) {
            var title=new Rectangle((int)Math.Round(x*scale),(int)Math.Round(y*scale),(int)Math.Ceiling(width*scale),(int)Math.Ceiling(40*scale));
            return screens.Any(area=>area.Contains(title));
        }
    }
}
