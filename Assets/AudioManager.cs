using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public AudioSource[] games = new AudioSource[3];
    public AudioSource audienceRight;
    public AudioSource audienceLeft;

    public AudioSource presenter;
    public List<AudioClip> presenter_generci_insults;
    public List<AudioClip> audience_generci_insults;
    public AudioClip bad;
    public AudioClip lvlUP;

    public AudioClip shot;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void playBad(int game)
    {
        games[game].PlayOneShot(bad);
    }

    public void playShot()
    {
        games[1].PlayOneShot(shot);
    }

    public void play_gen_insult()
    {
        // Randomly choose between A and B
        //bool useA = Random.value < 0.5f;
        if (Random.value < 0.5f)
        {
            // Safety check
            if (audience_generci_insults == null || audience_generci_insults.Count == 0)
                return;

            // Pick random clip from A
            AudioClip clip = audience_generci_insults[Random.Range(0, audience_generci_insults.Count)];

            // Randomly choose x or y
            AudioSource targetSource = (Random.value < 0.5f) ? audienceLeft : audienceRight;

            targetSource.PlayOneShot(clip);
        }
        else
        {
            // Safety check
            if (presenter_generci_insults == null || presenter_generci_insults.Count == 0)
                return;

            // Pick random clip from B
            AudioClip clip = presenter_generci_insults[Random.Range(0, presenter_generci_insults.Count)];

            // Play on z
            presenter.PlayOneShot(clip);
        }
    }
    }