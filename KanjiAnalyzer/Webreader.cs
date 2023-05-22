using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HtmlAgilityPack;

namespace KanjiAnalyzer;

public class Webreader
{
    /// <summary>
    /// Reads the list of jouyou kanji from Wikipedia and
    /// returns a list of HTML nodes including the said
    /// kanji. The method is tailored for a specific article
    /// so giving other URLs as the parameter is discouraged.
    /// </summary>
    /// <param name="url">The URL used to fetch the page</param>
    /// <returns>A list of HTML nodes including the kanji</returns>
    public static List<HtmlNode> ReadPage(String url)
    {
        HtmlDocument page = new HtmlDocument();
        HtmlWeb web = new HtmlWeb{AutoDetectEncoding = false, OverrideEncoding = Encoding.UTF8};
        web.UserAgent = "https://github.com/LunarUndertow/";
        page = web.Load(url);
        if (page.DocumentNode != null)
        {
            List<HtmlNode> kanjiList = page.DocumentNode.SelectNodes("//tr/td[2]/a[text()]").ToList();
            return kanjiList;
        }

        return null;
    }

    
    /// <summary>
    /// Read the article on jinmeiyou kanji and return a
    /// list of the nodes including said kanji. This is a
    /// separate method from ReadPage because the pages for
    /// jouyou and jinmeiyou are formatted very differently:
    /// in the forementioned the kanji are arranged in a table,
    /// in the latter in span elements. The method is tailored
    /// for reading a specific Wikipedia page, so calling it
    /// with another URL is discouraged. 
    /// </summary>
    /// <param name="url">The URL used to fetch the page</param>
    /// <returns>A list of HTML nodes including (not necessarily exclusively) the kanji</returns>
    public static List<HtmlNode> ReadPageJinmeiyou(String url)
    {
        HtmlDocument page = new HtmlDocument();
        HtmlWeb web = new HtmlWeb{AutoDetectEncoding = false, OverrideEncoding = Encoding.UTF8};
        web.UserAgent = "https://github.com/LunarUndertow/";
        page = web.Load(url);
        if (page.DocumentNode != null)
        {
            List<HtmlNode> kanjiList = page.DocumentNode.SelectNodes("//span[@lang='ja'] | //span[@lang='ja']/*").ToList();
            return kanjiList;
        }

        return null;
    }
    
    
    /// <summary>
    /// Extract kanji characters from a list of HTML nodes
    /// likely provided by ReadPage or ReadPageJinmeiyou
    /// methods. Uses AnkiReader.isCjk to determine if a
    /// character is kanji, so unexpected results might
    /// originate there. 
    /// </summary>
    /// <param name="nodeList">A list of HTML nodes</param>
    /// <returns>A list of characters</returns>
    public static List<char> ExtractKanji(List<HtmlNode> nodeList)
    {
        List<char> kanjiChars = new List<char>();
        foreach (HtmlNode n in nodeList)
        {
            foreach (char c in n.InnerHtml)
            {
                if (AnkiReader.isCjk(c)) kanjiChars.Add(c);
            }
        }

        return kanjiChars;
    }
}