using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MicrophoneInput : MonoBehaviour
{
    public string microphoneDevice;
    private AudioSource audioSource;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        // Pick default mic if not assigned
        if (Microphone.devices.Length > 0)
        {
            if (string.IsNullOrEmpty(microphoneDevice))
                microphoneDevice = Microphone.devices[0];

            Debug.Log("Using microphone: " + microphoneDevice);

            audioSource.clip = Microphone.Start(microphoneDevice, true, 10, 44100);
            audioSource.loop = true;

            // Wait until mic starts recording before playback
            while (!(Microphone.GetPosition(microphoneDevice) > 0)) { }

            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("No microphone found!");
        }

    }  
}
