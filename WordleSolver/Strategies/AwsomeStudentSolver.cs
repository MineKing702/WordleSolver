using System;
using System.Collections.Generic;
using WordleSolver.Services;

namespace WordleSolver.Strategies;

/// <summary>
/// Step 1: create a dictionary that holds how many times each letter appears in the word list
/// Step 2: always start with the word crane
/// step 3: filter out words that dont have used letters
/// step 4: filter out words that dont have correct letters in the correct spot
/// step 5: filter out words that have misplaced letters in the same spot
/// step 6: filter out words that have unused letters again
/// step 7: guess a word from the filtered list based on letter frequency
/// </summary>
public sealed class AwsomeStudentSolver : IWordleSolverStrategy
{
    /// <summary>Absolute or relative path of the word-list file.</summary>
    private static readonly string WordListPath = Path.Combine("data", "wordle.txt");

    /// <summary>In-memory dictionary of valid five-letter words.</summary>
    private static readonly List<string> WordList = LoadWordList();

    private const int ExpectedSimulationGames = 2000;

    /// <summary>
    /// Letter scores used to break ties when reporting the best and worst solved games.
    /// </summary>
    private static readonly Dictionary<char, int> LetterScores = BuildLetterScores();

    /// <summary>
    /// Remaining words that can be chosen
    /// </summary>
    private List<string> _remainingWords = new();

    /// <summary>
    /// Each letter is given a value based on popularity
    /// </summary>
    public Dictionary<string, int> LetterPointVal = new();

    private readonly List<CompletedGame> _completedGames = new();
    private List<GuessResult> _currentGameGuesses = new();
    private GuessResult? _lastResult;
    private string? _pendingGuess;
    private int _pendingGuessNumber;
    private int _gameNumber = 0;
    private bool _currentGameSaved = false;
    private bool _reportPrinted = false;

    public AwsomeStudentSolver()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            FinalizeCurrentGameIfSolved();

