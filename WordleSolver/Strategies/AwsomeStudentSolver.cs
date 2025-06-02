
using System.Collections.Generic;
using System.Net;

namespace WordleSolver.Strategies;

/// <summary>
/// Example solver that simply iterates through a fixed list of words.
/// Students will replace this with a smarter algorithm.
/// </summary>
public sealed class AwsomeStudentSolver : IWordleSolverStrategy
{
    /// <summary>Absolute or relative path of the word-list file.</summary>
    private static readonly string WordListPath = Path.Combine("data", "wordle.txt");

    /// <summary>In-memory dictionary of valid five-letter words.</summary>
    private static readonly List<string> WordList = LoadWordList();

    /// <summary>
    /// Remaining words that can be chosen
    /// </summary>
    private List<string> _remainingWords = new();

    public Dictionary<string, int> LetterPointVal = new();

    // TODO: ADD your own private variables that you might need

    /// <summary>
    /// Loads the dictionary from disk, filtering to distinct five-letter lowercase words.
    /// </summary>
    private static List<string> LoadWordList()
    {
        if (!File.Exists(WordListPath))
            throw new FileNotFoundException($"Word list not found at path: {WordListPath}");

        return File.ReadAllLines(WordListPath)
            .Select(w => w.Trim().ToLowerInvariant())
            .Where(w => w.Length == 5)
            .Distinct()
            .ToList();
    }

    /// <inheritdoc/>
    public void Reset()
    {
        // TODO: What should happen when a new game starts?

        // If using SLOW student strategy, we just reset the current index
        // to the first word to start the next guessing sequence
        _remainingWords = [.. WordList];  // Set _remainingWords to a copy of the full word list
        if (LetterPointVal.Count == 0)
        {
            LetterPointVal = GetPointDict();
        }
    }

    Dictionary<string, int> GetPointDict()
    {
        char[] letters = "abcdefghijklmnopqrstuvwxyz".ToCharArray();

        Dictionary<string, int> dict = new();

        foreach (string word in WordList)
        {
            char[] wordLetters = word.ToCharArray();
            for (int i = 0; i < wordLetters.Length; i++)
            {
                string letter = wordLetters[i].ToString();
                if (dict.ContainsKey(letter))
                {
                    dict[letter]++;
                }
                else
                {
                    dict.Add(letter, 1);
                }
            }
        }

        return dict;
    }

