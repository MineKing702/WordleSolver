using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WordleSolver.Models
{
    internal class Game
    {
        public List<GuessResult> guesses;

        public int numOfGuesses = 0;

        public void AddGuess(GuessResult result)
        {
            guesses.Add(result);
            numOfGuesses++:
        }
    }
}
