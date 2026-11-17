using UnityEngine;
using UnityEngine.UI;

namespace SeasonRush
{
    /// <summary>One row of the leaderboard table (rank, name, wins, points). Part of the leaderboard panel in the canvas.</summary>
    public class LeaderboardRow : MonoBehaviour
    {
        public Text rank, playerName, wins, points;
    }
}
