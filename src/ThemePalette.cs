using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Data;
using System.ComponentModel;
using Microsoft.Win32;
namespace DesktopNotes {
    public sealed class ThemePalette {
        public readonly SolidColorBrush Surface=LiveBrush(),Sidebar=LiveBrush(),Card=LiveBrush(),Input=LiveBrush(),Line=LiveBrush(),Selected=LiveBrush(),Hover=LiveBrush();
        public readonly SolidColorBrush Text=LiveBrush(),Secondary=LiveBrush(),AccentText=LiveBrush(),Accent=LiveBrush(),Icon=LiveBrush(),CheckInk=LiveBrush();
        public readonly SolidColorBrush ScrollThumb=LiveBrush(),ScrollThumbHover=LiveBrush(),ScrollThumbPressed=LiveBrush();
        public bool IsDark {get;private set;}
        sealed class PaintValue : INotifyPropertyChanged {
            public Color Color {get;set;}public double Opacity {get;set;}
            public event PropertyChangedEventHandler PropertyChanged;
            public void Changed() {var handler=PropertyChanged;if(handler!=null) {handler(this,new PropertyChangedEventArgs("Color"));handler(this,new PropertyChangedEventArgs("Opacity"));}}
        }
        static readonly DependencyProperty PaintProperty=DependencyProperty.RegisterAttached("LivePaint",typeof(PaintValue),typeof(ThemePalette));
        static SolidColorBrush LiveBrush() {
            var value=new PaintValue {Color=Colors.Transparent,Opacity=1};var brush=new SolidColorBrush();brush.SetValue(PaintProperty,value);
            BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding("Color") {Source=value});
            BindingOperations.SetBinding(brush,Brush.OpacityProperty,new Binding("Opacity") {Source=value});return brush;
        }
        static void Paint(SolidColorBrush brush,string color,double opacity) {var value=(PaintValue)brush.GetValue(PaintProperty);value.Color=(Color)ColorConverter.ConvertFromString(color);value.Opacity=opacity;value.Changed();}
        public static bool SystemIsDark() {try {using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))return key!=null && Convert.ToInt32(key.GetValue("AppsUseLightTheme",1))==0;}catch {return false;}}
        public void Apply(Preferences p,bool opaque) {
            IsDark=p.ThemeMode=="dark" || (p.ThemeMode!="light" && SystemIsDark());
            double bg=opaque ? 1 : Math.Max(1.0/255,p.BackgroundOpacity),text=opaque ? 1 : p.TextOpacity;
            Paint(Surface,IsDark ? "#181A1F":"#F5F6FA",bg);Paint(Sidebar,IsDark ? "#13151A":"#ECEFF5",bg);
            Paint(Card,IsDark ? "#24272E":"#FFFFFF",bg);Paint(Input,IsDark ? "#2B2E36":"#FFFFFF",bg);
            Paint(Line,IsDark ? "#363A43":"#E2E6EF",bg*.7);Paint(Selected,IsDark ? "#243C65":"#DCE8FF",bg);Paint(Hover,IsDark ? "#343945":"#E5EBF5",bg);
            Paint(Text,IsDark ? "#F2F3F5":"#1A1C20",text);Paint(Secondary,IsDark ? "#A1A7B3":"#727783",text);Paint(AccentText,IsDark ? "#8AB4FF":"#0A59F7",text);
            Paint(Accent,IsDark ? "#8AB4FF":"#0A59F7",1);Paint(Icon,IsDark ? "#CFD5E0":"#565E6D",1);
            Paint(CheckInk,IsDark ? "#181A1F":"#FFFFFF",1);
            string scroll=IsDark ? "#A4B1C8":"#687A98";Paint(ScrollThumb,scroll,.42);Paint(ScrollThumbHover,scroll,.68);Paint(ScrollThumbPressed,scroll,.88);
        }
        public void Resources(FrameworkElement element) {
            ScrollBarDesign.Resources(element,ScrollThumb,ScrollThumbHover,ScrollThumbPressed);
            element.Resources["InkBrush"]=Text;element.Resources["HoverBrush"]=Hover;
            element.Resources["MenuCardBrush"]=Card;element.Resources["MenuLineBrush"]=Line;
            element.Resources[SystemColors.WindowBrushKey]=Card;element.Resources[SystemColors.ControlBrushKey]=Surface;
            element.Resources[SystemColors.MenuBrushKey]=Card;element.Resources[SystemColors.MenuTextBrushKey]=Text;
            element.Resources[SystemColors.HighlightBrushKey]=Selected;element.Resources[SystemColors.HighlightTextBrushKey]=Text;
        }
        static readonly ControlTemplate MenuTemplate=(ControlTemplate)XamlReader.Parse(
            @"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ContextMenu'>
              <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='12' Padding='5'>
                <ScrollViewer HorizontalScrollBarVisibility='Disabled' VerticalScrollBarVisibility='Auto' MaxHeight='600'><ItemsPresenter/></ScrollViewer>
              </Border></ControlTemplate>");
        static readonly ControlTemplate MenuItemTemplate=(ControlTemplate)XamlReader.Parse(
            @"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='MenuItem'>
              <Grid>
                <Border x:Name='highlight' Background='Transparent' CornerRadius='7' Padding='12,8'>
                  <Grid><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='18'/></Grid.ColumnDefinitions>
                    <ContentPresenter ContentSource='Header' RecognizesAccessKey='True' VerticalAlignment='Center' TextElement.Foreground='{TemplateBinding Foreground}'/>
                    <Path x:Name='arrow' Grid.Column='1' Visibility='Collapsed' Data='M0,0 L4,4 L0,8' Stroke='{TemplateBinding Foreground}' StrokeThickness='1.3' HorizontalAlignment='Right' VerticalAlignment='Center'/>
                  </Grid>
                </Border>
                <Popup x:Name='PART_Popup' AllowsTransparency='True' Focusable='False' Placement='Right' HorizontalOffset='2' PopupAnimation='Fade' IsOpen='{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent},Mode=TwoWay}'>
                  <Border Background='{DynamicResource MenuCardBrush}' BorderBrush='{DynamicResource MenuLineBrush}' BorderThickness='1' CornerRadius='12' Padding='5'>
                    <ScrollViewer HorizontalScrollBarVisibility='Disabled' VerticalScrollBarVisibility='Auto' MaxHeight='600'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Cycle'/></ScrollViewer>
                  </Border>
                </Popup>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='highlight' Property='Background' Value='{DynamicResource HoverBrush}'/></Trigger>
                <Trigger Property='HasItems' Value='True'><Setter TargetName='arrow' Property='Visibility' Value='Visible'/></Trigger>
                <Trigger Property='IsEnabled' Value='False'><Setter TargetName='highlight' Property='Opacity' Value='.4'/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>");
        public void StyleMenu(ContextMenu menu) {
            Resources(menu);menu.Background=Card;menu.Foreground=Text;menu.BorderBrush=Line;menu.BorderThickness=new Thickness(1);menu.Padding=new Thickness(0);menu.FontFamily=new FontFamily("Microsoft YaHei UI");menu.FontSize=13;
            menu.Template=MenuTemplate;var style=new Style(typeof(MenuItem));style.Setters.Add(new Setter(Control.TemplateProperty,MenuItemTemplate));style.Setters.Add(new Setter(Control.ForegroundProperty,Text));style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty,150.0));
            menu.ItemContainerStyle=style;menu.Resources[typeof(MenuItem)]=style;
        }
    }
    public static class UiDesign {
        public const double NoteCornerRadius=20;
        static readonly ControlTemplate ButtonTemplate=(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border x:Name='body' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' CornerRadius='10' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='body' Property='Background' Value='{DynamicResource HoverBrush}'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='body' Property='Opacity' Value='0.7'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='body' Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
        public static Button Button(string text,string tip) {
            var b=new Button {Content=text,ToolTip=tip,Template=ButtonTemplate,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(10,9,10,9),Margin=new Thickness(2),Cursor=System.Windows.Input.Cursors.Hand,HorizontalContentAlignment=HorizontalAlignment.Center,FontSize=13};
            b.SetResourceReference(Control.ForegroundProperty,"InkBrush");System.Windows.Automation.AutomationProperties.SetName(b,tip);return b;
        }
        public static FrameworkElement Icon(string name,Brush brush,double size) {
            string data;
            switch(name) {
                case "lock":data="M5,10 L5,6 C5,0 15,0 15,6 L15,10 M3,10 L17,10 L17,21 L3,21 Z M10,14 L10,18";break;
                case "unlock":data="M5,10 L5,6 C5,0 15,0 15,6 M3,10 L17,10 L17,21 L3,21 Z M10,14 L10,18";break;
                case "settings":data="M10,0 L10,3 M10,17 L10,20 M0,10 L3,10 M17,10 L20,10 M3,3 L5,5 M15,15 L17,17 M3,17 L5,15 M15,5 L17,3 M15,10 A5,5 0 1 1 5,10 A5,5 0 1 1 15,10";break;
                case "close":data="M3,3 L17,17 M17,3 L3,17";break;
                case "plus":data="M10,2 L10,18 M2,10 L18,10";break;
                case "search":data="M14,8 A6,6 0 1 1 2,8 A6,6 0 1 1 14,8 M12.5,12.5 L19,19";break;
                case "trash":data="M3,5 L17,5 M7,5 L7,2 L13,2 L13,5 M5,5 L6,19 L14,19 L15,5 M9,8 L9,16 M12,8 L12,16";break;
                case "done":data="M2,10 L7,15 L18,4";break;
                case "inbox":data="M3,3 L17,3 L17,18 L3,18 Z M6,7 L14,7 M6,11 L12,11";break;
                default:data="M3,10 L4,10 M9,10 L10,10 M15,10 L16,10";break;
            }
            return new System.Windows.Shapes.Path {Data=Geometry.Parse(data),Stroke=brush,StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=size,Height=size,Stretch=Stretch.Uniform,IsHitTestVisible=false};
        }
    }
}