    /// <summary>
    /// Determines the next word to guess given feedback from the previous guess.
    /// </summary>
    /// <param name="previousResult">
    /// The <see cref="GuessResult"/> returned by the game engine for the last guess
    /// (or <see cref="GuessResult.Default"/> if this is the first turn).
    /// </param>
    /// <returns>A five-letter lowercase word.</returns>
    public string PickNextGuess(GuessResult previousResult)
    {
        // Analyze previousResult and remove any words from
        // _remainingWords that aren't possible

        if (!previousResult.IsValid)
            throw new InvalidOperationException("PickNextGuess shouldn't be called if previous result isn't valid");

        // Check if first guess
        if (previousResult.Guesses.Count == 0)
        {
            // TODO: Pick the best starting word from wordle.txt 
            // BE CAREFUL that the first word you pick is in that wordle.txt list or your
            // program won't work. Regular Wordle allows users to guess any five-letter
            // word from a much larger dictionary, but we restrict it to the words that
            // can actually be chosen by WordleService to make it easier on you.
            string firstWord = "crane";

            // Filter _remainingWords to remove any words that don't match the first word
            _remainingWords.Remove(firstWord);

            // Console.WriteLine(firstWord);

            return firstWord;
        }
        else
        {
            // TODO: Analyze the previousResult and reduce/filter _remainingWords based on the feedback
            // find the used letters
            List<string> usedLetters = new();
            char[] letters = previousResult.Word.ToCharArray();
            for (int i = 0; i < 5; i++)
            {
                if (previousResult.LetterStatuses[i] != LetterStatus.Unused)
                {
                    usedLetters.Add(letters[i].ToString());
                }
            }

            // filter out words that dont contain the used letters
            for (int word = 0; word < _remainingWords.Count; word++)
            {
                for (int letter = 0; letter < usedLetters.Count; letter++)
                {
                    if (!_remainingWords[word].Contains(usedLetters[letter]))
                    {
                        _remainingWords.RemoveAt(word);
                        word--;
                        break;
                    }
                }
            }

            // find all the correct letters and saves their index
            List<int> goodIndexes = new();
            List<string> PlacedLetters = new();
            for (int i = 0; i < 5; i++)
            {
                if (previousResult.LetterStatuses[i] == LetterStatus.Correct)
                {
                    PlacedLetters.Add(letters[i].ToString());
                    goodIndexes.Add(i);
                }
            }

            // checks if the word contains all the correct letters
            for (int word = 0; word < _remainingWords.Count; word++)
            {
                char[] wordChars = _remainingWords[word].ToCharArray();
                for (int goodIndex = 0; goodIndex < goodIndexes.Count; goodIndex++)
                {
                    int index = goodIndexes[goodIndex];
                    string letter = PlacedLetters[goodIndex];
                    char remainLetter = wordChars[index];

                    if (letter != remainLetter.ToString())
                    {
                        _remainingWords.RemoveAt(word);
                        word--;
                        break;
                    }
                }
            }

            // find all the correct letters and saves their index
            List<int> misplacedIndexes = new();
            List<string> misplacedLetters = new();
            for (int i = 0; i < 5; i++)
            {
                if (previousResult.LetterStatuses[i] == LetterStatus.Misplaced)
                {
                    misplacedLetters.Add(letters[i].ToString());
                    misplacedIndexes.Add(i);
                }
            }

            for (int word = 0; word < _remainingWords.Count; word++)
            {
                char[] wordChars = _remainingWords[word].ToCharArray();
                for (int misplacedIndex = 0; misplacedIndex < misplacedIndexes.Count; misplacedIndex++)
                {
                    int index = misplacedIndexes[misplacedIndex];
                    string letter = misplacedLetters[misplacedIndex];
                    char remainLetter = wordChars[index];

                    if (letter == remainLetter.ToString())
                    {
                        _remainingWords.RemoveAt(word);
                        word--;
                        break;
                    }
                }
            }

            if (!WordHasDupes(previousResult.Word))
            {

                // stop using words that have an unused letter
                List<string> unusedLetters = new();
                char[] previousChars = previousResult.Word.ToCharArray();
                for (int i = 0; i < 5; i++)
                {
                    if (previousResult.LetterStatuses[i] == LetterStatus.Unused)
                    {
                        unusedLetters.Add(previousChars[i].ToString());
                    }
                }

                for (int word = 0; word < _remainingWords.Count; word++)
                {
                    for (int i = 0; i < unusedLetters.Count; i++)
                    {
                        if (_remainingWords[word].Contains(unusedLetters[i]))
                        {
                            _remainingWords.RemoveAt(word);
                            word--;
                            break;
                        }
                    }
                }
            }
        }

        // Utilize the remaining words to choose the next guess
        string choice = ChooseBestRemainingWord(previousResult);
        _remainingWords.Remove(choice);

        return choice;
    }

    bool WordHasDupes(string word)
    {
        char[] letters = word.ToCharArray();

        for (int i = 0; i < letters.Length; i++)
        {
            for (int j = i + 1; j < letters.Length; j++)
            {
                if (letters[j] == letters[i])
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Pick the best of the remaining words according to some heuristic.
    /// For example, you might want to choose the word that has the most
    /// common letters found in the remaining words list
    /// </summary>
    /// <param name="previousResult"></param>
    /// <returns></returns>
    public string ChooseBestRemainingWord(GuessResult previousResult)
    {
        if (_remainingWords.Count == 0)
            throw new InvalidOperationException("No remaining words to choose from");

        // Obviously the first word is the best right?
        // Console.WriteLine(_remainingWords.First());
        string topWord = _remainingWords.First();
        int topScore = 0;
        for (int word = 0; word < _remainingWords.Count; word++)
        {
            char[] letters = _remainingWords[word].ToCharArray();
            letters = letters.Distinct().ToArray();

            int score = 0;
            foreach (char letter in letters)
            {
                score += LetterPointVal[letter.ToString()];
            }

            if (score > topScore)
            {
                topWord = _remainingWords[word];
                topScore = score;
            }
        }

        // return _remainingWords.First();
        return topWord;
    }
}