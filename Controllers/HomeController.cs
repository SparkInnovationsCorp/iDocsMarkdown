using iDocsMarkdown.Models;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Renderers.Html;
using Markdig.Renderers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace iDocsMarkdown.Controllers
{
    public partial class HomeController : _BaseController
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _env;

        public HomeController(IConfiguration configuration, ILogger<HomeController> logger, IWebHostEnvironment env) : base(configuration)
        {
            _logger = logger;
            _env = env;
        }

        [Route("{*path}")]
        public async Task<IActionResult> Index(string path)
        {
            path = NormalizePath(path);
            string? requestedPath = GetRequestedPath(path);

            if (requestedPath == null || !System.IO.File.Exists(GetMarkdownFilePath(requestedPath)))
            {
                return NotFound();
            }

            string mdContent = await System.IO.File.ReadAllTextAsync(GetMarkdownFilePath(requestedPath));

            SetViewBagProperties(mdContent);

            string htmlContent = ConvertMarkdownToHtml(mdContent);
            ViewBag.Html = htmlContent;

            ViewBag.NavItems = ParseHtmlForNavItems(htmlContent);
            ViewBag.TopicDropdown = await GenerateNavItemsForDropdown();

            return View("Index");
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) path = "index.html";
            if (path.EndsWith("/")) path += "index.html";
            return path.ToLower();
        }

        private string? GetRequestedPath(string path)
        {
            if (path.EndsWith(".html")) return path.Replace(".html", ".md");
            if (path.EndsWith(".htm")) return path.Replace(".htm", ".md");
            return null;
        }

        private string GetMarkdownFilePath(string requestedPath)
        {
            return Path.Combine(_env.WebRootPath, "md", requestedPath);
        }

        private void SetViewBagProperties(string mdContent)
        {
            ViewBag.SiteName = _configuration["Site:Name"];
            ViewBag.OwningSiteUrl = _configuration["Site:OwningSiteUrl"];
            ViewBag.SupportUrl = _configuration["Site:SupportUrl"];
            ViewBag.TwitterUrl = _configuration["SocialMedia:TwitterUrl"];
            ViewBag.FacebookUrl = _configuration["SocialMedia:FacebookUrl"];
            ViewBag.DribbleUrl = _configuration["SocialMedia:DribbleUrl"];
            ViewBag.GitHubUrl = _configuration["SocialMedia:GitHubUrl"];
            ViewBag.PageTitle = ExtractTitle(mdContent);
            ViewBag.PageDescription = ExtractDescription(mdContent);
            ViewBag.PageKeywords = ExtractKeywords(mdContent);
        }

        private string ConvertMarkdownToHtml(string mdContent)
        {
            mdContent = RemoveMetaHeader(mdContent);
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            StringBuilder html = new StringBuilder(Markdown.ToHtml(mdContent, pipeline));
            html.Replace("<table>", "<table class=\"table table-bordered table-striped\">");

            string imgPattern = "<img(.*?)>";
            string imgReplacement = "<img$1 class=\"img-fluid\">";
            return Regex.Replace(html.ToString(), imgPattern, imgReplacement, RegexOptions.IgnoreCase);
        }

        private async Task<NavItem> GenerateNavItemsForDropdown()
        {
            var rootNavItem = new NavItem { Title = "Browse Topics", Target = "#", NavItems = new List<NavItem>() };
            return await ParseMarkdownDocuments(Path.Combine(_env.WebRootPath, "md"), rootNavItem);
        }

        public async Task<NavItem> ParseMarkdownDocuments(string path, NavItem navItem)
        {
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

            foreach (var dirPath in Directory.GetDirectories(path))
            {
                var newNavItem = new NavItem
                {
                    Title = textInfo.ToTitleCase(Path.GetFileName(dirPath)),
                    Target = "#",
                    NavItems = new List<NavItem>()
                };
                navItem.NavItems.Add(newNavItem);
                await ParseMarkdownDocuments(dirPath, newNavItem);
            }

            foreach (var filePath in Directory.GetFiles(path, "*.md"))
            {
                string mdContent = await System.IO.File.ReadAllTextAsync(filePath);
                string title = ExtractTitle(mdContent);
                string webPath = ConvertFilePathToWebPath(filePath);

                var newNavItem = new NavItem
                {
                    Title = !string.IsNullOrEmpty(title) ? title.Trim() : textInfo.ToTitleCase(Path.GetFileNameWithoutExtension(filePath)),
                    Target = webPath
                };
                navItem.NavItems.Add(newNavItem);
            }

            navItem.NavItems = navItem.NavItems.OrderBy(item => item.Title).ToList();
            return navItem;
        }

        private string ConvertFilePathToWebPath(string filePath)
        {
            string rootPath = Path.Combine(_env.WebRootPath, "md");
            string relativePath = Path.GetRelativePath(rootPath, filePath);
            return "/" + relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(".md", ".html");
        }

        public List<NavItem> ParseHtmlForNavItems(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var navItems = new List<NavItem>();
            var stack = new Stack<(int level, NavItem item)>();

            foreach (var node in doc.DocumentNode.SelectNodes("//h1|//h2|//h3|//h4|//h5|//h6"))
            {
                int level = int.Parse(node.Name.Substring(1));
                var navItem = new NavItem
                {
                    Title = node.InnerText.Trim(),
                    Target = node.Id,
                    NavItems = new List<NavItem>()
                };

                while (stack.Count > 0 && stack.Peek().level >= level)
                {
                    stack.Pop();
                }

                if (stack.Count > 0)
                {
                    stack.Peek().item.NavItems.Add(navItem);
                }
                else
                {
                    navItems.Add(navItem);
                }

                stack.Push((level, navItem));
            }

            return navItems;
        }

        private string ExtractTitle(string markdownContent)
        {
            return ExtractMetadata(markdownContent, @"^title:\s*(.+)$");
        }

        private string ExtractDescription(string markdownContent)
        {
            return ExtractMetadata(markdownContent, @"^description:\s*(.+)$");
        }

        private string ExtractKeywords(string markdownContent)
        {
            return ExtractMetadata(markdownContent, @"^keywords:\s*(.+)$");
        }

        private string ExtractMetadata(string markdownContent, string pattern)
        {
            var regex = new Regex(pattern, RegexOptions.Multiline);
            var match = regex.Match(markdownContent);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private string RemoveMetaHeader(string markdownContent)
        {
            // Define a regular expression to match the header delimited by --- at the start of the content
            var regex = new Regex(@"^---[\s\S]*?---\s+", RegexOptions.Multiline);

            // Check if the content starts with the meta header pattern
            if (markdownContent.Trim().StartsWith("---"))
            {
                // Replace the matched header with an empty string
                return regex.Replace(markdownContent, string.Empty, 1);
            }

            // Return the original content if no meta header is found at the top
            return markdownContent;
        }

    }
}
