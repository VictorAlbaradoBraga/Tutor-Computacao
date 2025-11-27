using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UI;
using TMPro;

// Script responsável por conectar direto com o Groq + TTS
public class NPCVoice : MonoBehaviour
{
    [Header("Configurações da IA")]
    private string apiKey;
    public string model;

    public enum LearningLevel { Inicial, Intermediario, Avancado }
    public LearningLevel currentLevel = LearningLevel.Inicial;

    [Header("Referências do Unity")]
    public TextToSpeech textToSpeech;
    public ChatBox chatBox;

    [Header("Botões de Nível")]
    public Button inicialButton;
    public Button intermediarioButton;
    public Button avancadoButton;

    [Header("Cores de Seleção")]
    private Color activeColor = new Color(0.176f, 0.960f, 0.741f, 1f); // #2DF5BD
    private Color inactiveColor = new Color(0.643f, 0.643f, 0.643f, 1f); // #A4A4A4
    private Color activeTextColor = Color.white;
    private Color inactiveTextColor = new Color(0.643f, 0.643f, 0.643f, 1f);

    private List<AIMessage> messageHistory = new List<AIMessage>();

    [System.Serializable]
    public class AIRequest { public string model; public AIMessage[] messages; }

    [System.Serializable]
    public class AIMessage { public string role; public string content; }

    [System.Serializable]
    private class AIResponse { public AIChoice[] choices; }

    [System.Serializable]
    private class AIChoice { public AIMessage message; }

    void Start()
    {
        EnvLoader.LoadEnv();
        apiKey = EnvLoader.GetEnv("GROQ_API_KEY");
        model = EnvLoader.GetEnv("MODEL");

        if (string.IsNullOrEmpty(apiKey))
            Debug.LogError("API Key do Groq não foi encontrada no .env");

        if (chatBox == null)
            chatBox = FindAnyObjectByType<ChatBox>();

        // Aplica nível inicial e atualiza botões
        ApplySystemMessage();
        UpdateButtonColors();
    }

    // ====== Métodos de mudança de nível ======
    public void SetLevelInicial()
    {
        SetLearningLevel(LearningLevel.Inicial);
        UpdateButtonColors();
    }

    public void SetLevelIntermediario()
    {
        SetLearningLevel(LearningLevel.Intermediario);
        UpdateButtonColors();
    }

    public void SetLevelAvancado()
    {
        SetLearningLevel(LearningLevel.Avancado);
        UpdateButtonColors();
    }

    public void SetLearningLevel(LearningLevel newLevel)
    {
        currentLevel = newLevel;
        messageHistory.Clear();
        ApplySystemMessage();

        chatBox.AddMessageToChat($"--- Nível alterado para {currentLevel} ---", Color.yellow);
    }

    private void UpdateButtonColors()
    {
        UpdateButtonAppearance(inicialButton, LearningLevel.Inicial);
        UpdateButtonAppearance(intermediarioButton, LearningLevel.Intermediario);
        UpdateButtonAppearance(avancadoButton, LearningLevel.Avancado);
    }

    private void UpdateButtonAppearance(Button button, LearningLevel level)
    {
        if (button == null) return;

        bool isActive = currentLevel == level;

        // Cor do fundo
        Image img = button.GetComponent<Image>();
        if (img != null)
            img.color = isActive ? activeColor : inactiveColor;

        // Primeiro tenta TextMeshPro
        TextMeshProUGUI tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.color = isActive ? activeTextColor : inactiveTextColor;
            return;
        }