            if (!_reportPrinted && _completedGames.Count > 0)
            {
                PrintBestAndWorstGames();
            }
        };
    }

    private sealed class CompletedGame
    {
        public int GameNumber { get; set; }
        public string Answer { get; set; } = string.Empty;
        public int GuessCount { get; set; }
        public int WordScore { get; set; }
        public List<GuessResult> Guesses { get; set; } = new();
    }

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
        FinalizeCurrentGameIfSolved();

        _gameNumber++;
        _currentGameGuesses = new List<GuessResult>();
        _currentGameSaved = false;
        _lastResult = null;
        _pendingGuess = null;
        _pendingGuessNumber = 0;

        // If using SLOW student strategy, we just reset the current index
        // to the first word to start the next guessing sequence
        _remainingWords = [.. WordList];  // Set _remainingWords to a copy of the full word list
        // fills the dictionary by finding how many times each letter appears in the word list
        if (LetterPointVal.Count == 0)
        {
            LetterPointVal = GetPointDict();
        }
    }

    Dictionary<string, int> GetPointDict()
    {
        Dictionary<string, int> dict = new();

        // loops through each word in the wordlist
        foreach (string word in WordList)
        {
            // turns the word into a character array and adds each letter to the dictionary when found
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

    private static Dictionary<char, int> BuildLetterScores()
    {
        string path = Path.Combine("data", "wordle.txt");

        return File.ReadAllLines(path)
            .Select(w => w.Trim().ToLowerInvariant())
            .Where(w => w.Length == 5)
            .SelectMany(w => w.Distinct())
            .GroupBy(c => c)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static int CalculateWordScore(string word)
    {
        return word
            .ToLowerInvariant()
            .Distinct()
            .Sum(letter => LetterScores.TryGetValue(letter, out int score) ? score : 0);
    }

    private void FinalizeCurrentGameIfSolved()
    {
        if (_currentGameSaved)
        {
            return;
        }

        if (_lastResult != null && _lastResult.IsCorrect)
        {
            SaveCompletedGame(_lastResult);
            return;
        }

        if (!string.IsNullOrEmpty(_pendingGuess) && _pendingGuessNumber < WordleService.MaxGuesses)
        {
            SaveInferredCompletedGame();
        }
    }

    private void SaveInferredCompletedGame()
    {
        string answer = _pendingGuess!;
        var guesses = new List<GuessResult>(_currentGameGuesses);
        var solvedResult = new GuessResult
        {
            Word = answer,
            IsCorrect = true,
            IsValid = true,
            GuessNumber = _pendingGuessNumber,
            GuessesRemaining = Math.Max(WordleService.MaxGuesses - _pendingGuessNumber, 0),
            LetterStatuses = Enumerable.Repeat(LetterStatus.Correct, answer.Length).ToArray()
        };

        guesses.Add(solvedResult);
        solvedResult.Guesses = guesses;
        SaveCompletedGame(solvedResult);
    }

    private void SaveCompletedGame(GuessResult solvedResult)
    {
        string answer = solvedResult.Word;

        _completedGames.Add(new CompletedGame
        {
            GameNumber = _gameNumber,
            Answer = answer,
            GuessCount = solvedResult.GuessNumber,
            WordScore = CalculateWordScore(answer),
            Guesses = solvedResult.Guesses.Count > 0
                ? new List<GuessResult>(solvedResult.Guesses)
                : new List<GuessResult>(_currentGameGuesses)
        });

        _currentGameSaved = true;

        if (_completedGames.Count == ExpectedSimulationGames)
        {
            PrintBestAndWorstGames();
        }
    }

    private void PrintBestAndWorstGames()
    {
        if (_completedGames.Count == 0)
        {
            Console.WriteLine("No solved games were recorded.");
            return;
        }

        _reportPrinted = true;

        var bestGame = _completedGames
            .OrderBy(g => g.GuessCount)
            .ThenByDescending(g => g.WordScore)
            .First();

        var worstGame = _completedGames
            .OrderByDescending(g => g.GuessCount)
            .ThenBy(g => g.WordScore)
            .First();

        PrintGameSummary("BEST GAME", bestGame);
        PrintGameSummary("WORST GAME", worstGame);
    }

    private static void PrintGameSummary(string title, CompletedGame game)
    {
        Console.WriteLine();
        Console.WriteLine(title);
        Console.WriteLine($"Game #{game.GameNumber}");
        Console.WriteLine($"Answer: {game.Answer}");
        Console.WriteLine($"Guesses: {game.GuessCount}");
        Console.WriteLine($"Word score: {game.WordScore}");
        Console.WriteLine();

        foreach (var guess in game.Guesses)
        {
            PrintColoredGuess(guess);
        }

        Console.ResetColor();
    }

    private static void PrintColoredGuess(GuessResult guess)
    {
        for (int i = 0; i < guess.Word.Length; i++)
        {
            Console.ForegroundColor = guess.LetterStatuses[i] switch
            {
                LetterStatus.Correct => ConsoleColor.Green,
                LetterStatus.Misplaced => ConsoleColor.Yellow,
                _ => ConsoleColor.Gray
            };

            Console.Write(char.ToUpperInvariant(guess.Word[i]));
        }

        Console.ResetColor();
        Console.WriteLine();
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
        if (!previousResult.IsValid)
            throw new InvalidOperationException("PickNextGuess shouldn't be called if previous result isn't valid");

        if (previousResult.GuessNumber > 0 && previousResult.IsValid)
        {
            _lastResult = previousResult;
            _currentGameGuesses = [.. previousResult.Guesses];
            _pendingGuess = null;
            _pendingGuessNumber = 0;

            if (previousResult.IsCorrect && !_currentGameSaved)
            {
                SaveCompletedGame(previousResult);
            }
        }

        // First guess
        if (previousResult.Guesses.Count == 0)
        {
            string firstWord = "crane";
            _remainingWords.Remove(firstWord);

            //Console.WriteLine();
            //Console.WriteLine();
            //Console.WriteLine();
            //Console.WriteLine(firstWord);

            RememberPendingGuess(firstWord, previousResult);
            return firstWord;
        }
        else
        {
            FilterWordsByUsedLetters(previousResult);
            FilterWordsByCorrectLetters(previousResult);
            FilterWordsByMisplacedLetters(previousResult);

            // Only filter by unused letters if no duplicate letters in the previous guess
            if (!WordHasDupes(previousResult.Word))
            {
                FilterWordsByUnusedLetters(previousResult);
            }
        }

        string choice = ChooseBestRemainingWord(previousResult);
        _remainingWords.Remove(choice);
        RememberPendingGuess(choice, previousResult);
        return choice;
    }

    private void RememberPendingGuess(string guess, GuessResult previousResult)
    {
        _pendingGuess = guess;
        _pendingGuessNumber = previousResult.GuessNumber + 1;
    }

    private void FilterWordsByUsedLetters(GuessResult previousResult)
    {
        var usedLetters = new HashSet<char>();
        for (int i = 0; i < 5; i++)
        {
            if (previousResult.LetterStatuses[i] != LetterStatus.Unused)
            {
                usedLetters.Add(previousResult.Word[i]);
            }
        }

        var filtered = new List<string>();
        foreach (var word in _remainingWords)
        {
            bool allUsedLettersPresent = true;
            foreach (var c in usedLetters)
            {
                if (!word.Contains(c))
                {
                    allUsedLettersPresent = false;
                    break;
                }
            }
            if (allUsedLettersPresent)
            {
                filtered.Add(word);
            }
        }
        _remainingWords = filtered;
    }

    private void FilterWordsByCorrectLetters(GuessResult previousResult)
    {
        var correctPositions = new List<(int index, char letter)>();
        for (int i = 0; i < 5; i++)
        {
            if (previousResult.LetterStatuses[i] == LetterStatus.Correct)
            {
                correctPositions.Add((i, previousResult.Word[i]));
            }
        }

        var filtered = new List<string>();
        foreach (var word in _remainingWords)
        {
            bool allCorrect = true;
            foreach (var cp in correctPositions)
            {
                if (word[cp.index] != cp.letter)
                {
                    allCorrect = false;
                    break;
                }
            }
            if (allCorrect)
            {
                filtered.Add(word);
            }
        }
        _remainingWords = filtered;
    }

    private void FilterWordsByMisplacedLetters(GuessResult previousResult)
    {
        var misplacedPositions = new List<(int index, char letter)>();
        for (int i = 0; i < 5; i++)
        {
            if (previousResult.LetterStatuses[i] == LetterStatus.Misplaced)
            {
                misplacedPositions.Add((i, previousResult.Word[i]));
            }
        }

        var filtered = new List<string>();
        foreach (var word in _remainingWords)
        {
            bool allMisplaced = true;
            foreach (var mp in misplacedPositions)
            {
                if (!word.Contains(mp.letter) || word[mp.index] == mp.letter)
                {
                    allMisplaced = false;
                    break;
                }
            }
            if (allMisplaced)
            {
                filtered.Add(word);
            }
        }
        _remainingWords = filtered;
    }

    private void FilterWordsByUnusedLetters(GuessResult previousResult)
    {
        var unusedLetters = new HashSet<char>();
        for (int i = 0; i < 5; i++)
        {
            if (previousResult.LetterStatuses[i] == LetterStatus.Unused)
            {
                unusedLetters.Add(previousResult.Word[i]);
            }
        }

        var filtered = new List<string>();
        foreach (var word in _remainingWords)
        {
            bool containsUnused = false;
            foreach (var c in unusedLetters)
            {
                if (word.Contains(c))
                {
                    containsUnused = true;
                    break;
                }
            }
            if (!containsUnused)
            {
                filtered.Add(word);
            }
        }
        _remainingWords = filtered;
    }

    private void FilterWordsWithDoubles()
    {
        for (int i = 0; i < _remainingWords.Count; i++)
        {
            if (!WordHasDupes(_remainingWords[i]))
            {
                _remainingWords.RemoveAt(i);
            }
        }
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

    // find the word in the given list that has the hightest letter frequency score
    string GetHighestWordScore(List<string> words)
    {
        string topWord = words.First();
        int topScore = 0;
        for (int word = 0; word < words.Count; word++)
        {
            char[] letters = words[word].ToCharArray();
            letters = letters.Distinct().ToArray();

            int score = 0;
            foreach (char letter in letters)
            {
                score += LetterPointVal[letter.ToString()];
            }

            if (score > topScore)
            {
                topWord = words[word];
                topScore = score;
            }
        }

        return topWord;
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

        // score each word based on popularity of their letters
        string topWord = GetHighestWordScore(_remainingWords);

        // Console.WriteLine(topWord);

        // return _remainingWords.First();
        return topWord;
    }
}