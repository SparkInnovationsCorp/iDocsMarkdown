namespace iDocsMarkdown.Models
{
    public class NavItem
    {
        public string Title { get; set; }
        public string Target { get; set; }
        public List<NavItem> NavItems { get; set; }
    }

}
