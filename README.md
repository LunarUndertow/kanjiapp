# kanjiapp
A terminal application to find out which jouyou and jinmeiyou kanji your Anki collection is missing

Reads an exported Anki collection from the a path provided by the user, fetches jouyou and jinmeiyou kanji from Wikipedia, and creates an SQLite database with information on which kanji from the aforementioned groups are not included in the Anki cards' frontsides. Prints results in the console and in a file.

As for the code, I eschewed object oriented design for the most part when starting out, feeling that I might add needless convolution for little to no benefit, the scope of the program being as small as it is. Now that the main functionality has been implemented, I might get around to refactoring a bit.

### Usage of current version

To build, assuming you have dotnet SDK installed, run `dotnet build --configuration release`.

Export your Anki collection as a colpkg file somewhere. When running the program, it asks for the location of the collection file. Both relative and absolute paths should work. The program will tell you how many jouyou and jinmeiyou kanji your deck is still missing (only card fronts count), and print all individual unknown kanji for each group to console if you want it to. Please have UTF8 enabled in your terminal for the kanji to display properly. The stats and all unknown kanji will be printed to results.txt regardless of printing to console.

The program creates a 'kanjidatabase' file you can inspect with an SQLite database browser of your choice in the working directory of the program.