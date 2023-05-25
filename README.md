# kanjiapp
Find out which jouyou and jinmeiyou kanji your Anki collection is missing

Reads an exported Anki collection from the program's working directory, fetches jouyou and jinmeiyou kanji from Wikipedia, and creates an SQLite database with information on which kanji from the aforementioned groups are not included in the Anki cards' frontsides. For now the program only creates a database - the user has to inspect the result with another tool of their choice.

Usage of current version: export your Anki collection as a colpkg file (check 'support older Anki versions'), and extract the 'collection.anki21' file into the working directory of KanjiApp. Running KanjiAnalyzer results in a 'kanjidatabase' file you can inspect with an SQLite database browser of your choice.

TODO: let the user determine where to read the Anki collection from  
TODO: show information to the user  
TODO: rethink database structure to reduce redundancy  
TODO: optimize database queries and insertions to make them faster
TODO: deal with colpkg so the user doesn't have to
TODO: deal with Zstd archiving so the anki export doesn't have to be legacy mode