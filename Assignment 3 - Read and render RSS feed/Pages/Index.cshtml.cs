using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Xml;

namespace Assignment_3___Read_and_render_RSS_feed.Pages;

public class IndexModel : PageModel
{
    public List<RssItem> rssItemsList = new List<RssItem>();

    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        await loadxml();
    }

    public async Task loadxml()
    {
        string url = "http://scripting.com/rss.xml";

        using (HttpClient client = new HttpClient())
        {
            HttpResponseMessage response = await client.GetAsync(url);
            string xmlString = await response.Content.ReadAsStringAsync();

            XmlDocument document = new XmlDocument();
            document.LoadXml(xmlString);

            XmlElement? root = document.DocumentElement;
            XmlNodeList items = root.GetElementsByTagName("item");
            List<RssItem> itemsList = new List<RssItem>();

            foreach (XmlNode item in items)
            {
                XmlNode? titleNode = item.SelectSingleNode("title");
                string title = (titleNode != null) ? titleNode.InnerText : "";

                XmlNode? descriptionNode = item.SelectSingleNode("description");
                string description = (descriptionNode != null) ? descriptionNode.InnerText : "";
                HtmlString des = new HtmlString(description);

                XmlNode? pubDateNode = item.SelectSingleNode("pubDate");
                string pubDate = (pubDateNode != null) ? pubDateNode.InnerText : "";
                string formattedDate = DateTime.Parse(pubDate).ToString("dddd, MMMM dd, yyyy");

                XmlNode? linkNode = item.SelectSingleNode("link");
                string link = (linkNode != null) ? linkNode.InnerText : "";

                RssItem rssItem = new RssItem
                {
                    Title = title,
                    Description = des,
                    PubDate = formattedDate,
                    Link = link
                };

                itemsList.Add(rssItem);
            }

            rssItemsList = itemsList;
        }
    }
}


public class RssItem
{
    public string Title { get; set; }
    public HtmlString Description { get; set; }
    public string PubDate { get; set; }
    public string Link { get; set; }
}