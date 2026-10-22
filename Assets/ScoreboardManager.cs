using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Text;

public class ScoreboardManager : MonoBehaviour
{
    [SerializeField] private TMP_Text textA;
    [SerializeField] private TMP_Text textB;

    private List<Participant> participants = new List<Participant>();
    private Participant player = new Participant { id = 21, points = 0 };

    void Start()
    {
        GenerateFakeParticipants();
        UpdateScoreboard();
    }

    private void GenerateFakeParticipants()
{
    participants = new List<Participant>
    {
        new Participant { id = 1,  points = 210 },
        new Participant { id = 2,  points = 180 },
        new Participant { id = 3,  points = 180 },
        new Participant { id = 4,  points = 300 },
        new Participant { id = 5,  points = 150 },
        new Participant { id = 6,  points = 210 },
        new Participant { id = 7,  points = 180 },
        new Participant { id = 8,  points = 240 },
        new Participant { id = 9,  points = 150 },
        new Participant { id = 10, points = 90 },
        new Participant { id = 11, points = 210 },
        new Participant { id = 12, points = 90 },
        new Participant { id = 13, points = 160 },
        new Participant { id = 14, points = 120 },
        new Participant { id = 15, points = 120 },
        new Participant { id = 16, points = 90  },
        new Participant { id = 17, points = 150  },
        new Participant { id = 18, points = 60  },
        new Participant { id = 19, points = 270  },
        new Participant { id = 20, points = 60  },
    };
}

    public void AddPlayerPoints(int points)
    {
        player.points += points;
        UpdateScoreboard();
    }

    public void UpdateScoreboard()
    {
        List<Participant> all = new List<Participant>(participants) { player };
        all.Sort((a, b) =>
        {
            int pointsComparison = b.points.CompareTo(a.points);
            if (pointsComparison != 0)
                return pointsComparison;
            
            return b.id.CompareTo(a.id); // tiebreaker: higher ID wins
        });

        int playerRank = all.IndexOf(player) + 1;

        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < 3; i++)
        {
            sb.AppendLine(FormatEntry(all[i], i + 1));
        }

        sb.AppendLine("----------");
        sb.Append(FormatEntry(player, playerRank));

        ScoreboardText = sb.ToString();
    }

    private string FormatEntry(Participant p, int rank)
    {
        return $"Participant {p.id}: {p.points}pts - {rank}°";
    }

    private string _scoreboardText;
    public string ScoreboardText
    {
        get => _scoreboardText;
        set
        {
            _scoreboardText = value;
            textA.text = value;
            textB.text = value;
        }
    }

    [System.Serializable]
    private class Participant
    {
        public int id;
        public int points;
    }
}
