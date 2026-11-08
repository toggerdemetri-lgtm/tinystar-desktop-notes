using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace DesktopNotes {
    public partial class MainWindow {
        string noteDropCategory;Border noteCategoryHighlight;
        void ClearNoteCategoryTarget() {
            noteDropCategory=null;if(noteCategoryHighlight!=null && effectsLayer!=null)effectsLayer.Children.Remove(noteCategoryHighlight);noteCategoryHighlight=null;
        }
        void UpdateNoteCategoryTarget(Point point) {
            ClearNoteCategoryTarget();if(dragGhost!=null)dragGhost.Opacity=.9;if(overTrash || dragGhost==null)return;
            var viewport=BoundsInRoot(categoryScroll);if(!viewport.Contains(point))return;
            foreach(var button in tabs.Children.OfType<Button>()) {
                string tag=button.Tag as string;string id=button==tabs.Children[0] ? "inbox":tag!=null && tag.StartsWith("category:") ? tag.Substring(9):null;
                if(id==null || !Service.HasCategory(id))continue;
                var bounds=BoundsInRoot(button);if(!bounds.Contains(point))continue;
                noteDropCategory=id;noteCategoryHighlight=new Border {Width=bounds.Width,Height=bounds.Height,CornerRadius=new CornerRadius(10),BorderBrush=Palette.Accent,BorderThickness=new Thickness(1.5),Background=Palette.Selected,Opacity=.6,IsHitTestVisible=false};
                Canvas.SetLeft(noteCategoryHighlight,bounds.Left);Canvas.SetTop(noteCategoryHighlight,bounds.Top);effectsLayer.Children.Add(noteCategoryHighlight);dragGhost.Opacity=.45;return;
            }
        }
        void FinishNoteCategoryDrop() {
            string target=noteDropCategory;var note=Service.State.Notes.FirstOrDefault(n=>n.Id==dragNoteId);
            if(note==null || note.Deleted || !Privacy.CanSee(note) || !Service.HasCategory(target))return;
            string previous=note.CategoryId;if(previous==target)return;
            Service.Move(note.Id,target);if(!Save())note.CategoryId=previous;Refresh();
        }
    }
}

