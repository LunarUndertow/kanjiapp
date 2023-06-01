# kanjiapp
Find out which jouyou and jinmeiyou kanji your Anki collection is missing

Reads an exported Anki collection from the a path provided by the user, fetches jouyou and jinmeiyou kanji from Wikipedia, and creates an SQLite database with information on which kanji from the aforementioned groups are not included in the Anki cards' frontsides. For now the program only creates a database - the user has to inspect the result with another tool of their choice.

### Usage of current version

To build, assuming you have dotnet SDK installed, run `dotnet build --configuration release`.

Export your Anki collection as a colpkg file (check 'support older Anki versions') somewhere. When running the program, it asks for the location of the collection file. Both relative and absolute paths should work. The program will print out how many jouyou and jinmeiyou kanji your deck is still missing (only card fronts count), and all individual unknown kanji for each group if you want it to. Please have UTF8 enabled in your terminal for the kanji to display properly.

The program creates a 'kanjidatabase' file you can inspect with an SQLite database browser of your choice in the working directory of the program.

### Todos

TODO: maybe print the results to a file  
TODO: rethink database structure to reduce redundancy  
TODO: deal with Zstd archiving so the anki export doesn't have to be legacy mode  
TODO: if database already exists, only update it and don't fetch kanji from the web