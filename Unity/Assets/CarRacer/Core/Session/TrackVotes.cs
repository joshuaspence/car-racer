using System.Collections.Generic;
using System.Text;
using CarRacer.Core.Net;

namespace CarRacer.Core.Session
{
    /// <summary>
    /// Counting the lobby's track votes (design doc §11). Guests vote; the host sees the count and
    /// still decides, so this only ever suggests. Votes for a track that doesn't exist (an older
    /// or newer catalog) are ignored.
    /// </summary>
    public static class TrackVotes
    {
        /// <summary>How many votes each of <paramref name="trackCount"/> tracks has.</summary>
        public static int[] Count(IEnumerable<RosterEntry> players, int trackCount)
        {
            var counts = new int[trackCount];
            foreach (var p in players)
            {
                if (p.TrackVote < trackCount)
                {
                    counts[p.TrackVote]++;
                }
            }

            return counts;
        }

        /// <summary>
        /// The track with the most votes, or <paramref name="current"/> when it is tied for the most
        /// or nobody has voted, so a tie never overrides the host's pick. Among other ties, the first
        /// in the catalog.
        /// </summary>
        public static int Leader(int[] counts, int current)
        {
            int best = 0;
            foreach (int c in counts)
            {
                best = c > best ? c : best;
            }

            if (best == 0 || (current >= 0 && current < counts.Length && counts[current] == best))
            {
                return current;
            }

            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == best)
                {
                    return i;
                }
            }

            return current;
        }

        /// <summary>The count as one short line, most votes first, e.g. "Oval 2 · Canyon 1"; empty with no votes.</summary>
        public static string Summary(int[] counts, IReadOnlyList<string> names)
        {
            var order = new List<int>();
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > 0)
                {
                    order.Add(i);
                }
            }

            // Stable: equal counts keep catalog order.
            order.Sort((a, b) => counts[b] != counts[a] ? counts[b].CompareTo(counts[a]) : a.CompareTo(b));
            var text = new StringBuilder();
            foreach (int i in order)
            {
                if (text.Length > 0)
                {
                    text.Append(" · ");
                }

                text.Append(names[i]).Append(' ').Append(counts[i]);
            }

            return text.ToString();
        }
    }
}
