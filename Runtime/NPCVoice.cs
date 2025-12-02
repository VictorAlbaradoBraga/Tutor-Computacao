using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;

public class NPCVoice : MonoBehaviour
{
    [Header("Configurações da IA")]
    private string apiKey;
    public string model;

    public enum LearningLevel { Inicial, Intermediario, Avancado }
    public LearningLevel currentLevel = LearningLevel.Inicial;

    [Header("Níveis Personalizados (Editáveis no Inspector)")]
    [TextArea(4, 10)]
    public string nivelInicialText;

    [TextArea(4, 10)]
    public string nivelIntermediarioText;

    [TextArea(4, 10)]
    public string nivelAvancadoText;

    [Header("Referências")]
    public TextToSpeech textToSpeech;
    public ChatBox chatBox;

    private List<AIMessage> messageHistory = new List<AIMessage>();

    [System.Serializable]
    public class AIRequest { public string model; public AIMessage[] messages; }

    [System.Serializable]
    public class AIMessage { public string role; public string content; }

    [System.Serializable]
    private class AIResponse { public AIChoice[] choices; }

    [System.Serializable]
    private class AIChoice { public AIMessage message; }


    private void OnEnable()
    {
        TextToSpeech.OnTTSMessageStarted += StartTypingChatBox;
    }

    private void OnDisable()
    {
        TextToSpeech.OnTTSMessageStarted -= StartTypingChatBox;
    }

    void Start()
    {
        EnvLoader.LoadEnv();
        apiKey = EnvLoader.GetEnv("GROQ_API_KEY");
        model = EnvLoader.GetEnv("MODEL");

        if (chatBox == null)
            chatBox = FindAnyObjectByType<ChatBox>();

        ApplySystemMessage();
    }

    // Seleção de nível por script / trigger
    public void SetLearningLevel(LearningLevel newLevel)
    {
        currentLevel = newLevel;
        messageHistory.Clear();
        ApplySystemMessage();

        chatBox?.AddMessageToChat($"--- Nível alterado para {currentLevel} ---", Color.yellow);
    }

    private void ApplySystemMessage()
    {
        string baseRules =
            "Você é um tutor especializado em ensinar computação para pessoas com TEA. " +
            "Use frases curtas, claras e nunca dê a resposta direta. " +
            "Sempre induza o raciocínio. Responda até 3 frases e no máximo 600 caracteres.\n\n";

        string nivelText = currentLevel switch
        {
            LearningLevel.Inicial => nivelInicialText,
            LearningLevel.Intermediario => nivelIntermediarioText,
            LearningLevel.Avancado => nivelAvancadoText,
            _ => ""
        };

        messageHistory.Add(new AIMessage
        {
            role = "system",
            content = baseRules + nivelText
        });
    }


    public void StartSpeechToIAProcess(string recognizedSpeech)
    {
        if (!string.IsNullOrEmpty(recognizedSpeech))
            StartCoroutine(SendToIA(recognizedSpeech));
    }


    IEnumerator SendToIA(string inputText)
    {
        string apiUrl = "https://api.groq.com/openai/v1/chat/completions";

        messageHistory.RemoveAll(m => m.role == "system");
        ApplySystemMessage();

        messageHistory.Add(new AIMessage
        {
            role = "user",
            content = inputText
        });

        var requestData = new AIRequest
        {
            model = model,
            messages = messageHistory.ToArray()
        };

        string jsonData = JsonUtility.ToJson(requestData);
        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonData)),
            downloadHandler = new DownloadHandlerBuffer()
        };

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            chatBox.AddAIResponseWithTyping("Erro ao processar. Tente novamente.", Color.red, 0.03f);
            yield break;
        }

        string respostaIA = ParseAIResponse(request.downloadHandler.text);
        respostaIA = CleanResponse(respostaIA);

        messageHistory.Add(new AIMessage
        {
            role = "assistant",
            content = respostaIA
        });

        textToSpeech?.Speak(respostaIA);
    }

    private string ParseAIResponse(string json)
    {
        try
        {
            AIResponse response = JsonUtility.FromJson<AIResponse>(json);
            return response?.choices?[0]?.message?.content ?? "Não entendi sua pergunta.";
        }
        catch
        {
            return "Erro ao interpretar resposta.";
        }
    }

    private string CleanResponse(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        string clean = System.Text.RegularExpressions.Regex
            .Replace(text, @"[\*`_~\|#>]", "")
            .Trim();

        if (clean.Length > 600)
            clean = clean.Substring(0, 600);

        return clean;
    }

    public void StartTypingChatBox(string text)
    {
        chatBox?.AddAIResponseWithTyping("IA: " + text, Color.white, 0.03f);
    }
}
