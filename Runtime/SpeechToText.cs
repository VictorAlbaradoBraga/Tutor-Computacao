using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;
using System.Text;
using System;
using System.IO;

public class SpeechToText : MonoBehaviour
{
    [Header("Google API")]
    private string apiKey;
    public string languageCode = "pt-BR";

    [Header("UI")]
    public Button recordButton;
    public Image recordButtonImage;
    public Color idleColor = Color.white;
    public Color blinkColor = Color.red;

    [Header("Tecla para Gravar")]
    public KeyCode recordKey = KeyCode.V; // padrão: barra de espaço


    private AudioClip recordedClip;
    private bool isRecording = false;
    private Coroutine blinkCoroutine;
    private ChatBox chatBox;
    private NPCVoice npcVoice;

    void Start()
    {
        EnvLoader.LoadEnv();
        apiKey = EnvLoader.GetEnv("GOOGLE_CLOUD_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
            Debug.LogError("API Key do Google Cloud não foi encontrada no .env");
        chatBox = FindAnyObjectByType<ChatBox>();
        if (chatBox == null)
        {
            Debug.LogError("ChatBox não encontrado na cena!");
        }
        npcVoice = FindAnyObjectByType<NPCVoice>();
        if (npcVoice == null)
        {
            Debug.LogError("NPCVoice não encontrado na cena!");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(recordKey))
        {
            ToggleRecording();
        }
    }



    public void ToggleRecording()
    {
        if (isRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    public void StartRecording()
    {
        if (isRecording)
        {
            Debug.LogWarning("Já está gravando.");
            return;
        }

        recordedClip = Microphone.Start(null, true, 300, 16000);
        isRecording = true;
        Debug.Log("Gravação iniciada.");

        if (blinkCoroutine == null)
            blinkCoroutine = StartCoroutine(BlinkButton());
    }

    public void StopRecording()
    {
        if (!isRecording)
        {
            Debug.LogWarning("Nenhuma gravação em andamento.");
            return;
        }

        int position = Microphone.GetPosition(null);
        Microphone.End(null);
        isRecording = false;

        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        recordButtonImage.color = idleColor;

        if (position <= 0)
        {
            Debug.LogWarning("Nenhum dado de áudio capturado.");
            return;
        }

        float[] samples = new float[position * recordedClip.channels];
        recordedClip.GetData(samples, 0);

        byte[] wavData = EncodeAsWAV(samples, recordedClip.frequency, recordedClip.channels);
        string base64Audio = Convert.ToBase64String(wavData);

        StartCoroutine(SendSTTRequest(base64Audio));
    }

    private IEnumerator SendSTTRequest(string audioBase64)
    {
        string url = $"https://speech.googleapis.com/v1/speech:recognize?key={apiKey}";

        var requestBody = new SpeechToTextRequest
        {
            config = new RecognitionConfig
            {
                encoding = "LINEAR16",
                sampleRateHertz = 16000,
                languageCode = languageCode
            },
            audio = new RecognitionAudio
            {
                content = audioBase64
            }
        };

        string json = JsonUtility.ToJson(requestBody);

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Erro no STT: {request.error}\n{request.downloadHandler.text}");
            yield break;
        }

        string responseJson = request.downloadHandler.text;
        var response = JsonUtility.FromJson<STTResponseWrapper>(responseJson);


        if (response != null && response.results.Length > 0)
        {
            string transcript = response.results[0].alternatives[0].transcript;

            if (!string.IsNullOrEmpty(transcript))
            {
                chatBox.AddMessageToChat("Você: " + transcript, Color.cyan);
                chatBox.chatField.text = ""; // Limpa o inputField, por segurança
                npcVoice.StartSpeechToIAProcess(transcript);
            }

        }
        else
        {
            Debug.LogWarning("Nenhum resultado retornado.");
        }
    }

  

    private byte[] EncodeAsWAV(float[] samples, int frequency, int channels)
    {
        using (var memoryStream = new MemoryStream(44 + samples.Length * 2))
        {
            using (var writer = new BinaryWriter(memoryStream))
            {
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + samples.Length * 2);
                writer.Write("WAVE".ToCharArray());
                writer.Write("fmt ".ToCharArray());
                writer.Write(16);
                writer.Write((ushort)1);
                writer.Write((ushort)channels);
                writer.Write(frequency);
                writer.Write(frequency * channels * 2);
                writer.Write((ushort)(channels * 2));
                writer.Write((ushort)16);
                writer.Write("data".ToCharArray());
                writer.Write(samples.Length * 2);

                foreach (var sample in samples)
                {
                    short intSample = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue);
                    writer.Write(intSample);
                }
            }
            return memoryStream.ToArray();
        }
    }

    private IEnumerator BlinkButton()
    {
        while (isRecording)
        {
            recordButtonImage.color = blinkColor;
            yield return new WaitForSeconds(0.5f);
            recordButtonImage.color = idleColor;
            yield return new WaitForSeconds(0.5f);
        }
    }

    [Serializable]
    private class SpeechToTextRequest
    {
        public RecognitionConfig config;
        public RecognitionAudio audio;
    }

    [Serializable]
    private class RecognitionConfig
    {
        public string encoding;
        public int sampleRateHertz;
        public string languageCode;
    }

    [Serializable]
    private class RecognitionAudio
    {
        public string content;
    }

    [Serializable]
    private class STTResponseWrapper
    {
        public STTResult[] results;
    }

    [Serializable]
    private class STTResult
    {
        public STTAlternative[] alternatives;
    }

    [Serializable]
    private class STTAlternative
    {
        public string transcript;
        public float confidence;
    }
}
