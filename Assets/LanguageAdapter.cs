using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LanguageAdapter : MonoBehaviour
{

    public enum Language { Italian, Spanish, English }
    [Header("Language")]
    public Language currentLanguage = Language.English;

    // Optional helper properties to check modes as booleans in other scripts
    public bool Italian => currentLanguage == Language.Italian;
    public bool Spanish => currentLanguage == Language.Spanish;
    public bool English => currentLanguage == Language.English;

    [Header("Host Intros")]
    [SerializeField] private List<AudioClip> host_Intro_ITA;
    [SerializeField] private List<AudioClip> host_Intro_ES;
    [SerializeField] private List<AudioClip> host_Intro_ENG;

    [Header("Host Tutorials")]
    [SerializeField] private List<AudioClip> host_tutorials_ITA;
    [SerializeField] private List<AudioClip> host_tutorials_ES;
    [SerializeField] private List<AudioClip> host_tutorials_ENG;

    [Header("Host Time Insults")]
    [SerializeField] private List<AudioClip> host_Time_insults_ITA;
    [SerializeField] private List<AudioClip> host_Time_insults_ES;
    [SerializeField] private List<AudioClip> host_Time_insults_ENG;

    [Header("Host Error Insults")]
    [SerializeField] private List<AudioClip> host_error_insults_ITA;
    [SerializeField] private List<AudioClip> host_error_insults_ES;
    [SerializeField] private List<AudioClip> host_error_insults_ENG;

    [Header("Host Loss Message")]
    [SerializeField] private List<AudioClip> host_loss_ITA;
    [SerializeField] private List<AudioClip> host_loss_ES;
    [SerializeField] private List<AudioClip> host_loss_ENG;

    [Header("Audience Generic Insults")]
    [SerializeField] private List<AudioClip> audience_generci_insults_ITA;
    [SerializeField] private List<AudioClip> audience_generci_insults_ES;
    [SerializeField] private List<AudioClip> audience_generci_insults_ENG;

    [Header("Audience Time Insults")]
    [SerializeField] private List<AudioClip> audience_time_insults_ITA;
    [SerializeField] private List<AudioClip> audience_time_insults_ES;
    [SerializeField] private List<AudioClip> audience_time_insults_ENG;

    [Header("Audience Card Insults")]
    [SerializeField] private List<AudioClip> audience_card_insults_ITA;
    [SerializeField] private List<AudioClip> audience_card_insults_ES;
    [SerializeField] private List<AudioClip> audience_card_insults_ENG;

    [Header("Audience Gun Insults")]
    [SerializeField] private List<AudioClip> audience_gun_insults_ITA;
    [SerializeField] private List<AudioClip> audience_gun_insults_ES;
    [SerializeField] private List<AudioClip> audience_gun_insults_ENG;

    [Header("Audience Labyrinth Insults")]
    [SerializeField] private List<AudioClip> audience_lab_insults_ITA;
    [SerializeField] private List<AudioClip> audience_lab_insults_ES;
    [SerializeField] private List<AudioClip> audience_lab_insults_ENG;

    [Header("First Dialogue UI")]
    [SerializeField] private GameObject _dialogue_ita;
    [SerializeField] private GameObject _dialogue_es;
    [SerializeField] private GameObject _dialogue_eng;
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
    }

// --- PRIVATE HELPER METHOD ---
    // Selects the right list based on currentLanguage
    private List<AudioClip> GetLocalizedList(List<AudioClip> ita, List<AudioClip> es, List<AudioClip> eng)
    {
        return currentLanguage switch
        {
            Language.Italian => ita,
            Language.Spanish  => es,
            Language.English => eng,
            _            => eng
        };
    }

    private GameObject GetLocalizedGO(GameObject ita, GameObject es, GameObject eng)
    {
        return currentLanguage switch
        {
            Language.Italian => ita,
            Language.Spanish  => es,
            Language.English => eng,
            _            => eng
        };
    }
    // --- PUBLIC GETTERS (Properties) FOR EXTERNAL SCRIPTS ---

    public List<AudioClip> HostIntro 
        => GetLocalizedList(host_Intro_ITA, host_Intro_ES, host_Intro_ENG);

    public List<AudioClip> HostTutorials 
        => GetLocalizedList(host_tutorials_ITA, host_tutorials_ES, host_tutorials_ENG);

    public List<AudioClip> HostTimeInsults 
        => GetLocalizedList(host_Time_insults_ITA, host_Time_insults_ES, host_Time_insults_ENG);

    public List<AudioClip> HostErrorInsults 
        => GetLocalizedList(host_error_insults_ITA, host_error_insults_ES, host_error_insults_ENG);

    public List<AudioClip> HostLoss 
        => GetLocalizedList(host_loss_ITA, host_loss_ES, host_loss_ENG);

    public List<AudioClip> AudienceGenericInsults 
        => GetLocalizedList(audience_generci_insults_ITA, audience_generci_insults_ES, audience_generci_insults_ENG);

    public List<AudioClip> AudienceTimeInsults 
        => GetLocalizedList(audience_time_insults_ITA, audience_time_insults_ES, audience_time_insults_ENG);

    public List<AudioClip> AudienceCardInsults 
        => GetLocalizedList(audience_card_insults_ITA, audience_card_insults_ES, audience_card_insults_ENG);

    public List<AudioClip> AudienceGunInsults 
        => GetLocalizedList(audience_gun_insults_ITA, audience_gun_insults_ES, audience_gun_insults_ENG);

    public List<AudioClip> AudienceLabInsults 
        => GetLocalizedList(audience_lab_insults_ITA, audience_lab_insults_ES, audience_lab_insults_ENG);

    public GameObject Dialogue 
        => GetLocalizedGO(_dialogue_ita, _dialogue_es, _dialogue_es);
}
