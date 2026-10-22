using System;
using System.Collections.Generic;
using UnityEngine;

namespace AngerRoom.Localization
{
    // After integration, this class lets you create a .asset file from Unity's
    // Assets > Create menu. The .asset holds editable translations, not game state.
    [CreateAssetMenu(fileName = "GameTexts", menuName = "Anger Room/Text Catalog")]
    public sealed class TextCatalog : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private TextId id;
            [TextArea(2, 6)] [SerializeField] private string english;
            [TextArea(2, 6)] [SerializeField] private string italian;
            [TextArea(2, 6)] [SerializeField] private string spanish;

            public TextId Id => id;

            public Entry(TextId id, string english, string italian, string spanish)
            {
                this.id = id;
                this.english = english;
                this.italian = italian;
                this.spanish = spanish;
            }

            public string Select(LanguageAdapter.Language language)
            {
                return language switch
                {
                    LanguageAdapter.Language.English => english,
                    LanguageAdapter.Language.Italian => italian,
                    LanguageAdapter.Language.Spanish => spanish,
                    _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported language.")
                };
            }
        }

        // Review examples, not final approved translations. These seed NEW assets.
        // Once an asset exists, edit its Inspector values: changing these defaults
        // does not replace values already serialized into that asset.
        [SerializeField] private List<Entry> entries = new List<Entry>
        {
            new Entry(TextId.Victory,
                "You won!", "Hai vinto!", "¡Has ganado!"),
            new Entry(TextId.LevelLabel,
                "Level", "Livello", "Nivel"),
            new Entry(TextId.RoundLabel,
                "Round", "Turno", "Ronda"),
            new Entry(TextId.TimerLabel,
                "Timer", "Tempo", "Reloj"),
            new Entry(TextId.TargetScoreLabel,
                "Target score", "Punteggio da raggiungere", "Puntuación a alcanzar"),
            new Entry(TextId.TutorialTimerLabel,
                "Tutorial timer", "Tempo del tutorial", "Reloj del tutorial"),
            new Entry(TextId.LevelProgress,
                "Level\n{0}/{1}", "Livello\n{0}/{1}", "Nivel\n{0}/{1}"),
            new Entry(TextId.RoundProgress,
                "Round\n{0}/{1}", "Turno\n{0}/{1}", "Ronda\n{0}/{1}"),
            new Entry(TextId.ScoreProgress,
                "Points\n{0}/{1}", "Punti\n{0}/{1}", "Puntos\n{0}/{1}"),
            new Entry(TextId.NextLevelCountdown,
                "Next level in...\n{0:00}", "Prossimo livello tra...\n{0:00}", "Siguiente nivel en...\n{0:00}"),
            new Entry(TextId.LabyrinthStatus,
                "Level: {0}\n{1:00}:{2:00}", "Livello: {0}\n{1:00}:{2:00}", "Nivel: {0}\n{1:00}:{2:00}"),
            new Entry(TextId.AttemptFailed,
                "You failed :(\nAttempts remaining: {0}",
                "Hai fallito :(\nTentativi rimasti: {0}",
                "Has fallado :(\nIntentos restantes: {0}"),
            new Entry(TextId.GameDisabled,
                "You failed!\nGame disabled.", "Hai fallito!\nGioco disattivato.", "Has fallado!\nJuego desactivado."),
            new Entry(TextId.Points,
                "{0}pts", "{0}pt", "{0}pts"),
            new Entry(TextId.ScoreboardEntry,
                "Participant {0}: {1}pts - #{2}",
                "Partecipante {0}: {1}pt - #{2}",
                "Participante {0}: {1}pts - #{2}")
        };

        // The Inspector stores a list. At startup we turn it into a dictionary
        // containing just the selected language. The asset itself is not modified.
        public Dictionary<TextId, string> CreateLookup(LanguageAdapter.Language language)
        {
            var lookup = new Dictionary<TextId, string>();

            if (entries == null)
            {
                throw new InvalidOperationException("The text catalog's entry list is missing.");
            }

            foreach (Entry entry in entries)
            {
                if (entry == null || entry.Id == TextId.None || !Enum.IsDefined(typeof(TextId), entry.Id))
                {
                    throw new InvalidOperationException("Every text catalog entry needs a valid TextId.");
                }

                if (lookup.ContainsKey(entry.Id))
                {
                    throw new InvalidOperationException($"Duplicate text catalog ID: {entry.Id}.");
                }

                lookup.Add(entry.Id, entry.Select(language));
            }

            return lookup;
        }
    }
}
