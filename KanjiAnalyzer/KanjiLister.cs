using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Transactions;
using HtmlAgilityPack;

namespace KanjiAnalyzer;

public class KanjiLister
{
    /// <summary>
    /// Store a group of kanji in an SQLite database.
    /// The kanji will be added into 'kanji' table with fields
    /// for the character and the group it belongs to.
    /// The database file is 'kanjidatabase' in the working
    /// directory and will be created if it doesn't exist.
    /// Only distinct kanji will be added, duplicates are ignored.
    /// </summary>
    /// <param name="kanjiChars">A list of characters to add</param>
    /// <param name="kanjiGroup">The kanji group, e.g. jouyou or jinmeiyou</param>
    public static void StoreKanji(List<char> kanjiChars, string kanjiGroup)
    {
        if (!File.Exists("kanjidatabase")) SQLiteConnection.CreateFile("kanjidatabase");

        SQLiteConnection kanjidatabase = new SQLiteConnection("Data Source=kanjidatabase;Version=3");
        kanjidatabase.Open();
        string cmdText = "CREATE TABLE IF NOT EXISTS kanji (id INTEGER PRIMARY KEY, kanjichar TEXT NOT NULL UNIQUE, grp TEXT NOT NULL);";
        SQLiteCommand command = new SQLiteCommand(cmdText, kanjidatabase);
        command.ExecuteNonQuery();
        
        cmdText = "INSERT OR IGNORE INTO kanji (kanjichar, grp) VALUES (@kanji, @group);";
        command = new SQLiteCommand(cmdText, kanjidatabase);
        
        SQLiteParameter kanjiParameter = command.Parameters.Add("@kanji", DbType.String);
        SQLiteParameter groupParameter = command.Parameters.Add("@group", DbType.String);
        groupParameter.Value = kanjiGroup;

        using (TransactionScope transaction = new TransactionScope())
        {
            foreach (char k in kanjiChars)
            {
                kanjiParameter.Value = k;
                command.ExecuteNonQuery();
            }
            transaction.Complete();
        }

        kanjidatabase.Close();
    }


    /// <summary>
    /// Connect to kanjidatabase and find kanji that are unknown,
    /// i.e. not in the data extracted from the anki deck.
    /// Create a new table for the unknown kanji, including the
    /// kanji and the group it belongs to as fields.
    /// TODO: rethink to reduce database redundancy 
    /// </summary>
    public static void FindUnknownKanji()
    {
        SQLiteConnection kanjidatabase = new SQLiteConnection("Data Source=kanjidatabase;Version=3");
        kanjidatabase.Open();

        string cmd =
            "CREATE TABLE IF NOT EXISTS unknown (id INTEGER PRIMARY KEY, kanji TEXT NOT NULL UNIQUE, grp TEXT NOT NULL);";
        SQLiteCommand command = new SQLiteCommand(cmd, kanjidatabase);
        command.ExecuteNonQuery();
        
        string select = "SELECT kanjichar, grp FROM kanji WHERE kanjichar NOT IN (SELECT character FROM ankidata);";
        command = new SQLiteCommand(select, kanjidatabase);
        SQLiteDataReader reader = command.ExecuteReader();

        SQLiteCommand insert = new SQLiteCommand("INSERT OR IGNORE INTO unknown (kanji, grp) VALUES (@kanji, @group)", kanjidatabase);
        SQLiteParameter kanjiParameter = insert.Parameters.Add("@kanji", DbType.String);
        SQLiteParameter groupParameter = insert.Parameters.Add("@group", DbType.String);
        
        while (reader.Read())
        {
            string k = reader.GetString(0);
            string g = reader.GetString(1);
            kanjiParameter.Value = k;
            groupParameter.Value = g;
            insert.ExecuteNonQuery();
        }
        kanjidatabase.Close();
    }


