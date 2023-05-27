using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
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

        while (reader.Read())
        {
            string k = reader.GetString(0);
            string g = reader.GetString(1);
            string insert = $"INSERT OR IGNORE INTO unknown (kanji, grp) VALUES (\"{k}\", \"{g}\")";
            command = new SQLiteCommand(insert, kanjidatabase);
            command.ExecuteNonQuery();
        }
        kanjidatabase.Close();
    }
    
    
    /// <summary>
    /// Reads kanji and stores them in a database for both
    /// jinmeiyou and jouyou kanji. Reads an Anki database
    /// located in working directory and stores distinct
    /// kanji in the same database as those read from the
    /// web. Finally finds out, which jinmeiyou/jouyou kanji
    /// are not found in the Anki database and lists them
    /// in the database as unknown kanji.
    /// </summary>
    public static void Main()
    {
        Console.Write("Give Anki database location kudasai $");
        StringBuilder data = new StringBuilder();
        string collectionPath = Console.ReadLine();
        
        try
        {
            data = AnkiReader.ReadDatabase(collectionPath);
        }
        catch (FileNotFoundException e)
        {
            Console.WriteLine(e.Message);
        }
        catch (SQLiteException e)
        {
            Console.WriteLine("Failed to read database file: " + collectionPath);
        }

        // no need to continue if there's no data
        if (data.Equals("")) return;
        
        List<HtmlNode> kanjiList = Webreader.ReadPage("https://en.wikipedia.org/api/rest_v1/page/html/List_of_j%C5%8Dy%C5%8D_kanji");
        List<char> kanji = Webreader.ExtractKanji(kanjiList);
        StoreKanji(kanji, "jouyou");
        kanjiList = Webreader.ReadPageJinmeiyou("https://en.wikipedia.org/api/rest_v1/page/html/Jinmeiyō_kanji");
        kanji = Webreader.ExtractKanji(kanjiList);
        StoreKanji(kanji, "jinmeiyou");
        
        AnkiReader.InsertAnkiData("kanjidatabase", data);
        
        FindUnknownKanji();
    }
}