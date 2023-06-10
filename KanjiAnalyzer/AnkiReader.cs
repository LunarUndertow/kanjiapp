using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace KanjiAnalyzer;

public class AnkiReader
{
    // regex for matching to Chinese/Japanese/Korean unified ideographs unicode block
    // TODO: might need more unicode blocks to include everything, but seems to give decent results already
    private static readonly Regex CjkRegex = new Regex(@"\p{IsCJKUnifiedIdeographs}");

    /// <summary>
    /// Unzips the collection to a temporary directory, reads the data to
    /// a StringBuilder, deletes temporary files and returns the StringBuilder.
    /// The data structure might change to something more sensible than a
    /// StringBuilder, but it works for now, as we're only interested in
    /// distinct individual characters. For now assumes the data was extracted
    /// in legacy mode (no Zstd compression) and the file extension is anki21
    /// </summary>
    /// <param name="path">Path to read the collection file from</param>
    /// <returns>A StringBuilder containing the data</returns>
    public static StringBuilder ReadCollection(string path)
    {
        StringBuilder data = new StringBuilder(); // StringBuilder to append the data into
        
        // extract collection archive to a temporary directory
        string extractPath = @".\extractTemp";
        try
        {
            ZipFile.ExtractToDirectory(path, extractPath);
        }
        catch (FileNotFoundException e)
        {
            Console.WriteLine(e.Message);
        }
        catch (InvalidDataException e)
        {
            Console.WriteLine(e.Message);
        }

        // read the SQLite database from the extracted archive
        string dbFile = extractPath + @"\collection.anki21";
        try
        {
            data = ReadDatabase(dbFile);
        }
        catch (FileNotFoundException e)
        {
            Console.WriteLine(e.Message);
        }
        catch (SQLiteException e)
        {
            Console.WriteLine("Failed to read database file: " + path);
        }

        // the temporary directory might not exist if the user didn't enter a valid path for the collection
        if (Directory.Exists(@".\extractTemp"))
            Directory.Delete(@".\extractTemp", true);

        return data;
    }
    
    
    /// <summary>
    /// Reads an Anki database. Returns the read data as a
    /// StringBuilder. We're only interested in individual characters for
    /// now, so there's no need to preserve individual elements of the
    /// database. That might be subject to change according to future
    /// features - in such case refactor to e.g. read data into a list
    /// of Strings.
    /// </summary>
    /// <param name="filePath">File to read.</param>
    /// <returns>Stringbuilder containing data from slfd table in notes field</returns>
    public static StringBuilder ReadDatabase(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("File does not exist: " + filePath);
        StringBuilder data = new StringBuilder("");
        using (SQLiteConnection anki = new SQLiteConnection($"Data Source={filePath};Version=3"))
        {
            anki.Open();
            String select = "SELECT sfld FROM notes;"; // field 'sfld' in table 'notes' is the front side of the card
            using (SQLiteCommand command = new SQLiteCommand(select, anki))
            {
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        String item = reader.GetString(0);
                        data.Append(item);
                    }
                }
            }
        }
        return data;
    }


    /// <summary>
    /// Insert distinct CJK characters into a table in an SQLite databse.
    /// Uses the isCjk method to determine whether to add a character,
    /// so in case of unexpected results refer to that and the cjkRegex.
    /// Added characters are defined as known. If a database already exists,
    /// only insert missing kanji and update the 'known' value as true for
    /// any kanji in Anki collection, so kanji previously marked as unknown
    /// get updated.
    /// </summary>
    /// <param name="sqlitedb">SQLite database file in current working directory. Will be created if does not exist.</param>
    /// <param name="ankidata">Character data to to insert.</param>
    /// <returns>True if database already exists, false if not</returns>
    public static bool InsertAnkiData(string sqlitedb, StringBuilder ankidata)
    {
        bool dbExists = File.Exists(sqlitedb);
        if (!dbExists) SQLiteConnection.CreateFile(sqlitedb);

        using (SQLiteConnection db = new SQLiteConnection($"Data Source={sqlitedb};Version=3;"))
        {
            db.Open();
            using (SQLiteCommand command = new SQLiteCommand(db), update = new SQLiteCommand(db))
            {
                command.CommandText =
                    "CREATE TABLE IF NOT EXISTS kanji (id INTEGER PRIMARY KEY, character TEXT NOT NULL UNIQUE, grp TEXT, known INTEGER)";
                command.ExecuteNonQuery();

                command.CommandText = "INSERT OR IGNORE INTO kanji (character) VALUES (@char);"; // use INSERT OR IGNORE and UPDATE in a separate command because
                update.CommandText = "UPDATE kanji SET known = 1 WHERE character = @char;";      // doing it in one command with INSERT OR REPLACE is abysmally slow
                SQLiteParameter kanjiInsertParameter = command.Parameters.Add("@char", DbType.String);
                SQLiteParameter kanjiUpdateParameter = update.Parameters.Add("@char", DbType.String);

                for (int i = 0; i < ankidata.Length; i++)
                {
                    char c = ankidata[i];
                    if (isCjk(c))
                    {
                        kanjiInsertParameter.Value = c;
                        kanjiUpdateParameter.Value = c;
                        command.ExecuteNonQuery();
                        update.ExecuteNonQuery();
                    }
                }
            }
        }

        return dbExists;
    }


    /// <summary>
    /// Checks if a character belongs to the CJK unified
    /// ideographs unicode block.
    /// </summary>
    /// <param name="c">A character to check</param>
    /// <returns>True if CJK, false otherwise</returns>
    public static bool isCjk(char c)
    {
        return CjkRegex.IsMatch(c.ToString());
    } 
}