    /// <summary>
    /// Queries the database for unknown kanji and returns
    /// the results as a list of ResultsGroup objects. Each
    /// ResultsGroup knows the name of the kanji group it
    /// contains plus the unknown kanji in it
    /// </summary>
    /// <param name="databaseFile">SQLite database to query from</param>
    /// <returns>A list of ResultsGroup objects</returns>
    public static List<ResultsGroup<char>> FetchUnknown(string databaseFile)
    {
        SQLiteConnection db = new SQLiteConnection($"Data Source={databaseFile};Version=3");
        db.Open();

        var results = new List<ResultsGroup<char>>();
        string[] groups = { "jouyou", "jinmeiyou" }; // hardcoded everywhere for now, might change if I decide to add hyougaiji or whatever
        SQLiteCommand getResults = new SQLiteCommand("SELECT kanji FROM unknown WHERE grp = @grp;", db);
        SQLiteParameter groupParameter = getResults.Parameters.Add("@grp", DbType.String);

        foreach (string group in groups)
        {
            results.Add(new ResultsGroup<char>(group));
            groupParameter.Value = group;
            ResultsGroup<char> rg = results.Find(g => g.getName() == group);
            SQLiteDataReader reader = getResults.ExecuteReader();
            while (reader.Read())
            {
                char c = reader.GetString(0)[0];
                rg.addItem(c);
            }
            reader.Close();
        }
        
        db.Close();
        return results;
    }


    /// <summary>
    /// Prints character counts in each ResultsGroup, and
    /// all character in a group should the user so desire
    /// (better have UTF8 enabled in your terminal for this one)
    /// </summary>
    /// <param name="unknownKanji">A list of ResultsGroup objects</param>
    public static void PrintResults(List<ResultsGroup<char>> unknownKanji)
    {
        foreach (ResultsGroup<char> group in unknownKanji)
        {
            string groupName = group.getName();
            int groupCount = group.getCount();
            Console.WriteLine($"{groupCount} unknown kanji in {groupName} group");
            if (groupCount > 0) // no need to ask for printing if there's nothing to print
                Console.Write($"Print all unknown {groupName} kanji? y/N $ ");
            string choice = Console.ReadLine();
            // only accepts Y and y for now, a yes won't fly
            if (choice.ToLower() == "y")
            {
                // set the separator as a null character to avoid starting with whitespace
                char separator = '\0';
                foreach (char kanji in group)
                {
                    Console.Write(separator);
                    Console.Write(kanji);
                    separator = ' ';
                }
                Console.WriteLine();
            }
        }
    }
    
    
    /// <summary>
    /// Reads an Anki collection specified by the user and stores distinct
    /// kanji in a new SQLite database. Reads jouyou and jinmeiyou kanji
    /// from the web and stores them in the aforementioned database. Finally
    /// finds out which jinmeiyou/jouyou kanji are not found in the Anki
    /// database (card fronts only) and lists them in the database as unknown kanji.
    /// </summary>
    public static void Main()
    {
        Console.Write("Extract Anki collection onegai and give .colpkg file location kudasai $ ");
        string collectionPath = Console.ReadLine();
        
        // read data and insert it into a database
        var data = AnkiReader.ReadCollection(collectionPath);
        // no need to continue if there's no data
        if (data.Equals("")) return;
        AnkiReader.InsertAnkiData("kanjidatabase", data);
        
        // fetch kanji by group and insert said groups into the database  
        List<HtmlNode> kanjiList = Webreader.ReadPage("https://en.wikipedia.org/api/rest_v1/page/html/List_of_j%C5%8Dy%C5%8D_kanji");
        List<char> kanji = Webreader.ExtractKanji(kanjiList);
        StoreKanji(kanji, "jouyou");
        kanjiList = Webreader.ReadPageJinmeiyou("https://en.wikipedia.org/api/rest_v1/page/html/Jinmeiyō_kanji");
        kanji = Webreader.ExtractKanji(kanjiList);
        StoreKanji(kanji, "jinmeiyou");
        
        // find out which kanji are unknown and show results
        FindUnknownKanji();
        var unknownKanji = FetchUnknown("kanjidatabase");
        PrintResults(unknownKanji);
    }
}