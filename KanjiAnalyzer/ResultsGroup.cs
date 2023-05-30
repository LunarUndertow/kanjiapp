using System.Collections;
using System.Collections.Generic;

namespace KanjiAnalyzer;

/// <summary>
/// A class for collecting kanji for further processing.
/// Implemented with generic type because nothing at the
/// moment requires defining we're handling characters
/// </summary>
/// <typeparam name="T">Generic type</typeparam>
public class ResultsGroup<T> : IEnumerable<T>
{
    private string _groupName; // the name of the kanji group
    private List<T> _items = new List<T>(); // a list of the individual kanji

    public ResultsGroup(string groupName) // the group gets named at creation
    {
        this._groupName = groupName;
    }


    public void addItem(T item)
    {
        _items.Add(item);
    }


    public string getName()
    {
        return this._groupName;
    }


    public int getCount()
    {
        return this._items.Count;
    }


    /// <summary>
    /// Expose an enumerator so other classes can foreach the items
    /// </summary>
    /// <returns>An enumerator</returns>
    public IEnumerator<T> GetEnumerator()
    {
        for (int i = getCount() - 1; i >= 0; i--)
        {
            yield return _items[i];
        }
    }


    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}