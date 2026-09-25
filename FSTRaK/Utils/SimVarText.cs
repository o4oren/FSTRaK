using System;
using System.Collections.Generic;
using System.Linq;

namespace FSTRaK.Utils
{
    /// <summary>
    /// Turns MSFS localisation keys into something a human can read.
    ///
    /// Several aircraft SimVars - ATC Type, ATC Model, ATC Airline - hand back the key
    /// rather than the resolved text for AI objects, because nothing resolves it on the
    /// way out of the simulator. The user's own aircraft usually escapes this, since
    /// FSTRaK reads its .air file, but traffic has no such fallback.
    ///
    /// The shapes vary and are not documented: "TT:ATCCOM.AC_MODEL_B738.0.text",
    /// "ATCCOM.ATC_NAME AIRBUS.0.TEXT" and the tail-less "ATCCOM_AC_MODEL_BEECHCRAFT"
    /// have all been observed. Rather than guess at which separator a given build uses,
    /// this treats '.', '_', ':' and space alike and discards the scaffolding tokens, which
    /// makes it indifferent to the shape - and to truncation.
    /// </summary>
    internal static class SimVarText
    {
        // ':' is in here so the "TT:" prefix separates from what follows it.
        private static readonly char[] Separators = { '.', '_', ' ', ':' };

        /// <summary>
        /// Tokens that are structure rather than content, wherever they appear.
        /// </summary>
        private static readonly string[] Scaffolding =
        {
            "TT", "ATCCOM", "AC", "MODEL", "ATC", "NAME", "AIRLINE"
        };

        /// <summary>
        /// The key's trailing marker, e.g. the "text" of ".0.text". "tts" is the
        /// text-to-speech variant and is just as common in practice.
        /// </summary>
        private static readonly string[] TrailingMarkers = { "TEXT", "TXT", "TTS" };

        /// <summary>
        /// Returns the readable part of a localisation key, or the input unchanged when it
        /// is not one. Never returns empty for a non-empty input: a value that reduces to
        /// nothing is handed back as it arrived, because an ugly label beats a blank one.
        /// </summary>
        public static string Humanize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var trimmed = value.Trim();

            if (!LooksLikeLocalisationKey(trimmed))
            {
                return trimmed;
            }

            var tokens = trimmed
                .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            DropTrailingMarker(tokens);

            var kept = tokens.Where(token => !IsScaffolding(token)).ToArray();

            if (kept.Length == 0)
            {
                return trimmed;
            }

            return string.Join(" ", kept);
        }

        /// <summary>
        /// Deliberately narrow: only strings carrying the simulator's own markers are
        /// rewritten, so a genuine name like "Boeing 737-800" is never taken apart.
        /// </summary>
        private static bool LooksLikeLocalisationKey(string value)
        {
            return value.IndexOf("ATCCOM", StringComparison.OrdinalIgnoreCase) >= 0
                   || value.StartsWith("TT:", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Removes a trailing "TEXT"/"TXT" marker and the index that precedes it. Done
        /// positionally rather than by discarding every bare number, so a numeric model
        /// code survives.
        /// </summary>
        private static void DropTrailingMarker(List<string> tokens)
        {
            if (tokens.Count == 0 ||
                !TrailingMarkers.Any(m => string.Equals(m, tokens[tokens.Count - 1], StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            tokens.RemoveAt(tokens.Count - 1);

            if (tokens.Count > 0 && tokens[tokens.Count - 1].All(char.IsDigit))
            {
                tokens.RemoveAt(tokens.Count - 1);
            }
        }

        private static bool IsScaffolding(string token)
        {
            return Scaffolding.Any(s => string.Equals(s, token, StringComparison.OrdinalIgnoreCase));
        }
    }
}
