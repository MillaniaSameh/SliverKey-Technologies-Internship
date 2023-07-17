using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Distributed;
using System.Xml;
using System.Text.Json;


namespace FavoriteFeedsWithHTMX.Pages;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDistributedCache _cache;
    private readonly ILogger<IndexModel> _logger;
    public List<RssItem> RssItemsList { get; private set; } = new();
    public int PageNumber { get; private set; } = 0;
    public int PageSize { get; private set; } = 5;
    public int TotalPages { get; private set; } = 0;
    public string PageName { get; private set; } = "Index";
    public bool MainPage { get; private set; } = true;

    public IndexModel(IHttpClientFactory httpClientFactory, IDistributedCache cache, ILogger<IndexModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task OnGetAsync(int pageNumber = 1, int pageSize = 5)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;

        if (MainPage)
            await LoadAllFeeds();
        else 
            await LoadFavoriteFeeds();
    }

    public async Task<IActionResult> OnGetUpdateContent()
    {
        var mainPageString = Request.Query["mainPage"];
        var mainPage = string.IsNullOrEmpty(mainPageString) ? true : bool.Parse(mainPageString);

        MainPage = mainPage;

        if (MainPage)
            await LoadAllFeeds();
        else 
            await LoadFavoriteFeeds();

        return Page();
    }

    public async Task LoadAllFeeds()
    {
        PageName = "Index";

        string? jsonItems = await _cache.GetStringAsync("itemsList");
        int counter = 0;

        if (string.IsNullOrEmpty(jsonItems))
        {
            var httpClient = _httpClientFactory.CreateClient();
            string OpmlUrl = "https://blue.feedland.org/opml?screenname=dave";

            HttpResponseMessage response = await httpClient.GetAsync(OpmlUrl);
            string xmlString = await response.Content.ReadAsStringAsync();

            XmlDocument document = new XmlDocument();
            document.LoadXml(xmlString);

            XmlElement? root = document.DocumentElement;
            XmlNodeList feedNodes = root.GetElementsByTagName("outline");
            List<RssItem> itemsList = new List<RssItem>();

            foreach (XmlNode feedNode in feedNodes)
            {
                string feedTitle = feedNode.Attributes["text"]?.Value ?? "";
                string feedUrl = feedNode.Attributes["xmlUrl"]?.Value ?? "";
                if (feedUrl == "") continue;

                HttpResponseMessage feedResponse = await httpClient.GetAsync(feedUrl);
                string feedXmlString = await feedResponse.Content.ReadAsStringAsync();

                XmlDocument feedDocument = new XmlDocument();
                feedDocument.LoadXml(feedXmlString);

                XmlElement? feedRoot = feedDocument.DocumentElement;
                XmlNodeList itemNodes = feedRoot.GetElementsByTagName("item");

                foreach (XmlNode itemNode in itemNodes)
                {
                    counter++;
                    RssItem RssItem = CreateRssItem(itemNode, counter, feedTitle);
                    itemsList.Add(RssItem);
                }
                break;
            }

            jsonItems = JsonSerializer.Serialize(itemsList);
            await _cache.SetStringAsync("itemsList", jsonItems, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            });
            RssItemsList = itemsList;
        }
        else
        {
            RssItemsList = JsonSerializer.Deserialize<List<RssItem>>(jsonItems);
        }

        // TotalPages = (int)Math.Ceiling(RssItemsList.Count / (double)PageSize);
        // int skip = (PageNumber - 1) * PageSize;
        // RssItemsList = RssItemsList.Skip(skip).Take(PageSize).ToList();
    }

    public async Task LoadFavoriteFeeds()
    {
        PageName = "FavoriteFeeds";

        string userFavoriteFeeds = HttpContext.Request.Cookies["favoriteFeeds"];

        if (!string.IsNullOrEmpty(userFavoriteFeeds) && userFavoriteFeeds != "[]")
        {
            int[] favoriteFeedsIds = JsonSerializer.Deserialize<int[]>(userFavoriteFeeds);

            string? jsonItems = await _cache.GetStringAsync("itemsList");
            List<RssItem> RssItems = new();

            if (!string.IsNullOrEmpty(jsonItems))
                RssItems = JsonSerializer.Deserialize<List<RssItem>>(jsonItems);

            RssItemsList = RssItems.Where(item => favoriteFeedsIds.Contains(item.Id)).ToList();

            // TotalPages = (int)Math.Ceiling(RssItemsList.Count / (double)PageSize);
            // int skip = (PageNumber - 1) * PageSize;
            // RssItemsList = RssItemsList.Skip(skip).Take(PageSize).ToList();
        }
        else
        {
            RssItemsList = null;
        }
    }

    public async Task<IActionResult> OnPostToggleFavorite(int id, string pageName)
    {
        // Update Cookie

        var userFavoriteFeeds = HttpContext.Request.Cookies["favoriteFeeds"];
        var favorites = new List<int>();

        if (!string.IsNullOrEmpty(userFavoriteFeeds))
            favorites = JsonSerializer.Deserialize<List<int>>(userFavoriteFeeds);

        if (!favorites.Contains(id))
            favorites.Add(id);
        else
            favorites.Remove(id);

        HttpContext.Response.Cookies.Append("favoriteFeeds", JsonSerializer.Serialize(favorites), new CookieOptions
        {
            Path = "/",
            IsEssential = true,
        });

        // Update Favorite Feed

        string? jsonItems = await _cache.GetStringAsync("itemsList");

        if (!string.IsNullOrEmpty(jsonItems))
        {
            List<RssItem> tempRssItemsList = JsonSerializer.Deserialize<List<RssItem>>(jsonItems);
            RssItem rssItem = tempRssItemsList.Find(item => item.Id == id);

            if (rssItem != null)
            {
                rssItem.IsFavorite = !rssItem.IsFavorite;
                jsonItems = JsonSerializer.Serialize(tempRssItemsList);
                await _cache.SetStringAsync("itemsList", jsonItems);
            }
        }

        // Redirect

        string refererUrl = Request.Headers["Referer"].ToString();

        return Redirect(refererUrl);
    }

    RssItem CreateRssItem(XmlNode itemNode, int counter, string feedTitle)
    {
        XmlNode? titleNode = itemNode.SelectSingleNode("title");
        string title = (titleNode != null) ? $"- {titleNode.InnerText}" : "";

        XmlNode? descriptionNode = itemNode.SelectSingleNode("description");
        string description = (descriptionNode != null) ? descriptionNode.InnerText : "";
        HtmlString htmlDescription = new HtmlString(description);

        XmlNode? pubDateNode = itemNode.SelectSingleNode("pubDate");
        string pubDate = (pubDateNode != null) ? pubDateNode.InnerText : "";
        string formattedDate = DateTime.Parse(pubDate).ToString("dddd, MMMM dd, yyyy");

        XmlNode? linkNode = itemNode.SelectSingleNode("link");
        string link = (linkNode != null) ? linkNode.InnerText : "";

        RssItem rssItem = new RssItem
        {
            Id = counter,
            Title = title,
            Description = htmlDescription,
            PubDate = formattedDate,
            Link = link,
            FeedTitle = feedTitle,
            IsFavorite = false
        };

        return rssItem;
    }
}

public class RssItem
{
    public int Id { get; set; }
    public string Title { get; set; }
    public HtmlString Description { get; set; }
    public string PubDate { get; set; }
    public string Link { get; set; }
    public string FeedTitle { get; set; }
    public bool IsFavorite { get; set; }
}

public class Pagination
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class RssFeeds
{
    public List<RssItem> RssItemsList { get; set; } = new();
    public string PageName { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}