using System;
using System.Windows.Controls;
namespace DesktopNotes {
    public partial class MainWindow {
        Button addCategoryButton;CategoryAddPopup categoryAddPopup;
        void ToggleCategoryAddPopup() {
            if(categoryAddPopup!=null && categoryAddPopup.IsOpen) {categoryAddPopup.IsOpen=false;return;}
            if(!CommitCategoryRename(false))return;
            CancelCategoryDrag();CancelStepDrag();CancelNoteDrag();CommitEdit(false);
            var popup=new CategoryAddPopup(this,addCategoryButton);categoryAddPopup=popup;
            popup.Closed+=delegate {if(categoryAddPopup==popup)categoryAddPopup=null;};popup.IsOpen=true;
        }
        internal bool TryCreateCategory(string name,out string error) {
            error=null;Category category;
            try {category=Service.AddCategory(name);}catch(ArgumentException ex){error=ex.Message;return false;}
            if(!Save()) {Service.State.Categories.Remove(category);error="保存失败，名称已保留，请稍后重试。";return false;}
            active=category.Id;Refresh();return true;
        }
    }
}
