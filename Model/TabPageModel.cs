namespace MiniERP.UI.Model
{
    public class TabPageModel
    {
        public string Title { get; }
        public object ContentView { get; }

        public TabPageModel(string title, object content)
        {
            Title = title;
            ContentView = content;
        }
    }
}
