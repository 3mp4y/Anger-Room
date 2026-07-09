using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class Logger : MonoBehaviour
{
    private string _logPath;
    // Start is called before the first frame update
    void Start()
    {
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _logPath = Application.persistentDataPath + "/log_" + timestamp + ".txt";
        Debug.Log(_logPath);
        // Create the file immediately with a header
        using (StreamWriter writer = new StreamWriter(_logPath, false))
        {
            writer.WriteLine("Log started: " + System.DateTime.Now);
        }
    }

    public void Log(string message)
    {
        using (StreamWriter writer = new StreamWriter(_logPath, true))
        {
            writer.WriteLine(System.DateTime.Now + " - " + message);
            writer.Flush();
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
