namespace MiniERP.UI.Model
{
    public class TabPageModel
    {
        public string Title { get; set; }
        public object ContentView { get; set; }

        public TabPageModel(string title, object content)
        {
            Title = title;
            ContentView = content;
        }
    }
}
