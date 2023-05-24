# kanjiapp
Find out which jouyou and jinmeiyou kanji your Anki collection is missing

Reads an exported Anki collection from the program's working directory, fetches jouyou and jinmeiyou kanji from Wikipedia, and creates an SQLite database with information on which kanji from the aforementioned groups are not included in the Anki cards' frontsides. For now the program only creates a database - the user has to inspect the result with another tool of their choice.

TODO: let the user determine where to read the Anki collection from
TODO: show information to the user
TODO: rethink database structure to reduce redundancy
TODO: optimize database queries and insertions to make them faster