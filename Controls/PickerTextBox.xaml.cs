using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MiniERP.UI.Controls
{
    /// <summary>
    /// PickerTextBox.xaml 的交互逻辑
    /// </summary>
    public partial class PickerTextBox : UserControl
    {
        public PickerTextBox()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(PickerTextBox),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(PickerTextBox),
                new PropertyMetadata(true));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty BrowseCommandProperty =
            DependencyProperty.Register(nameof(BrowseCommand), typeof(ICommand), typeof(PickerTextBox));

        public ICommand BrowseCommand
        {
            get => (ICommand)GetValue(BrowseCommandProperty);
            set => SetValue(BrowseCommandProperty, value);
        }

        public static readonly DependencyProperty BrowseCommandParameterProperty =
            DependencyProperty.Register(nameof(BrowseCommandParameter), typeof(object), typeof(PickerTextBox));

        public object BrowseCommandParameter
        {
            get => GetValue(BrowseCommandParameterProperty);
            set => SetValue(BrowseCommandParameterProperty, value);
        }
    }
}
