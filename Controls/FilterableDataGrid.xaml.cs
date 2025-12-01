using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace MiniERP.UI.Controls
{
    /// <summary>
    /// FilterableDataGrid.xaml 的交互逻辑
    /// 支持多列筛选的DataGrid控件
    /// </summary>
    public partial class FilterableDataGrid : UserControl
    {
        private CollectionViewSource? _collectionViewSource;
        private Dictionary<int, FilterColumnInfo> _filterColumns = new();

        public FilterableDataGrid()
        {
            InitializeComponent();
            if (Columns == null)
                Columns = new ObservableCollection<DataGridColumn>();

            this.Loaded += FilterableDataGrid_Loaded;
            ShowFilterRow = true;
        }


        private bool _isInitialized = false;
        private bool _isSettingColumns = false; // 防止重复设置列的标志

        private void FilterableDataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            // 避免重复初始化
            if (_isInitialized) return;
            
            // 确保列已设置（只有在列确实为空或数量不匹配时才设置）
            if (Columns != null && Columns.Count > 0)
            {
                // 只有在列数量不匹配时才设置
                if (InnerDataGrid.Columns.Count != Columns.Count)
                {
                    SetupColumns();
                }
                // 如果列数量匹配，SetupColumns内部会检查是否需要更新
                else if (InnerDataGrid.Columns.Count == 0)
                {
                    SetupColumns();
                }
            }
            SetupCollectionViewSource();

            // 设置行高调整功能（只设置一次）

            // 延迟设置筛选绑定，确保列标题已渲染
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SetupAllFilterBindings();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            
            _isInitialized = true;
        }

        private void SetupAllFilterBindings()
        {
            // 延迟一点，确保列标题已完全渲染
            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var kvp in _filterColumns)
                {
                    var columnIndex = kvp.Key;
                    var filterInfo = kvp.Value;

                    if (columnIndex < InnerDataGrid.Columns.Count)
                    {
                        var column = InnerDataGrid.Columns[columnIndex];
                        SetupFilterBinding(column, filterInfo);
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        #region Dependency Properties

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(FilterableDataGrid),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(
                nameof(Columns),
                typeof(ObservableCollection<DataGridColumn>),
                typeof(FilterableDataGrid),
                new PropertyMetadata(null, OnColumnsChanged));

        public ObservableCollection<DataGridColumn> Columns
        {
            get => (ObservableCollection<DataGridColumn>)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(object),
                typeof(FilterableDataGrid),
                new PropertyMetadata(null));

        public object? SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly DependencyProperty ShowFilterRowProperty =
            DependencyProperty.Register(
                nameof(ShowFilterRow),
                typeof(bool),
                typeof(FilterableDataGrid),
                new PropertyMetadata(true));

        public bool ShowFilterRow
        {
            get => (bool)GetValue(ShowFilterRowProperty);
            set => SetValue(ShowFilterRowProperty, value);
        }

        #endregion

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FilterableDataGrid control)
            {
                // 如果控件已加载但列还没设置，先设置列
                if (control.IsLoaded && control.Columns != null && control.Columns.Count > 0)
                {
                    // 只有在列确实为空且不在设置过程中时才设置，避免重复
                    if (control.InnerDataGrid.Columns.Count == 0 && !control._isSettingColumns)
                    {
                        control.SetupColumns();
                    }
                }
                control.SetupCollectionViewSource();
            }
        }

        private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FilterableDataGrid control)
            {
                if (e.OldValue is ObservableCollection<DataGridColumn> oldCollection)
                {
                    oldCollection.CollectionChanged -= control.Columns_CollectionChanged;
                }

                if (e.NewValue is ObservableCollection<DataGridColumn> newCollection)
                {
                    newCollection.CollectionChanged += control.Columns_CollectionChanged;
                }

                // 只有在控件已加载且不在设置过程中时才设置列，避免重复设置
                if (control.IsLoaded && !control._isSettingColumns)
                {
                    // SetupColumns内部会检查是否需要更新，直接调用即可
                    control.SetupColumns();
                }
            }
        }

        private void Columns_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 只有在控件已加载且不在设置列的过程中才设置
            if (IsLoaded && !_isSettingColumns)
            {
                SetupColumns();
            }
        }

        private void SetupColumns()
        {
            // 防止重复设置
            if (_isSettingColumns) return;
            
            // 如果列已经设置且没有变化，不需要重新设置
            if (Columns == null || Columns.Count == 0)
            {
                // 如果没有列，清空内部列
                if (InnerDataGrid.Columns.Count > 0)
                {
                    InnerDataGrid.Columns.Clear();
                    _filterColumns.Clear();
                }
                return;
            }

            // 关键检查：如果列数量已经匹配，且每列的Header都匹配，就不需要重新设置
            if (InnerDataGrid.Columns.Count == Columns.Count && InnerDataGrid.Columns.Count > 0)
            {
                bool allMatch = true;
                for (int i = 0; i < Columns.Count; i++)
                {
                    var sourceColumn = Columns[i];
                    var existingColumn = InnerDataGrid.Columns[i];
                    
                    // 比较列的关键属性
                    string? sourceHeader = sourceColumn.Header?.ToString();
                    string? existingHeader = existingColumn.Header?.ToString();
                    
                    // 比较Binding（如果是DataGridTextColumn）
                    string? sourceBindingPath = null;
                    string? existingBindingPath = null;
                    
                    if (sourceColumn is DataGridTextColumn sourceTextCol && sourceTextCol.Binding is Binding sourceBinding)
                    {
                        sourceBindingPath = sourceBinding.Path.Path;
                    }
                    if (existingColumn is DataGridTextColumn existingTextCol && existingTextCol.Binding is Binding existingBinding)
                    {
                        existingBindingPath = existingBinding.Path.Path;
                    }
                    
                    if (sourceHeader != existingHeader || sourceBindingPath != existingBindingPath)
                    {
                        allMatch = false;
                        break;
                    }
                }
                
                // 如果所有列都匹配，直接返回，不重新设置
                if (allMatch) return;
            }

            // 设置标志，防止重复设置
            _isSettingColumns = true;
            
            try
            {
                // 清除现有列
                InnerDataGrid.Columns.Clear();
                _filterColumns.Clear();

                int index = 0;
                foreach (var column in Columns)
                {
                    // 克隆列对象以避免"列已属于另一个DataGrid"的错误
                    DataGridColumn newColumn = CloneColumn(column);

                    // 为每列创建筛选信息
                    string? propertyPath = null;

                    if (column is DataGridTextColumn textColumn && textColumn.Binding is Binding binding)
                    {
                        propertyPath = binding.Path.Path;
                    }

                    FilterColumnInfo? filterInfo = null;
                    if (!string.IsNullOrEmpty(propertyPath))
                    {
                        filterInfo = new FilterColumnInfo
                        {
                            PropertyPath = propertyPath,
                            ColumnIndex = index,
                            FilterText = string.Empty
                        };
                        _filterColumns[index] = filterInfo;
                    }

                    // 使用 XAML 中定义的模板
                    if (filterInfo != null)
                    {
                        var headerTemplate = this.Resources["FilterableHeaderTemplate"] as DataTemplate;
                        if (headerTemplate != null)
                        {
                            newColumn.HeaderTemplate = headerTemplate;
                            // 注意：DataGridColumn 没有 Tag 属性，我们通过其他方式关联 FilterColumnInfo
                            // 在 SetupFilterBinding 中通过列索引查找
                        }
                    }

                    // 添加到 DataGrid
                    InnerDataGrid.Columns.Add(newColumn);

                    index++;
                }
            }
            finally
            {
                // 重置标志
                _isSettingColumns = false;
            }
        }

        private void SetupFilterBinding(DataGridColumn column, FilterColumnInfo filterInfo)
        {
            // 查找列标题中的 TextBox
            var columnHeader = GetColumnHeader(column);
            if (columnHeader is not null)
            {
                var textBox = FindVisualChild<TextBox>(columnHeader, "FilterTextBox");
                if (textBox is not null)
                {
                    textBox.Tag = filterInfo;
                    // 设置绑定（使用 Explicit 模式，只在回车时更新）
                    var binding = new Binding("FilterText")
                    {
                        Source = filterInfo,
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.Explicit
                    };
                    textBox.SetBinding(TextBox.TextProperty, binding);

                    // 初始化文本
                    textBox.Text = filterInfo.FilterText;
                }
            }
        }

        private DataGridColumnHeader? GetColumnHeader(DataGridColumn column)
        {
            if (InnerDataGrid.Columns.Contains(column))
            {
                // 通过可视化树查找列标题
                var presenter = FindVisualChild<DataGridColumnHeadersPresenter>(InnerDataGrid);
                if (presenter is not null)
                {
                    var headers = GetVisualChildren<DataGridColumnHeader>(presenter);
                    foreach (var header in headers)
                    {
                        if (header.Column == column)
                            return header;
                    }
                }
            }
            return null;
        }

        private T? FindVisualChild<T>(DependencyObject parent, string? childName = null) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t && (childName == null || (child is FrameworkElement fe && fe.Name == childName)))
                {
                    return t;
                }
                var childOfChild = FindVisualChild<T>(child, childName);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        private IEnumerable<T> GetVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                    yield return t;
                foreach (var childOfChild in GetVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }

        private void FilterTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (sender is TextBox textBox)
                {
                    // 从 Tag 获取 FilterColumnInfo，或者通过查找父元素获取
                    FilterColumnInfo? filterInfo = textBox.Tag as FilterColumnInfo;

                    if (filterInfo == null)
                    {
                        // 如果 Tag 没有设置，尝试通过可视化树查找
                        var columnHeader = FindParent<DataGridColumnHeader>(textBox);
                        if (columnHeader?.Column != null)
                        {
                            var columnIndex = InnerDataGrid.Columns.IndexOf(columnHeader.Column);
                            _filterColumns.TryGetValue(columnIndex, out filterInfo);
                        }
                    }

                    if (filterInfo != null)
                    {
                        // 更新筛选文本
                        var bindingExpr = BindingOperations.GetBindingExpression(textBox, TextBox.TextProperty);
                        bindingExpr?.UpdateSource();

                        // 确保筛选文本已更新
                        filterInfo.FilterText = textBox.Text;

                        // 刷新筛选
                        _collectionViewSource?.View.Refresh();

                        // 移除焦点，完成筛选
                        textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    }
                }
            }
        }

        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T t)
                    return t;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private DataGridColumn CloneColumn(DataGridColumn original)
        {
            if (original is DataGridTextColumn textColumn)
            {
                var cloned = new DataGridTextColumn
                {
                    Header = textColumn.Header,
                    Binding = textColumn.Binding,
                    Width = textColumn.Width,
                    SortMemberPath = textColumn.SortMemberPath,
                    CanUserResize = textColumn.CanUserResize,
                    CanUserReorder = textColumn.CanUserReorder,
                    CanUserSort = textColumn.CanUserSort,
                    Visibility = textColumn.Visibility
                };
                return cloned;
            }

            throw new NotSupportedException($"不支持的列类型: {original.GetType()}");
        }

        private void SetupCollectionViewSource()
        {
            if (ItemsSource == null)
            {
                _collectionViewSource = null;
                InnerDataGrid.ItemsSource = null;
                return;
            }

            // 如果已经有 CollectionViewSource，先清理旧的 Filter
            if (_collectionViewSource != null)
            {
                _collectionViewSource.View.Filter = null;
            }

            _collectionViewSource = new CollectionViewSource
            {
                Source = ItemsSource
            };
            _collectionViewSource.View.Filter = FilterPredicate;

            InnerDataGrid.ItemsSource = _collectionViewSource.View;

            // 确保视图刷新
            _collectionViewSource.View.Refresh();
        }

        private bool FilterPredicate(object obj)
        {
            if (_filterColumns.Count == 0) return true;

            foreach (var filterInfo in _filterColumns.Values)
            {
                if (string.IsNullOrWhiteSpace(filterInfo.FilterText)) continue;

                try
                {
                    var propertyValue = GetPropertyValue(obj, filterInfo.PropertyPath);
                    if (propertyValue == null) return false;

                    var filterText = filterInfo.FilterText.ToLowerInvariant();
                    var propertyString = propertyValue.ToString()?.ToLowerInvariant() ?? string.Empty;

                    if (!propertyString.Contains(filterText))
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        private object? GetPropertyValue(object obj, string propertyPath)
        {
            if (obj == null || string.IsNullOrEmpty(propertyPath))
                return null;

            var parts = propertyPath.Split('.');
            object? current = obj;

            foreach (var part in parts)
            {
                if (current == null) return null;

                var property = current.GetType().GetProperty(part);
                if (property == null) return null;

                current = property.GetValue(current);
            }

            return current;
        }
    }
}

/// <summary>
/// 筛选列信息
/// </summary>
public class FilterColumnInfo : INotifyPropertyChanged
{
    private string _filterText = string.Empty;

    public string PropertyPath { get; set; } = string.Empty;
    public int ColumnIndex { get; set; }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText != value)
            {
                _filterText = value;
                OnPropertyChanged(nameof(FilterText));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

