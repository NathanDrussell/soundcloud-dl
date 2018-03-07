using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public class ArgumentHandler
{
    public static void displayHelpMessage()
    {
        string[] helpMessage =
        {
            "soundcloud-dl [OPTIONS] url",
            "",
            "URL Format: https://soundcloud.com/{ARTIST}/{SONG-NAME}",
            "Artist URL Format: https://soundcloud.com/{ARTIST}/tracks",
            "",
            "\t-a, --artist - Download all the tracks uploaded by a user",
            "\t-h, --help -  Display Help"
        };
        foreach (string s in helpMessage)
        {
            Console.WriteLine(s);
        }
    }
    public static void parseParams(string[] args)
    {
        if (args.Length > 1)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-a" || args[i] == "--artist")
                {
                    try
                    {
                        if (ArgumentHandler.isValidArtistLink(args[i + 1]))
                        {
                            Program.getUserIds(args[i + 1]);
                        }
                    }
                    catch
                    {
                        ArgumentHandler.displayError("Invalid URL");
                    }
                }
            }
        }
        else if (args.Length == 1)
        {
            if (ArgumentHandler.isValidUrl(args[0]))
            {
                Program.getIdFromUrl(args[0]);
            }
            else if (args[0] == "-h" || args[0] == "--help")
            {
                displayHelpMessage();
            }
        }
        else
        {
            displayHelpMessage();
        }
    }
    public static bool isValidUrl(string url)
    {
        string[] urlElement = url.Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

        if (urlElement[0].ToLower() != "https:")
            return false;
        if (urlElement[1].ToLower() != "soundcloud.com")
            return false;

        return true;
    }
    public static bool isValidArtistLink(string url)
    {
        string[] urlElement = url.Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        if (urlElement[0].ToLower() != "https:")
            return false;
        if (urlElement[1].ToLower() != "soundcloud.com")
            return false;
        if (urlElement[2].ToLower() == "tracks")
            return false;
        try
        {
            if (urlElement[3].ToLower() != "tracks")
            {
                displayError("Invalid URL");
                return false;
            }
        }
        catch
        {
            displayError("Invalid Link");
            return false;
        }

        return true;
    }
    public static void displayError(string errorMessage)
    {
        Console.WriteLine(errorMessage);
    }
}

class Program
{
    #region Global Data
    static List<KeyValuePair<string, int>> downloadIds = new List<KeyValuePair<string, int>>();
    static bool done = false;
    static bool asyncing = false;
    static WebBrowser wb = null;
    #endregion

    [STAThread]
    static void Main(string[] args)
    {
        
        ArgumentHandler.parseParams(args);
        
        foreach (KeyValuePair<string, int> s in downloadIds)
        {
            downloadFile(s.Value, s.Key);
            while (!done) Thread.Sleep(5000);
        }
    }

    #region Download an Artist
    public static void getUserIds(string inUrl)
    {
        wb = new WebBrowser();
        wb.Navigate(inUrl);
        while (wb.ReadyState != WebBrowserReadyState.Complete)
        {
            Application.DoEvents();
        }
        int userId = 0;
        int trackAmount = 0;
        HtmlElementCollection metaElements = wb.Document.GetElementsByTagName("meta");
        foreach (HtmlElement h in metaElements)
        {
            if (h.OuterHtml.Contains("sound_count"))
            {
                trackAmount = int.Parse(h.GetAttribute("content"));
            }
            else if (h.OuterHtml.Contains("users:"))
            {
                userId = int.Parse(h.GetAttribute("content").Remove(0, h.GetAttribute("content").LastIndexOf(":") + 1));
            }
        }

        getIdsFromArtist(userId, trackAmount);
        while (asyncing) ;
    }

