using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace DesktopNotes {
    public sealed class CompletedDateCalendar : Grid {
        readonly ThemePalette palette;readonly Button heading;readonly UniformGrid body,week;
        readonly List<Button> dayButtons=new List<Button>();int mode;
        DateTime? selected,from,to;
        public DateTime DisplayMonth {get;private set;}
        public IEnumerable<Button> DayButtons {get{return dayButtons;}}
        public event Action<DateTime> DateSelected;
        public CompletedDateCalendar(ThemePalette palette,DateTime month) {
            this.palette=palette;RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});RowDefinitions.Add(new RowDefinition {Height=new GridLength(204)});
            var header=new Grid {Margin=new Thickness(0,0,0,5)};header.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(36)});header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(36)});Children.Add(header);
            var previous=UiDesign.Button("‹","上一个月或年份");previous.FontSize=24;previous.Padding=new Thickness(2,0,2,2);previous.Click+=delegate {PreviousPeriod();};header.Children.Add(previous);
            heading=UiDesign.Button("","选择月份或年份");heading.FontWeight=FontWeights.SemiBold;heading.Click+=delegate {mode=(mode+1)%3;Draw();};Grid.SetColumn(heading,1);header.Children.Add(heading);
            var next=UiDesign.Button("›","下一个月或年份");next.FontSize=24;next.Padding=new Thickness(2,0,2,2);next.Click+=delegate {NextPeriod();};Grid.SetColumn(next,2);header.Children.Add(next);
            week=new UniformGrid {Columns=7};foreach(var name in new[]{"一","二","三","四","五","六","日"})week.Children.Add(new TextBlock {Text=name,TextAlignment=TextAlignment.Center,Foreground=palette.Secondary,FontSize=11,Margin=new Thickness(0,0,0,4)});Grid.SetRow(week,1);Children.Add(week);
            body=new UniformGrid {Columns=7,Rows=6};Grid.SetRow(body,2);Children.Add(body);ShowMonth(month);
        }
        public void ShowMonth(DateTime value) {DisplayMonth=new DateTime(value.Year,value.Month,1);mode=0;Draw();}
        public void SetSelection(DateTime? value,DateTime? rangeFrom,DateTime? rangeTo) {selected=value;from=rangeFrom;to=rangeTo;Draw();}
        public void PreviousPeriod() {Move(-1);}
        public void NextPeriod() {Move(1);}
        void Move(int direction) {try {DisplayMonth=mode==0 ? DisplayMonth.AddMonths(direction):DisplayMonth.AddYears(direction*(mode==2 ? 12:1));}catch(ArgumentOutOfRangeException){return;}Draw();}
        Button Cell(string text,string label) {var button=UiDesign.Button(text,label);button.Margin=new Thickness(2);button.Padding=new Thickness(0);button.MinHeight=27;return button;}
        void Draw() {
            body.Children.Clear();dayButtons.Clear();week.Visibility=mode==0 ? Visibility.Visible:Visibility.Collapsed;
            if(mode==0) {
                heading.Content=DisplayMonth.ToString("yyyy年 M月",CultureInfo.InvariantCulture);body.Columns=7;body.Rows=6;
                int offset=((int)DisplayMonth.DayOfWeek+6)%7;
                for(int i=0;i<42;i++) {
                    long ticks=DisplayMonth.Ticks+(long)(i-offset)*TimeSpan.TicksPerDay;
                    if(ticks<DateTime.MinValue.Ticks || ticks>DateTime.MaxValue.Ticks){body.Children.Add(new Border());continue;}
                    var date=new DateTime(ticks);var day=Cell(date.Day.ToString(CultureInfo.InvariantCulture),date.ToString("yyyy年M月d日",CultureInfo.InvariantCulture));day.Tag=date;
                    if(date.Month!=DisplayMonth.Month)day.Opacity=.35;
                    if(from.HasValue && to.HasValue && date>=from.Value.Date && date<=to.Value.Date)day.Background=palette.Hover;
                    if((selected.HasValue && date==selected.Value.Date) || (from.HasValue && date==from.Value.Date) || (to.HasValue && date==to.Value.Date)){day.Background=palette.Selected;day.Foreground=palette.AccentText;day.FontWeight=FontWeights.SemiBold;}
                    if(date==DateTime.Today){day.BorderBrush=palette.Accent;day.BorderThickness=new Thickness(1);}
                    day.Click+=delegate {var callback=DateSelected;if(callback!=null)callback(date);};body.Children.Add(day);dayButtons.Add(day);
                }
            }else if(mode==1) {
                heading.Content=DisplayMonth.Year+"年";body.Columns=4;body.Rows=3;
                for(int month=1;month<=12;month++){int current=month;var cell=Cell(month+"月",DisplayMonth.Year+"年"+month+"月");if(month==DisplayMonth.Month){cell.Background=palette.Selected;cell.Foreground=palette.AccentText;}cell.Click+=delegate {ShowMonth(new DateTime(DisplayMonth.Year,current,1));};body.Children.Add(cell);}
            }else {
                int first=Math.Max(1,Math.Min(9988,DisplayMonth.Year-DisplayMonth.Year%12));heading.Content=first+"—"+(first+11)+"年";body.Columns=4;body.Rows=3;
                for(int year=first;year<first+12;year++){int current=year;var cell=Cell(year.ToString(CultureInfo.InvariantCulture),"选择"+year+"年");if(year==DisplayMonth.Year){cell.Background=palette.Selected;cell.Foreground=palette.AccentText;}cell.Click+=delegate {DisplayMonth=new DateTime(current,DisplayMonth.Month,1);mode=1;Draw();};body.Children.Add(cell);}
            }
        }
    }
}
