using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace DesktopNotes {
    internal sealed class CategoryAddPopup : Popup {
        readonly MainWindow main;readonly Button anchor;readonly TextBox input;readonly TextBlock error;
        readonly ThemePalette palette=new ThemePalette();bool composing,submitting;
        internal CategoryAddPopup(MainWindow main,Button anchor) {
            this.main=main;this.anchor=anchor;PlacementTarget=anchor;Placement=PlacementMode.Custom;StaysOpen=false;AllowsTransparency=true;PopupAnimation=PopupAnimation.None;
            palette.Apply(main.Service.State.Settings,true);
            var shell=new Border {Width=300,Padding=new Thickness(12),Margin=new Thickness(8),CornerRadius=new CornerRadius(14),Background=palette.Surface,Effect=new DropShadowEffect {Color=Colors.Black,BlurRadius=14,ShadowDepth=2,Opacity=.24}};Child=shell;palette.Resources(shell);
            TextElement.SetFontFamily(shell,main.FontFamily);TextElement.SetFontSize(shell,13);TextElement.SetForeground(shell,palette.Text);
            var panel=new StackPanel();shell.Child=panel;var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});panel.Children.Add(row);
            var field=new Grid();
            input=new TextBox {MaxLength=200,FontSize=13,MinHeight=36,VerticalContentAlignment=VerticalAlignment.Center,Foreground=palette.Text,CaretBrush=palette.Accent,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(9,6,9,6),FocusVisualStyle=null};AutomationProperties.SetName(input,"新增分类名称");field.Children.Add(input);
            var placeholder=new TextBlock {Text="分类名称",Foreground=palette.Secondary,Margin=new Thickness(10,0,0,0),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};field.Children.Add(placeholder);
            row.Children.Add(new Border {Background=palette.Input,BorderBrush=palette.Accent,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Child=field});
            var create=MainWindow.Button("","创建分类");create.Width=36;create.Height=36;create.Padding=new Thickness(10);create.Margin=new Thickness(8,0,0,0);create.Background=palette.Selected;create.Content=UiDesign.Icon("done",palette.Accent,16);Grid.SetColumn(create,1);row.Children.Add(create);create.Click+=delegate{Submit();};
            error=new TextBlock {FontSize=11,Foreground=new SolidColorBrush(Color.FromRgb(222,125,113)),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(2,8,2,0),Visibility=Visibility.Collapsed};panel.Children.Add(error);
            input.TextChanged+=delegate{placeholder.Visibility=String.IsNullOrEmpty(input.Text) ? Visibility.Visible:Visibility.Collapsed;error.Visibility=Visibility.Collapsed;};
            TextCompositionManager.AddPreviewTextInputStartHandler(input,delegate{composing=true;});TextCompositionManager.AddPreviewTextInputHandler(input,delegate{composing=false;});input.LostKeyboardFocus+=delegate{composing=false;};
            KeyboardNavigation.SetTabNavigation(shell,KeyboardNavigationMode.Cycle);
            shell.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(!composing && e.Key==Key.Escape){IsOpen=false;anchor.Focus();e.Handled=true;}};
            input.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(composing || e.Key==Key.ImeProcessed)return;if(e.Key==Key.Enter){Submit();e.Handled=true;}};
            CustomPopupPlacementCallback=delegate(Size size,Size targetSize,Point offset) {
                var source=PresentationSource.FromVisual(anchor);var transform=source!=null && source.CompositionTarget!=null ? source.CompositionTarget.TransformFromDevice:Matrix.Identity;
                Point physical=anchor.PointToScreen(new Point());Point point=transform.Transform(physical);
                var screen=Forms.Screen.FromPoint(new System.Drawing.Point((int)physical.X,(int)physical.Y));var bounds=screen.WorkingArea;
                Point topLeft=transform.Transform(new Point(bounds.Left,bounds.Top)),bottomRight=transform.Transform(new Point(bounds.Right,bounds.Bottom));
                var location=Place(size,targetSize,point,new Rect(topLeft,bottomRight));
                return new[]{new CustomPopupPlacement(new Point(location.X-point.X,location.Y-point.Y),PopupPrimaryAxis.None)};
            };
            EventHandler changed=delegate{palette.Apply(main.Service.State.Settings,true);};main.AppearanceChanged+=changed;
            EventHandler close=delegate{IsOpen=false;};SizeChangedEventHandler resize=delegate{IsOpen=false;};
            main.Deactivated+=close;main.LocationChanged+=close;main.SizeChanged+=resize;
            Opened+=delegate{Dispatcher.BeginInvoke(new Action(delegate{if(IsOpen){input.Focus();Keyboard.Focus(input);}}),DispatcherPriority.Input);};
            Closed+=delegate{main.AppearanceChanged-=changed;main.Deactivated-=close;main.LocationChanged-=close;main.SizeChanged-=resize;composing=false;};
        }
        internal static Point Place(Size popup,Size target,Point anchor,Rect area) {
            double x=anchor.X+target.Width+8,y=anchor.Y+target.Height-popup.Height;
            if(x+popup.Width>area.Right)x=anchor.X-popup.Width-8;
            if(x<area.Left){x=anchor.X;y=anchor.Y-popup.Height-8;}
            return new Point(Math.Max(area.Left,Math.Min(x,area.Right-popup.Width)),Math.Max(area.Top,Math.Min(y,area.Bottom-popup.Height)));
        }
        internal bool Submit() {
            if(!IsOpen || submitting || composing)return false;submitting=true;
            try {string message;if(!main.TryCreateCategory(input.Text,out message)){error.Text=message;error.Visibility=Visibility.Visible;input.Focus();return false;}IsOpen=false;return true;}finally{submitting=false;}
        }
    }
}
