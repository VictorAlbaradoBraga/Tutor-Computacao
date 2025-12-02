using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Globalization;

public class TextToSpeech : MonoBehaviour
{
    public static event Action<string> OnTTSMessageStarted;

    [Header("Configurações de Voz")]
    public VoiceType voiceType = VoiceType.Robo;
    public string voiceNameOverride = "";
    [Range(-10f, 10f)] public float pitch = 2.0f;
    [Range(0.5f, 2.0f)] public float speakingRate = 0.95f;

    private string apiKey;
    private AudioSource audioSource;
    private string lastMessageText;
    public bool isSpeaking = false;

    private string cachePath;
    private const float MAX_CACHE_SIZE_MB = 100f;

    private void Start()
    {
        EnvLoader.LoadEnv();
        apiKey = EnvLoader.GetEnv("GOOGLE_CLOUD_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
            Debug.LogError("API Key do Google Cloud não foi encontrada no .env (GOOGLE_CLOUD_API_KEY)");

        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        cachePath = Application.temporaryCachePath;
        Directory.CreateDirectory(cachePath);
        ClearOldAudioCache();
    }

    public void Speak(string text, bool notify = true)
    {
        if (string.IsNullOrEmpty(text))
        {
            Debug.LogError("O texto de entrada está vazio!");
            return;
        }

        lastMessageText = text;
        StartCoroutine(SpeakWithCache(text, notify));
    }

    private IEnumerator SpeakWithCache(string text, bool notify)
    {
        string hash = GetMD5Hash(text + voiceType.ToString() + (voiceNameOverride ?? ""));
        string cachedFilePath = Path.Combine(cachePath, $"tts_{hash}.mp3");

        // Se já existe cache, só toca
        if (File.Exists(cachedFilePath))
        {
            yield return PlayAudio(cachedFilePath, notify);
            yield break;
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("API Key ausente. Abortando TTS.");
            yield break;
        }

        string voiceName =
            !string.IsNullOrEmpty(voiceNameOverride) ?
            voiceNameOverride :
            GetVoiceName(voiceType);

        string escapedText = EscapeForJson(text);

        string json = $@"
        {{
            ""input"": {{
                ""text"": ""{escapedText}""
            }},
            ""voice"": {{
                ""languageCode"": ""pt-BR"",
                ""name"": ""{voiceName}""
            }},
            ""audioConfig"": {{
                ""audioEncoding"": ""MP3"",
                ""pitch"": {pitch.ToString(CultureInfo.InvariantCulture)},
                ""speakingRate"": {speakingRate.ToString(CultureInfo.InvariantCulture)}
            }}
        }}";

        string url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={apiKey}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Erro no TTS: {request.error}\n{request.downloadHandler.text}");
                yield break;
            }

            TTSResponse resp = JsonUtility.FromJson<TTSResponse>(request.downloadHandler.text);

            if (resp == null || string.IsNullOrEmpty(resp.audioContent))
            {
                Debug.LogError("Resposta de áudio vazia ou inválida!");
                yield break;
            }

            File.WriteAllBytes(cachedFilePath, Convert.FromBase64String(resp.audioContent));
            yield return PlayAudio(cachedFilePath, notify);
        }
    }

    [Serializable]
    private class TTSResponse { public string audioContent; }

    private IEnumerator PlayAudio(string filePath, bool notify)
    {
        using (UnityWebRequest www =
            UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Erro ao carregar áudio MP3: " + www.error);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            audioSource.clip = clip;
            audioSource.Play();

            OnAudioStarted(notify);
            Invoke(nameof(OnAudioFinished), audioSource.clip.length - 0.25f);
        }
    }

    private void OnAudioStarted(bool notify)
    {
        isSpeaking = true;

        if (notify)
            OnTTSMessageStarted?.Invoke(lastMessageText);
    }

    private void OnAudioFinished()
    {
        isSpeaking = false;
    }

    private string GetMD5Hash(string input)
    {
        using (var md5 = MD5.Create())
        {
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            StringBuilder sb = new StringBuilder();
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    private string EscapeForJson(string input)
    {
        return input.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", " ")
                    .Replace("\r", " ");
    }

    private void ClearOldAudioCache()
    {
        try
        {
            var files = new DirectoryInfo(cachePath).GetFiles("tts_*.mp3");
            float totalSize = 0f;

            foreach (var f in files)
                totalSize += f.Length / (1024f * 1024f);

            if (totalSize > MAX_CACHE_SIZE_MB)
                foreach (var f in files)
                    f.Delete();
        }
        catch { }
    }

    private string GetVoiceName(VoiceType type)
    {
        if (!string.IsNullOrEmpty(voiceNameOverride))
            return voiceNameOverride;

        switch (type)
        {
            case VoiceType.Masculina:
                pitch = 0f;
                speakingRate = 1f;
                return "pt-BR-Wavenet-B";

            case VoiceType.Feminina:
                pitch = 0f;
                speakingRate = 1f;
                return "pt-BR-Wavenet-A";

            case VoiceType.Robo:
            default:
                pitch = 4f;
                speakingRate = 0.85f;
                return "pt-BR-Wavenet-B";
        }
    }

    public enum VoiceType { Robo, Masculina, Feminina }
}