        // Se não houver TMP, tenta o Text padrão
        Text uiText = button.GetComponentInChildren<Text>(true);
        if (uiText != null)
        {
            uiText.color = isActive ? activeTextColor : inactiveTextColor;
        }
    }
    private void ApplySystemMessage()
    {
        string baseRules =
            "Você é um tutor especializado em ensinar computação para pessoas com Transtorno do Espectro Autista. \n" +
            "Sempre respeite o nível atual do aprendiz. \n" +
            "Nunca forneça a resposta direta dos desafios ou exercícios.\n" +
            "Induza o aprendiz a pensar, refletir e encontrar a resposta por si só.\n" +
            "Sempre reforce os conceitos relacionados ao problema.\n" +
            "Use linguagem clara, frases curtas e diretas, sem emojis ou símbolos de Markdown. \n" +
            "Nunca dê a resposta pronta, sempre induza o raciocínio. \n" +
            "Use símbolos de programação apenas quando fizer parte do conteúdo (ex: * para multiplicação).\n" +
            "Se perceber confusão ou bloqueio, ofereça dicas passo a passo e valide o esforço do aprendiz com empatia.\n\n" +
            "Se o usuário fizer perguntas que fogem da área de computação, responda de forma breve e redirecione com gentileza a conversa para a trilha de aprendizagem.\n\n" +
            "Finalize frases de forma completa. Responda em até 3 frases, máximo de 600 caracteres.\n\n";

        string content = baseRules;

        switch (currentLevel)
        {
            case LearningLevel.Inicial:
                content +=
                    "Nível Inicial: Introdução à Programação. Só aborde conteúdos desse nível.\n" +
                    "- Tipos de dados (números, textos, booleanos)\n" +
                    "- Variáveis e operadores\n" +
                    "- Entrada e saída de dados\n" +
                    "- Condicionais (if/else)\n" +
                    "- Estruturas de repetição (while, for)";
                break;

            case LearningLevel.Intermediario:
                content +=
                    "Nível Intermediário: Programação e Algoritmos. Só aborde conteúdos desse nível.\n" +
                    "- Funções e escopo\n" +
                    "- Vetores e matrizes\n" +
                    "- Algoritmos simples (máximo, mínimo, média, ordenação) \n" +
                    "- Introdução à recursão";
                break;

            case LearningLevel.Avancado:
                content +=
                    "Nível Avançado: Estruturas de Dados. Só aborde conteúdos desse nível.\n" +
                    "- Listas encadeadas\n" +
                    "- Pilhas e filas\n" +
                    "- Árvores binárias\n" +
                    "- Ordenação e busca (bubble sort, selection sort, insertion sort, binary search)";
                break;
        }

        messageHistory.Add(new AIMessage
        {
            role = "system",
            content = content
        });
    }

    // Entrada principal (fala reconhecida do usuário)
    public void StartSpeechToIAProcess(string recognizedSpeech)
    {
        if (!string.IsNullOrEmpty(recognizedSpeech))
            StartCoroutine(SendToIA(recognizedSpeech));
    }

    IEnumerator SendToIA(string inputText)
    {
        string apiUrl = "https://api.groq.com/openai/v1/chat/completions";

        // Reaplica a regra do nível atual SEM limpar o histórico
        messageHistory.RemoveAll(m => m.role == "system");
        ApplySystemMessage();

        // Entrada do usuário
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
        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Erro na IA: {request.error}");
            chatBox.AddAIResponseWithTyping(
                "Desculpe, tive um problema...", // texto
                Color.white,                      // cor
                0.03f                             // delay por caractere (ajuste conforme quiser)
            );
        }
        else
        {
            string respostaIA = ParseAIResponse(request.downloadHandler.text);

            respostaIA = CleanResponse(respostaIA);

            // Resposta do assistente
            messageHistory.Add(new AIMessage
            {
                role = "assistant",
                content = respostaIA
            });

            
            PlayText(respostaIA);
        }
    }


    private string ParseAIResponse(string json)
    {
        try
        {
            AIResponse response = JsonUtility.FromJson<AIResponse>(json);
            return response?.choices?[0]?.message?.content ?? "Não entendi sua pergunta...";
        }
        catch
        {
            return "Houve um erro ao processar a resposta...";
        }
    }

    private string CleanResponse(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        string clean = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"(\*{2}|_{2}|~{2}|`{1,3}|#{1,6}|^>|\|)",
            ""
        );

        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", " ").Trim();

        if (clean.Length > 600)
            clean = clean.Substring(0, 600);

        return clean;
    }

    private void PlayText(string text)
    {
        if (textToSpeech != null)
            textToSpeech.Speak(text);
    }

    public void StartTypingChatBox(string text)
    {
        if (chatBox != null)
            chatBox.AddAIResponseWithTyping("IA: " + text, Color.white, 0.03f);
    }

}