    private static async void getIdsFromArtist(int userId, int numberOfTracks)
    {
        asyncing = true;
        string requestUrl = "https://api-v2.soundcloud.com/users/" + userId + "/tracks?representation=&client_id=3GtnQtvbxU1K5jhCPJcq2xyZ6xtctDIc&limit=" + numberOfTracks + "&offset=0&linked_partitioning=1&app_version=1519904138&app_locale=en";

        HttpClient httpClient = new HttpClient();

        HttpResponseMessage res = await httpClient.GetAsync(requestUrl);
        string jsonData = await res.Content.ReadAsStringAsync();
        int limit = 3;

        foreach (string oneItem in jsonData.Split('{'))
        {
            int id = 0;
            string title = "";
            foreach (string line in oneItem.Split(','))
            {
                if (line.StartsWith("\"id\"") && id == 0)
                {
                    string pass1 = line.Remove(0, 5);
                    id = int.Parse(pass1);
                    limit = 0;
                }

                if (line.StartsWith("\"title\""))
                {

                    string notValid = line.Remove(0, 8);
                    foreach (char c in notValid)
                    {
                        if (char.IsLetterOrDigit(c) || c == ' ')
                        {
                            title += c;
                        }
                    }
                }
            }
            if (id != 0 && title != "")
            {
                downloadIds.Add(new KeyValuePair<string, int>(title, id));
            }

        }
        asyncing = false;
    }
    #endregion

    #region Download from a URL
    public static void getIdFromUrl(string url)
    {
        int id = 0;
        string name = "";

        wb = new WebBrowser();
        wb.DocumentCompleted += (sender, e) => afterLoadComplete(sender, e, ref id, ref name);
        wb.ScriptErrorsSuppressed = true;
        wb.Navigate(url);

        while (wb.ReadyState != WebBrowserReadyState.Complete)
        {
            Application.DoEvents();
        }

        downloadIds.Add(new KeyValuePair<string, int>(name, id));
        wb = null;

    }
    #endregion

    private static async void downloadFile(int id, string name)
    {
        done = false;
        HttpClient client = new HttpClient();

        string url = "https://api.soundcloud.com/i1/tracks/" + id + "/streams?client_id=3GtnQtvbxU1K5jhCPJcq2xyZ6xtctDIc";
        HttpResponseMessage res = await client.GetAsync(url);

        byte[] resultData = await res.Content.ReadAsByteArrayAsync();
        string dataAsString = Encoding.ASCII.GetString(resultData);

        dataAsString = dataAsString.Replace("\\u0026", "&");
        string pass1 = dataAsString.Remove(0, dataAsString.IndexOf("\":\"") + 3);
        string pass2 = pass1.Remove(pass1.IndexOf("\",\""));

        using (WebClient wc = new WebClient())
        {
            Console.WriteLine("Downloading: " + name);
            wc.DownloadProgressChanged += (sender, e) => DownloadProgress(sender, e, name);
            wc.DownloadFileCompleted += (sender, e) => FileDownloadCompleted(sender, e, name);
            wc.DownloadFileAsync(new Uri(pass2), name + ".mp3");
        }

    }

    #region Event Handlers
    private static void afterLoadComplete(object sender, WebBrowserDocumentCompletedEventArgs e, ref int id, ref string name)
    {
        WebBrowser w = sender as WebBrowser;

        string html = w.Document.Body.InnerHtml;
        string title = w.DocumentTitle;
        string titleWithoughSoundcloud = title.Remove(title.IndexOf(" | "));

        string validChar = "";

        foreach (char c in titleWithoughSoundcloud)
        {
            if (char.IsLetterOrDigit(c) || c == ' ')
            {
                validChar += c;
            }
        }
        name = validChar;

        foreach (string line in html.Split('<'))
        {
            if (line.Contains("tracks"))
            {
                string pass1 = html.Remove(0, html.IndexOf("tracks"));
                string pass2 = pass1.Remove(pass1.IndexOf("&"));
                string pass3 = pass2.Remove(0, pass2.IndexOf("%") + 3);
                id = int.Parse(pass3);
            }
        }
    }
    private static void FileDownloadCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e, string name)
    {
        Console.WriteLine(name + " Completed");
        done = true;
    }
    private static void DownloadProgress(object sender, DownloadProgressChangedEventArgs e, string name)
    {
        double perc = Math.Round((float)e.BytesReceived / (float)e.TotalBytesToReceive, 2);
        Console.Title = "Downloading: " + name + " Progress: " + perc * 100 + "%";

    }
    #endregion
}