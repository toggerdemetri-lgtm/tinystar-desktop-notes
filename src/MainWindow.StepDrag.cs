using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace DesktopNotes {
    public partial class MainWindow {
        NoteDragSession stepDrag;Grid stepSource;Border stepGhost,stepMarker;
        string stepNoteId,stepId,stepTarget;bool stepBefore,stepStarting;
        Point stepOffset,stepPointer;DispatcherTimer stepScrollTimer;
        static Grid FindStepRow(DependencyObject node) {
            while(node!=null) {var grid=node as Grid;if(grid!=null && (grid.Tag as string)=="step-row" && !String.IsNullOrEmpty(AutomationProperties.GetAutomationId(grid)))return grid;node=Ancestor(node);}return null;
        }
        static bool IsStepControl(DependencyObject node,Grid row) {
            while(node!=null && node!=row) {if(node is ButtonBase || node is TextBoxBase || node is ScrollBar || node is Thumb || node is MenuItem)return true;node=Ancestor(node);}return false;
        }
        void BeginStepDrag(Grid source,Point pointer) {
            CancelStepDrag();CancelNoteDrag();var parent=FindNoteRow(source);if(parent==null)return;var note=Service.Find((string)parent.Tag);if(note.Deleted)return;
            stepNoteId=note.Id;stepId=AutomationProperties.GetAutomationId(source);stepSource=source;stepPointer=pointer;
            var origin=source.TranslatePoint(new Point(),root);stepOffset=new Point(pointer.X-origin.X,pointer.Y-origin.Y);stepDrag=new NoteDragSession(stepId,pointer.X,pointer.Y);
        }
        void MoveStepDrag(Point pointer,bool pressed) {
            if(stepDrag==null || stepStarting)return;if(!pressed) {CancelStepDrag();return;}stepPointer=pointer;
            if(!stepDrag.Move(pointer.X,pointer.Y,Math.Max(SystemParameters.MinimumHorizontalDragDistance,SystemParameters.MinimumVerticalDragDistance)))return;
            if(stepGhost==null) {
                stepStarting=true;
                try {
                    CommitEdit(false);if(!root.CaptureMouse() || stepDrag==null) {CancelStepDrag();return;}
                    var image=UiMotion.Snapshot(stepSource,UiScaleFactor);stepGhost=new Border {Width=stepSource.ActualWidth*UiScaleFactor,Height=stepSource.ActualHeight*UiScaleFactor,CornerRadius=new CornerRadius(7),Background=Palette.Card,BorderBrush=Palette.Line,BorderThickness=new Thickness(1),Child=new Image {Source=image,Stretch=Stretch.Fill},IsHitTestVisible=false,Opacity=.95};effectsLayer.Children.Add(stepGhost);stepSource.Opacity=.28;
                    stepScrollTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(40)};stepScrollTimer.Tick+=delegate {
                        // Let the routed MouseUp finish the drop; its dispatch may
                        // follow this timer tick after the physical button releases.
                        if(Mouse.LeftButton!=MouseButtonState.Pressed)return;
                        var viewport=BoundsInRoot(noteScroll);if(!viewport.Contains(stepPointer))return;
                        double delta=stepPointer.Y<viewport.Top+24 ? -10:stepPointer.Y>viewport.Bottom-24 ? 10:0;
                        if(delta!=0) {noteScroll.ScrollToVerticalOffset(noteScroll.VerticalOffset+delta);noteScroll.UpdateLayout();UpdateStepTarget(stepPointer);}
                    };stepScrollTimer.Start();
                } catch {CancelStepDrag();return;} finally {stepStarting=false;}
            }
            Canvas.SetLeft(stepGhost,Math.Max(3,Math.Min(root.ActualWidth-stepGhost.Width-3,pointer.X-stepOffset.X)));Canvas.SetTop(stepGhost,Math.Max(3,Math.Min(root.ActualHeight-stepGhost.Height-3,pointer.Y-stepOffset.Y)));UpdateStepTarget(pointer);
        }
        void UpdateStepTarget(Point pointer) {
            stepTarget=null;if(stepMarker!=null)effectsLayer.Children.Remove(stepMarker);stepMarker=null;
            var viewport=BoundsInRoot(noteScroll);if(!viewport.Contains(pointer))return;
            var parent=rows.Children.OfType<Border>().FirstOrDefault(r=>(r.Tag as string)==stepNoteId);if(parent==null || !BoundsInRoot(parent).Contains(pointer))return;
            var candidates=FindDescendants(parent).OfType<Grid>().Where(g=>(g.Tag as string)=="step-row" && !String.IsNullOrEmpty(AutomationProperties.GetAutomationId(g))).ToList();
            Grid target=null;
            foreach(var candidate in candidates) {
                var rect=BoundsInRoot(candidate);
                if(AutomationProperties.GetAutomationId(candidate)==stepId) {if(rect.Contains(pointer))return;continue;}
                target=candidate;if(pointer.Y<rect.Bottom)break;
            }
            if(target==null)return;var bounds=BoundsInRoot(target);if(pointer.X<bounds.Left || pointer.X>bounds.Right)return;
            stepTarget=AutomationProperties.GetAutomationId(target);stepBefore=pointer.Y<bounds.Top+bounds.Height/2;
            stepMarker=new Border {Width=bounds.Width,Height=2,CornerRadius=new CornerRadius(1),Background=Palette.Accent,IsHitTestVisible=false};Canvas.SetLeft(stepMarker,bounds.Left);Canvas.SetTop(stepMarker,Math.Max(viewport.Top,Math.Min(viewport.Bottom-2,stepBefore ? bounds.Top-1:bounds.Bottom)));effectsLayer.Children.Add(stepMarker);
        }
        void FinishStepDrag() {
            if(stepDrag==null)return;
            try {if(stepGhost!=null && stepTarget!=null) {var note=Service.Find(stepNoteId);var old=note.Steps;Service.ReorderStep(note.Id,stepId,stepTarget,stepBefore);if(!Save())note.Steps=old;Refresh();}}finally {CancelStepDrag();}
        }
        void CancelStepDrag() {
            var session=stepDrag;stepDrag=null;if(session!=null)session.Cancel();if(stepScrollTimer!=null) {stepScrollTimer.Stop();stepScrollTimer=null;}
            if(stepSource!=null)stepSource.Opacity=1;if(stepGhost!=null)effectsLayer.Children.Remove(stepGhost);if(stepMarker!=null)effectsLayer.Children.Remove(stepMarker);
            stepSource=null;stepGhost=null;stepMarker=null;stepNoteId=null;stepId=null;stepTarget=null;
            if(session!=null && root!=null && Mouse.Captured==root && !moving && noteDrag==null)root.ReleaseMouseCapture();
        }
        void DeleteStep(Note note,string id,Grid visual) {
            CommitEdit(false);var old=note.Steps;Service.SetSteps(note.Id,note.Steps.Where(s=>s.Id!=id));
            if(Save())UiMotion.ShatterStep(visual,effectsLayer,visual.TranslatePoint(new Point(),root),UiScaleFactor);else note.Steps=old;Refresh();
        }
    }
}

