using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace DesktopNotes {
    public partial class MainWindow {
        NoteDragSession categoryDrag;Button categorySource;Border categoryGhost,categoryMarker,categoryTrashDrop;
        string draggedCategory,categoryTarget;bool categoryBefore,startingCategoryDrag,categoryOverTrash;Point categoryOffset,categoryPointer;DispatcherTimer categoryScrollTimer;
        void BeginCategoryDrag(Button source,string id,Point point) {
            CancelCategoryDrag();if(!Service.State.Categories.Any(c=>c.Id==id))return;CancelStepDrag();CancelNoteDrag();
            categorySource=source;draggedCategory=id;categoryPointer=point;var origin=source.TranslatePoint(new Point(),root);categoryOffset=new Point(point.X-origin.X,point.Y-origin.Y);categoryDrag=new NoteDragSession(id,point.X,point.Y);
        }
        void MoveCategoryDrag(Point point,bool pressed) {
            if(categoryDrag==null || startingCategoryDrag)return;if(!pressed) {CancelCategoryDrag();return;}categoryPointer=point;
            if(!categoryDrag.Move(point.X,point.Y,Math.Max(SystemParameters.MinimumHorizontalDragDistance,SystemParameters.MinimumVerticalDragDistance)))return;
            if(categoryGhost==null) {
                startingCategoryDrag=true;
                try {
                    CommitEdit(false);if(!root.CaptureMouse() || categoryDrag==null) {CancelCategoryDrag();return;}
                    categoryGhost=new Border {Width=categorySource.ActualWidth*UiScaleFactor,Height=categorySource.ActualHeight*UiScaleFactor,CornerRadius=new CornerRadius(10),Background=Palette.Card,BorderBrush=Palette.Line,BorderThickness=new Thickness(1),Child=new Image {Source=UiMotion.Snapshot(categorySource,UiScaleFactor),Stretch=Stretch.Fill},Opacity=.95,IsHitTestVisible=false};effectsLayer.Children.Add(categoryGhost);categorySource.Opacity=.3;
                    ShowCategoryTrash();
                    categoryScrollTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(40)};categoryScrollTimer.Tick+=delegate {
                        if(Mouse.LeftButton!=MouseButtonState.Pressed)return;var viewport=BoundsInRoot(categoryScroll);if(!viewport.Contains(categoryPointer))return;
                        double delta=categoryPointer.Y<viewport.Top+20 ? -8:categoryPointer.Y>viewport.Bottom-20 ? 8:0;if(delta!=0) {categoryScroll.ScrollToVerticalOffset(categoryScroll.VerticalOffset+delta);categoryScroll.UpdateLayout();UpdateCategoryTarget(categoryPointer);}
                    };categoryScrollTimer.Start();
                }catch {CancelCategoryDrag();return;}finally {startingCategoryDrag=false;}
            }
            Canvas.SetLeft(categoryGhost,Math.Max(3,Math.Min(root.ActualWidth-categoryGhost.Width-3,point.X-categoryOffset.X)));Canvas.SetTop(categoryGhost,Math.Max(3,Math.Min(root.ActualHeight-categoryGhost.Height-3,point.Y-categoryOffset.Y)));UpdateCategoryTarget(point);
        }
        void UpdateCategoryTarget(Point point) {
            categoryTarget=null;if(categoryMarker!=null)effectsLayer.Children.Remove(categoryMarker);categoryMarker=null;
            var trashBounds=BoundsInRoot(categoryTrashDrop);categoryOverTrash=trashBounds.Contains(point) && !Privacy.IsLocked(draggedCategory);
            categoryTrashDrop.Background=new SolidColorBrush(categoryOverTrash ? Color.FromRgb(202,57,80):Color.FromRgb(67,69,78));
            if(categoryGhost!=null)categoryGhost.Opacity=categoryOverTrash ? .3:.95;
            if(categoryOverTrash)return;
            var viewport=BoundsInRoot(categoryScroll);if(!viewport.Contains(point))return;
            foreach(var button in tabs.Children.OfType<Button>()) {
                var tag=button.Tag as string;if(tag==null || !tag.StartsWith("category:"))continue;string id=tag.Substring(9);if(id==draggedCategory)continue;
                var bounds=BoundsInRoot(button);if(!bounds.Contains(point))continue;
                categoryTarget=id;categoryBefore=point.Y<bounds.Top+bounds.Height/2;categoryMarker=new Border {Width=bounds.Width,Height=2,CornerRadius=new CornerRadius(1),Background=Palette.Accent,IsHitTestVisible=false};Canvas.SetLeft(categoryMarker,bounds.Left);Canvas.SetTop(categoryMarker,Math.Max(viewport.Top,Math.Min(viewport.Bottom-2,categoryBefore ? bounds.Top-1:bounds.Bottom)));effectsLayer.Children.Add(categoryMarker);return;
            }
        }
        bool FinishCategoryDrag() {
            bool active=categoryGhost!=null;if(categoryDrag==null)return false;
            try {
                if(active && categoryOverTrash && !Privacy.IsLocked(draggedCategory)) {
                    CommitEdit(false);var old=Service.State.Categories.ToList();var affected=Service.State.Notes.Where(n=>n.CategoryId==draggedCategory).ToList();string previousActive=this.active,id=draggedCategory;
                    Service.DeleteCategory(id);if(this.active==id)this.active="inbox";
                    if(Save()) {categoryGhost.Opacity=1;UiMotion.Shatter(categoryGhost,effectsLayer,new Point(Canvas.GetLeft(categoryGhost),Canvas.GetTop(categoryGhost)));}
                    else {Service.State.Categories=old;foreach(var note in affected)note.CategoryId=id;this.active=previousActive;}
                    Refresh();
                } else if(active && categoryTarget!=null) {var old=Service.State.Categories;Service.ReorderCategory(draggedCategory,categoryTarget,categoryBefore);if(!Save())Service.State.Categories=old;Refresh();}
            }finally {CancelCategoryDrag();}return active;
        }
        void CancelCategoryDrag() {
            var session=categoryDrag;categoryDrag=null;if(session!=null)session.Cancel();if(categoryScrollTimer!=null) {categoryScrollTimer.Stop();categoryScrollTimer=null;}
            if(categorySource!=null)categorySource.Opacity=1;if(categoryGhost!=null)effectsLayer.Children.Remove(categoryGhost);if(categoryMarker!=null)effectsLayer.Children.Remove(categoryMarker);
            categorySource=null;categoryGhost=null;categoryMarker=null;draggedCategory=null;categoryTarget=null;categoryOverTrash=false;
            if(categoryTrashDrop!=null)categoryTrashDrop.Visibility=Visibility.Collapsed;
            if(session!=null && root!=null && Mouse.Captured==root && !moving && stepDrag==null && noteDrag==null)root.ReleaseMouseCapture();
        }
        void ShowCategoryTrash() {
            if(categoryTrashDrop==null) {
                var contents=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
                contents.Children.Add(UiDesign.Icon("trash",Brushes.White,18));contents.Children.Add(new TextBlock {Text="删除分类",Foreground=Brushes.White,FontSize=11,Margin=new Thickness(5,0,0,0),VerticalAlignment=VerticalAlignment.Center});
                categoryTrashDrop=new Border {Height=46,CornerRadius=new CornerRadius(14),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,BorderBrush=new SolidColorBrush(Color.FromRgb(125,129,143)),BorderThickness=new Thickness(1),Child=contents,IsHitTestVisible=false,ToolTip="删除分类，事项保留并移入未完成"};Panel.SetZIndex(categoryTrashDrop,999);root.Children.Add(categoryTrashDrop);
            }
            var anchor=addCategoryButton.TranslatePoint(new Point(),root);categoryTrashDrop.Width=BoundsInRoot(addCategoryButton).Width;
            categoryTrashDrop.Height=46*UiScaleFactor;categoryTrashDrop.CornerRadius=new CornerRadius(14*UiScaleFactor);((FrameworkElement)categoryTrashDrop.Child).LayoutTransform=new ScaleTransform(UiScaleFactor,UiScaleFactor);
            categoryTrashDrop.Margin=new Thickness(anchor.X,Math.Max(4,anchor.Y-categoryTrashDrop.Height-10),0,0);categoryTrashDrop.Background=new SolidColorBrush(Color.FromRgb(67,69,78));categoryTrashDrop.Visibility=Visibility.Visible;root.UpdateLayout();
        }
    }
}


