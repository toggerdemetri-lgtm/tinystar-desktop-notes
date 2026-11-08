using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
namespace DesktopNotes {
    public static class ScrollBarDesign {
        static readonly Style CapsuleStyle=(Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'>
          <Setter Property='Width' Value='12'/><Setter Property='Background' Value='Transparent'/><Setter Property='Focusable' Value='False'/>
          <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'>
            <Grid Background='Transparent'>
              <Track x:Name='PART_Track' Orientation='{TemplateBinding Orientation}' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' ViewportSize='{TemplateBinding ViewportSize}' IsDirectionReversed='True'>
                <Track.DecreaseRepeatButton><RepeatButton x:Name='Decrease' Command='{x:Static ScrollBar.PageUpCommand}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
                <Track.Thumb><Thumb x:Name='ScrollThumb' MinHeight='32' Background='{DynamicResource ScrollThumbBrush}'><Thumb.Template><ControlTemplate TargetType='Thumb'>
                  <Border Background='Transparent'><Border x:Name='Pill' Width='6' HorizontalAlignment='Center' Background='{TemplateBinding Background}' Margin='0,2' CornerRadius='3'/></Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Pill' Property='Background' Value='{DynamicResource ScrollThumbHoverBrush}'/></Trigger>
                    <Trigger Property='IsDragging' Value='True'><Setter TargetName='Pill' Property='Background' Value='{DynamicResource ScrollThumbPressedBrush}'/></Trigger>
                    <DataTrigger Binding='{Binding Orientation,RelativeSource={RelativeSource AncestorType={x:Type ScrollBar}}}' Value='Horizontal'><Setter TargetName='Pill' Property='Width' Value='Auto'/><Setter TargetName='Pill' Property='Height' Value='6'/><Setter TargetName='Pill' Property='HorizontalAlignment' Value='Stretch'/><Setter TargetName='Pill' Property='VerticalAlignment' Value='Center'/><Setter TargetName='Pill' Property='Margin' Value='2,0'/></DataTrigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                <Track.IncreaseRepeatButton><RepeatButton x:Name='Increase' Command='{x:Static ScrollBar.PageDownCommand}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
              </Track>
            </Grid>
            <ControlTemplate.Triggers>
              <Trigger Property='Orientation' Value='Horizontal'>
                <Setter Property='Width' Value='Auto'/><Setter Property='Height' Value='12'/>
                <Setter TargetName='PART_Track' Property='IsDirectionReversed' Value='False'/>
                <Setter TargetName='ScrollThumb' Property='MinHeight' Value='0'/><Setter TargetName='ScrollThumb' Property='MinWidth' Value='32'/>
                <Setter TargetName='Decrease' Property='Command' Value='{x:Static ScrollBar.PageLeftCommand}'/><Setter TargetName='Increase' Property='Command' Value='{x:Static ScrollBar.PageRightCommand}'/>
              </Trigger>
              <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.35'/></Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate></Setter.Value></Setter>
        </Style>");
        public static void Resources(FrameworkElement element,Brush normal,Brush hover,Brush pressed) {
            element.Resources["ScrollThumbBrush"]=normal;element.Resources["ScrollThumbHoverBrush"]=hover;element.Resources["ScrollThumbPressedBrush"]=pressed;
            element.Resources[typeof(ScrollBar)]=CapsuleStyle;
        }
    }
}
