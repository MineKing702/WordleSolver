
using System.Collections.Generic;

namespace WordleSolver.Strategies;

/// <summary>
/// Example solver that simply iterates through a fixed list of words.
/// Students will replace this with a smarter algorithm.
/// </summary>
public sealed class AiStudentSolver : IWordleSolverStrategy
{
    private static readonly string WordListPath = Path.Combine("data", "wordle.txt");
    private static readonly List<string> WordList = LoadWordList();

    private List<string> _remainingWords = new();
    private Dictionary<char, int> _letterFrequencies = new();

    private static List<string> LoadWordList()
    {
        return File.ReadAllLines(WordListPath)
                   .Select(w => w.Trim().ToLowerInvariant())
                   .Where(w => w.Length == 5)
                   .Distinct()
                   .ToList();
    }

    public void Reset()
    {
        _remainingWords = new List<string>(WordList);
        _letterFrequencies = CalculateLetterFrequencies(_remainingWords);
    }

    public string PickNextGuess(GuessResult previousResult)
    {
        if (!previousResult.IsValid)
            throw new InvalidOperationException("Previous guess was invalid.");

        // First guess — don't filter
        if (previousResult.Guesses.Count > 0)
        {
            FilterRemainingWords(previousResult);
            _letterFrequencies = CalculateLetterFrequencies(_remainingWords);
        }

        return ChooseBestRemainingWord(previousResult);
    }

    public string ChooseBestRemainingWord(GuessResult previousResult)
    {
        if (_remainingWords.Count == 0)
            throw new InvalidOperationException("No remaining words to choose from");

        // Score by sum of letter frequency (for distinct letters only)
        return _remainingWords
            .OrderByDescending(w => w.Distinct().Sum(c => _letterFrequencies.GetValueOrDefault(c, 0)))
            .First();
    }

    private void FilterRemainingWords(GuessResult result)
    {
        string guess = result.Word;
        var statuses = result.LetterStatuses;

        _remainingWords = _remainingWords
            .Where(word => IsCandidateValid(word, guess, statuses))
            .ToList();
    }

    private bool IsCandidateValid(string candidate, string guess, LetterStatus[] status)
    {
        char[] wc = candidate.ToCharArray();
        char[] gc = guess.ToCharArray();

        var requiredCounts = new Dictionary<char, int>();
        var forbiddenPositions = new Dictionary<char, List<int>>();

        // First pass: handle Correct and Misplaced
        for (int i = 0; i < 5; i++)
        {
            if (status[i] == LetterStatus.Correct)
            {
                if (gc[i] != wc[i]) return false;

                if (!requiredCounts.ContainsKey(gc[i]))
                    requiredCounts[gc[i]] = 0;
                requiredCounts[gc[i]]++;
            }
            else if (status[i] == LetterStatus.Misplaced)
            {
                if (gc[i] == wc[i]) return false;

                if (!requiredCounts.ContainsKey(gc[i]))
                    requiredCounts[gc[i]] = 0;
                requiredCounts[gc[i]]++;

                if (!forbiddenPositions.ContainsKey(gc[i]))
                    forbiddenPositions[gc[i]] = new();
                forbiddenPositions[gc[i]].Add(i);
            }
        }

        // Check if required letter counts are met
        foreach (var kvp in requiredCounts)
        {
            if (candidate.Count(c => c == kvp.Key) < kvp.Value)
                return false;
        }

        // Check forbidden positions
        foreach (var kvp in forbiddenPositions)
        {
            foreach (int pos in kvp.Value)
            {
                if (wc[pos] == kvp.Key)
                    return false;
            }
        }

        // Handle Unused — allow only if letter is already required
        for (int i = 0; i < 5; i++)
        {
            if (status[i] == LetterStatus.Unused)
            {
                int allowedCount = CountLetterInStatus(gc[i], gc, status, LetterStatus.Correct, LetterStatus.Misplaced);
                int actualCount = candidate.Count(c => c == gc[i]);
                if (actualCount > allowedCount)
                    return false;
            }
        }

        return true;
    }

    private int CountLetterInStatus(char letter, char[] guess, LetterStatus[] status, params LetterStatus[] include)
    {
        int count = 0;
        for (int i = 0; i < 5; i++)
        {
            if (guess[i] == letter && include.Contains(status[i]))
                count++;
        }
        return count;
    }

    private Dictionary<char, int> CalculateLetterFrequencies(IEnumerable<string> words)
    {
        var freq = new Dictionary<char, int>();
        foreach (var word in words)
        {
            foreach (var c in word.Distinct())
            {
                if (!freq.ContainsKey(c)) freq[c] = 0;
                freq[c]++;
            }
        }
        return freq;
    }
}