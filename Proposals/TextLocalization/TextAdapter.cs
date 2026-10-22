using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AngerRoom.Localization
{
    // One shared component per session. Assign the existing LanguageAdapter and
    // a TextCatalog asset in the Inspector; every minigame references this instance.
    public sealed class TextAdapter : MonoBehaviour
    {
        [SerializeField] private LanguageAdapter languageAdapter;
        [SerializeField] private TextCatalog catalog;

        private Dictionary<TextId, string> sessionTexts;
        private LanguageAdapter.Language sessionLanguage;
        private CultureInfo sessionCulture;
        private readonly HashSet<string> reportedProblems = new HashSet<string>();

        public LanguageAdapter.Language SessionLanguage
        {
            get
            {
                EnsureInitialized();
                return sessionLanguage;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            // Once a session has started, even an Inspector language edit during
            // Play Mode must not silently switch this component's text selection.
            if (sessionTexts != null)
            {
                return;
            }

            if (languageAdapter == null || catalog == null)
            {
                throw new InvalidOperationException(
                    "Assign both LanguageAdapter and TextCatalog on the TextAdapter component.");
            }

            // Uses the EXISTING LanguageAdapter API, so reviewing this proposal
            // requires no replacement of the audio or language scripts.
            // Configure the language before entering Play Mode.
            sessionLanguage = languageAdapter.currentLanguage;
            sessionCulture = sessionLanguage switch
            {
                LanguageAdapter.Language.English => CultureInfo.GetCultureInfo("en-GB"),
                LanguageAdapter.Language.Italian => CultureInfo.GetCultureInfo("it-IT"),
                LanguageAdapter.Language.Spanish => CultureInfo.GetCultureInfo("es-ES"),
                _ => throw new InvalidOperationException($"Unsupported language: {sessionLanguage}.")
            };
            sessionTexts = catalog.CreateLookup(sessionLanguage);

            // Report incomplete selected-language content once at startup.
            foreach (TextId id in Enum.GetValues(typeof(TextId)))
            {
                if (id != TextId.None)
                {
                    Get(id);
                }
            }
        }

        public string Get(TextId id)
        {
            // Also supports a caller that runs before this component's Awake.
            // The configured LanguageAdapter field is already serialized then.
            EnsureInitialized();

            if (sessionTexts.TryGetValue(id, out string text) && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            return ReportProblem($"[Missing: {id}]", $"Missing {sessionLanguage} text for {id}.");
        }

        public string Format(TextId id, params object[] arguments)
        {
            string template = Get(id);

            try
            {
                // Placeholders belong to the translation. Values come from the
                // game, and culture controls their numeric formatting.
                return string.Format(sessionCulture, template, arguments);
            }
            catch (FormatException exception)
            {
                return ReportProblem($"[Invalid format: {id}]",
                    $"Invalid {sessionLanguage} format or arguments for {id}: {exception.Message}");
            }
        }

        private string ReportProblem(string marker, string message)
        {
            // A timer might request text frequently; log each problem only once.
            if (reportedProblems.Add(marker))
            {
                Debug.LogWarning(message, this);
            }

            return marker;
        }
    }
}
