using TMPro;
using UnityEngine;

namespace AngerRoom.Rewards
{
    // One component per minigame. Text is still written/localized by its usual
    // owner; this component changes appearance only, never the string or activity.
    public sealed class MinigameTutorialGlow : MonoBehaviour
    {
        [SerializeField] private Card_Minigame cards;
        [SerializeField] private GunPuzzle shooting;
        [SerializeField] private LabyrinthScript labyrinth;
        [SerializeField] private TMP_Text[] labels;
        [Tooltip("Glow-enabled copies of each label's material, included in builds. Never edit shared font assets at runtime.")]
        [SerializeField] private Material[] glowMaterials;
        [Header("Tutorial gold pulse (editable during Play)")]
        [SerializeField] private bool highlightEnabled = true;
        [SerializeField] private Color gold = new Color(1f, .72f, .12f, 1f);
        [SerializeField, Min(.1f)] private float pulsePeriod = 2f;
        [SerializeField, Range(0f, 1f)] private float minimumGoldBlend = .3f;
        [SerializeField, Range(0f, 1f)] private float maximumGlow = .65f;
        private Material[] originals, instances;
        private Color[] originalColors;
        private bool highlighting;
        private float phase;
        private static readonly int GlowColor = Shader.PropertyToID("_GlowColor");

        private bool TutorialActive => (cards != null && cards.IsTutorialActive) ||
            (shooting != null && shooting.GetGunTutorial()) || (labyrinth != null && labyrinth.IsTutorialActive);

        private void LateUpdate()
        {
            bool show = highlightEnabled && TutorialActive;
            if (!show) { Restore(); return; }
            if (!highlighting) BeginHighlight();
            // Smooth and slow: no flashing or abrupt on/off transitions. Use real
            // time so the attention cue remains visible if gameplay time pauses.
            phase += Time.unscaledDeltaTime / Mathf.Max(.1f, pulsePeriod);
            phase = Mathf.Repeat(phase, 1f);
            ApplyPulse(.5f - .5f * Mathf.Cos(phase * Mathf.PI * 2f));
        }

        private void BeginHighlight()
        {
            originals = new Material[labels.Length];
            originalColors = new Color[labels.Length];
            if (instances == null) instances = new Material[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                originals[i] = labels[i].fontSharedMaterial;
                originalColors[i] = labels[i].color;
                if (instances[i] == null && glowMaterials[i] != null)
                    instances[i] = new Material(glowMaterials[i]);
                if (instances[i] != null) labels[i].fontSharedMaterial = instances[i];
                labels[i].UpdateMeshPadding();
            }
            phase = 0;
            highlighting = true;
        }

        private void ApplyPulse(float pulse)
        {
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                Color tint = Color.Lerp(originalColors[i], gold, Mathf.Lerp(minimumGoldBlend, 1f, pulse));
                tint.a = originalColors[i].a; // Keep text readable throughout the pulse.
                labels[i].color = tint;
                if (instances[i] != null)
                {
                    Color halo = gold;
                    halo.a = Mathf.Lerp(.1f, 1f, pulse) * maximumGlow;
                    instances[i].SetColor(GlowColor, halo);
                }
            }
        }

        private void Restore()
        {
            if (!highlighting) return;
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                labels[i].fontSharedMaterial = originals[i];
                labels[i].color = originalColors[i];
                labels[i].UpdateMeshPadding();
            }
            highlighting = false;
        }
        private void OnDisable() => Restore();
        private void OnDestroy()
        {
            Restore();
            if (instances == null) return;
            foreach (var material in instances) if (material != null) Destroy(material);
        }
    }
}
