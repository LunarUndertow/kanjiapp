using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Transactions;
using HtmlAgilityPack;

namespace KanjiAnalyzer;

public class KanjiLister
{
    /// <summary>
    /// Store a group of kanji in an SQLite database.
    /// The kanji will be added into 'kanji' table with fields
    /// for the character, the group it belongs to, and whether
    /// it's unknown. Any kanji not defined 'known' by InsertAnkiData
    /// is marked as unknown. The database file is 'kanjidatabase'
    /// in the working directory and will be created if it doesn't exist.
    /// Only distinct kanji will be added, duplicates are ignored.
    /// </summary>
    /// <param name="kanjiChars">A list of characters to add</param>
    /// <param name="kanjiGroup">The kanji group, e.g. jouyou or jinmeiyou</param>
    /// <param name="database">SQLite database file to store the kanji in</param>
    public static void StoreKanji(List<char> kanjiChars, string kanjiGroup, string database)
    {
        if (!File.Exists(database)) SQLiteConnection.CreateFile(database);

        using (SQLiteConnection kanjidatabase = new SQLiteConnection($"Data Source={database};Version=3"))
        {
            kanjidatabase.Open();
            string cmdText =
                "CREATE TABLE IF NOT EXISTS kanji (id INTEGER PRIMARY KEY, character TEXT NOT NULL UNIQUE, grp TEXT, known INTEGER);";
            using (SQLiteCommand command = new SQLiteCommand(cmdText, kanjidatabase))
                command.ExecuteNonQuery();

            cmdText = "INSERT OR IGNORE INTO kanji (character, grp) VALUES (@kanji, @group);";
            using (SQLiteCommand command = new SQLiteCommand(cmdText, kanjidatabase))
            {

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

                    string unknown = "UPDATE kanji SET known = 0 WHERE known IS NULL";
                    using (SQLiteCommand markUnknown = new SQLiteCommand(unknown, kanjidatabase))
                        markUnknown.ExecuteNonQuery();

                    transaction.Complete();
                }
            }
        }
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
        var results = new List<ResultsGroup<char>>();
        using (SQLiteConnection db = new SQLiteConnection($"Data Source={databaseFile};Version=3"))
        {
            db.Open();
            string[] groups = { "jouyou", "jinmeiyou" }; // hardcoded everywhere for now, might change if I decide to add hyougaiji or whatever
            
            using (SQLiteCommand getResults = new SQLiteCommand("SELECT character FROM kanji WHERE grp = @grp AND known = 0;", db))
            {
                SQLiteParameter groupParameter = getResults.Parameters.Add("@grp", DbType.String);

                foreach (string group in groups)
                {
                    results.Add(new ResultsGroup<char>(group));
                    groupParameter.Value = group;
                    ResultsGroup<char> rg = results.Find(g => g.getName() == group);
                    using (SQLiteDataReader reader = getResults.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            char c = reader.GetString(0)[0];
                            rg.addItem(c);
                        }
                    }
                }
            }
        }
        return results;
    }


    /// <summary>
    /// Prints character counts in each ResultsGroup and all
    /// characters in a group to file results.txt. Stats are also
    /// printed to console and the individual kanji too, should
    /// the user so desire (better have UTF8 enabled for this one)
    /// </summary>
    /// <param name="unknownKanji">A list of ResultsGroup objects</param>
    public static void PrintResults(List<ResultsGroup<char>> unknownKanji)
    {
        Console.WriteLine("Stats and any unknown kanji will be printed to results.txt");
        using (StreamWriter sw = new StreamWriter("results.txt", false))
        {
            foreach (ResultsGroup<char> group in unknownKanji)
            {
                string groupName = group.getName();
                int groupCount = group.getCount();
                string groupStats = $"{groupCount} unknown kanji in {groupName} group";
                Console.WriteLine(groupStats);
                sw.WriteLine(groupStats);
                if (groupCount <= 0) continue; // no need to go on if there's nothing to print
                
                Console.Write($"Print all unknown {groupName} kanji to console? y/N $ ");
                string choice = Console.ReadLine().ToLower(); // only accepts Y and y for now, a yes won't fly
                bool printSwitch = choice.Equals("y"); // convert user choice to bool; the compiler might do this anyway but I feel better
                                                       // about testing for a bool instead of string equality if we're going to do it a lot
                sw.WriteLine($"Unknown kanji in {groupName}:");
                
                char separator = '\0'; // start with a null character to avoid starting with a space
                foreach (char kanji in group)
                {
                    sw.Write(separator);
                    sw.Write(kanji);    // print each kanji to a file
                    if (printSwitch)    // only print to console if asked to
                    {
                        Console.Write(separator);
                        Console.Write(kanji);
                    }
                    separator = ' ';
                }
                if (printSwitch) Console.WriteLine(); // newline if printing to console, not needed otherwise
                for (int i = 0; i < 2; i++) // two newlines to the file so it's more readable and we get to loop a bit more
                    sw.WriteLine();
            }
        }
    }
    
    
    /// <summary>
    /// Reads an Anki collection specified by the user and stores distinct
    /// kanji in an SQLite database. If a database has not been created previously,
    /// reads jouyou and jinmeiyou kanji from the web and stores them in the database.
    /// Finally finds out which jinmeiyou/jouyou kanji are not found in the Anki
    /// database (card fronts only) and lists them in the database as unknown kanji.
    /// Prints results in the console and in a file.
    /// </summary>
    public static void Main()
    {
        Console.Write("Extract Anki collection onegai and give .colpkg file location kudasai $ ");
        string collectionPath = Console.ReadLine();

        string database = "kanjidatabase"; // database file to store the kanji in 
        
        // read data and insert it into a database
        var data = AnkiReader.ReadCollection(collectionPath);
        // no need to continue if there's no data
        if (data.Equals("")) return;
        bool update = AnkiReader.InsertAnkiData(database, data); // returns whether a database was found; if so, just updating
                                                                 // with new Anki data suffices, no need to fetch kanji
        if (!update) // if starting from scratch we need to fetch the kanji
        {
            // fetch kanji by group and insert said groups into the database  
            List<HtmlNode> kanjiList =
                Webreader.ReadPage("https://en.wikipedia.org/api/rest_v1/page/html/List_of_j%C5%8Dy%C5%8D_kanji");
            List<char> kanji = Webreader.ExtractKanji(kanjiList);
            StoreKanji(kanji, "jouyou", database);
            kanjiList = Webreader.ReadPageJinmeiyou("https://en.wikipedia.org/api/rest_v1/page/html/Jinmeiyō_kanji");
            kanji = Webreader.ExtractKanji(kanjiList);
            StoreKanji(kanji, "jinmeiyou", database);
        }

        // fetch unknown kanji and show results
        var unknownKanji = FetchUnknown(database);
        PrintResults(unknownKanji);
    }
}