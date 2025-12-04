# FilterableDataGrid 使用说明

## 问题解答

### 1. 列标题是动态显示的吗？
**是的**，列标题是动态显示的。列定义存储在 `FilterableDataGrid.Columns` 属性中，在运行时这些列会被复制（克隆）到内部的 `InnerDataGrid` 中。

### 2. 为什么在 Designer 中看不到列标题？
在 Designer（设计器）中看不到列标题是正常的，因为：
- 列是在运行时通过 `SetupColumns()` 方法动态添加的
- XAML Designer 不会执行代码逻辑，所以无法显示运行时生成的列

在运行时的效果：
- 列标题会正常显示
- 筛选行会显示在每个列标题下方
- 数据会正常绑定和显示

### 3. 数据不显示的问题已修复
修复内容：
- ✅ 改进了初始化顺序，确保在 `Loaded` 事件中正确设置列和数据源
- ✅ 添加了 `CollectionViewSource.View.Refresh()` 确保视图刷新
- ✅ 修复了列的克隆逻辑，避免"列已属于另一个DataGrid"的错误
- ✅ 改进了列的设置逻辑，避免重复设置

## 使用方法

在 XAML 中定义列（在运行时会被动态添加）：

```xaml
<controls:FilterableDataGrid
    ItemsSource="{Binding Articles}"
    SelectedItem="{Binding SelectedArticle, Mode=TwoWay}">
    <controls:FilterableDataGrid.Columns>
        <DataGridTextColumn
            Width="80"
            Binding="{Binding Id}"
            Header="ID" />
        <DataGridTextColumn
            Width="*"
            Binding="{Binding Name}"
            Header="物料编码" />
        <!-- 更多列... -->
    </controls:FilterableDataGrid.Columns>
</controls:FilterableDataGrid>
```

## 工作原理

1. **列定义阶段**：XAML 中定义的列存储在 `Columns` 依赖属性中
2. **列克隆阶段**：在 `SetupColumns()` 中，列被克隆并添加到内部的 `InnerDataGrid`
3. **筛选行创建**：根据列的数量和属性路径，为每列创建对应的筛选输入框
4. **数据绑定**：使用 `CollectionViewSource` 包装数据源，并应用筛选逻辑

## 注意事项

- 列标题只在运行时显示，设计器中看不到是正常的
- 筛选是基于属性的字符串匹配（不区分大小写）
- 多列筛选条件是 AND 关系（必须同时满足）
- 列的 `Binding` 属性路径用于筛选，确保 `DataGridTextColumn.Binding` 正确设置


