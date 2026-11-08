using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace DesktopNotes {
    // Draw in the input's visual tree so a detached WPF caret adorner cannot
    // hide the insertion point. Native caret tracking remains available to IME
    // and accessibility; its transparent ink avoids two unsynchronized blinks.
    internal sealed class InputCaret : FrameworkElement {
        [DllImport("user32.dll")]static extern uint GetCaretBlinkTime();
        readonly TextBox input;
        readonly Brush ink;
        Rect caret=Rect.Empty,viewport=Rect.Empty;
        internal InputCaret(TextBox input) {
            this.input=input;ink=input.CaretBrush;input.CaretBrush=Brushes.Transparent;
            IsHitTestVisible=false;Focusable=false;ClipToBounds=true;SnapsToDevicePixels=true;
            RenderOptions.SetEdgeMode(this,EdgeMode.Aliased);
            input.SelectionChanged+=delegate {Refresh();RestartBlink();};
            input.TextChanged+=delegate {Refresh();RestartBlink();};
            input.PreviewMouseLeftButtonDown+=delegate {RestartBlink();};
            input.PreviewKeyDown+=delegate {RestartBlink();};
            input.IsKeyboardFocusWithinChanged+=delegate {Refresh();RestartBlink();};
            input.IsEnabledChanged+=delegate {Refresh();};
            input.IsVisibleChanged+=delegate {Refresh();};
            input.LayoutUpdated+=delegate {Refresh();};
            Loaded+=delegate {Refresh();RestartBlink();};
            Unloaded+=delegate {BeginAnimation(OpacityProperty,null);};
        }
        void RestartBlink() {
            BeginAnimation(OpacityProperty,null);Opacity=1;
            if(caret.IsEmpty || !input.IsKeyboardFocused)return;
            uint interval=GetCaretBlinkTime();
            if(interval==0 || interval==UInt32.MaxValue)return;
            var animation=new DoubleAnimationUsingKeyFrames {Duration=TimeSpan.FromMilliseconds(interval*2.0),RepeatBehavior=RepeatBehavior.Forever};
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(interval))));
            BeginAnimation(OpacityProperty,animation);
        }
        void Refresh() {
            Rect next=Rect.Empty,clip=Rect.Empty;
            if(IsLoaded && input.IsVisible && input.IsEnabled && input.IsKeyboardFocused && !input.IsReadOnly && input.SelectionLength==0) {
                Rect position=input.GetRectFromCharacterIndex(input.CaretIndex);
                if(!position.IsEmpty && position.Height>0) {
                    var transform=input.TransformToVisual(this);
                    next=transform.TransformBounds(new Rect(position.X,position.Y,Math.Max(2,SystemParameters.CaretWidth),position.Height));
                    var host=input.Template.FindName("PART_ContentHost",input) as FrameworkElement;
                    clip=host==null ? transform.TransformBounds(new Rect(input.RenderSize)):host.TransformToVisual(this).TransformBounds(new Rect(host.RenderSize));
                }
            }
            if(next==caret && clip==viewport)return;
            bool wasEmpty=caret.IsEmpty;caret=next;viewport=clip;InvalidateVisual();
            if(wasEmpty || caret.IsEmpty)RestartBlink();
        }
        protected override void OnRender(DrawingContext context) {
            base.OnRender(context);
            if(caret.IsEmpty || viewport.IsEmpty || !input.IsKeyboardFocused || input.SelectionLength!=0)return;
            context.PushClip(new RectangleGeometry(viewport));context.DrawRectangle(ink,null,caret);context.Pop();
        }
    }
}

