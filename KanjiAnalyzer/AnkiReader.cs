﻿using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace KanjiAnalyzer;

public class AnkiReader
{
    // regex for matching to Chinese/Japanese/Korean unified ideographs unicode block
    // TODO: might need more unicode blocks to include everything, but seems to give decent results already
    private static readonly Regex cjkRegex = new Regex(@"\p{IsCJKUnifiedIdeographs}");
    
    /// <summary>
    /// Reads an Anki database. Hardcoded to use collection.anki21 in the
    /// current working directory for now. Returns the read data as a
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
        SQLiteConnection anki = new SQLiteConnection($"Data Source={filePath};Version=3");
        anki.Open();
        // field 'sfld' in table 'notes' is the front side of the card 
        String select = "SELECT sfld FROM notes;";
        SQLiteCommand command = new SQLiteCommand(select, anki);
        SQLiteDataReader reader = command.ExecuteReader();

        StringBuilder data = new StringBuilder("");

        while (reader.Read())
        {
            String item = reader.GetString(0);
            data.Append(item);
        }
        anki.Close();
        return data;
    }


    /// <summary>
    /// Insert distinct CJK characters into a table
    /// in an SQLite databse. Uses the isCjk method to
    /// determine whether to add a character, so in case
    /// of unexpected results refer to that and the cjkRegex
    /// </summary>
    /// <param name="sqlitedb">SQLite database file in current working directory. Will be created if does not exist.</param>
    /// <param name="ankidata">Character data to to insert.</param>
    public static void InsertAnkiData(string sqlitedb, StringBuilder ankidata)
    {
        if (!File.Exists(sqlitedb)) SQLiteConnection.CreateFile(sqlitedb);
        
        SQLiteConnection db = new SQLiteConnection("Data Source=" + sqlitedb + ";Version=3;");
        db.Open();
        SQLiteCommand command = new SQLiteCommand(db);
        command.CommandText = "CREATE TABLE IF NOT EXISTS ankidata (id INTEGER PRIMARY KEY, character TEXT UNIQUE)";
        command.ExecuteNonQuery();

        command.CommandText = "INSERT OR IGNORE INTO ankidata (character) VALUES (@char);";
        SQLiteParameter parameter = command.Parameters.Add("@char", DbType.String);

        for (int i = 0; i < ankidata.Length; i++)
        {
            char c = ankidata[i];
            if (isCjk(c))
            {
                parameter.Value = c;
                command.ExecuteNonQuery();
            }
        }
        db.Close();
    }


    /// <summary>
    /// Checks if a character belongs to the CJK unified
    /// ideographs unicode block.
    /// </summary>
    /// <param name="c">A character to check</param>
    /// <returns>True if CJK, false otherwise</returns>
    public static bool isCjk(char c)
    {
        return cjkRegex.IsMatch(c.ToString());
    } 
}