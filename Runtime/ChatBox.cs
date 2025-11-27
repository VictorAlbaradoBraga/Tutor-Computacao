using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI; // importante para acessar Button

public class ChatBox : MonoBehaviour
{
    public int maxMessages = 25;
    public GameObject chatPanel;
    public GameObject textPrefab;
    public TMP_InputField chatField;

    [SerializeField]
    private List<Message> messageList = new List<Message>();
    private NPCVoice npcVoice;

    void Start()
    {
        npcVoice = FindAnyObjectByType<NPCVoice>();
        if (npcVoice == null)
            Debug.LogError("NPCVoice não encontrado na cena!");

        chatField.onSelect.AddListener((string text) =>
        {
            chatField.ActivateInputField();
            chatField.caretBlinkRate = 0.85f;
            chatField.caretWidth = 2;
        });
    }

    void Update()
    {
        if (!string.IsNullOrEmpty(chatField.text) && Input.GetKeyDown(KeyCode.Return))
        {
            string userMessage = chatField.text;
            AddMessageToChat("Você: " + userMessage, Color.cyan);
            chatField.text = "";

            if (npcVoice != null)
            {
                npcVoice.StartSpeechToIAProcess(userMessage);
            }
        }
    }

    public void AddMessageToChat(string text, Color color)
    {
        if (messageList.Count >= maxMessages)
            DestroyOldestMessage();

        GameObject newTextObj = Instantiate(textPrefab, chatPanel.transform);
        TMP_Text textComponent = newTextObj.GetComponentInChildren<TMP_Text>();
        Button repeatButton = newTextObj.GetComponentInChildren<Button>(true); // procura mesmo se estiver desativado

        textComponent.text = text;
        textComponent.color = color;

        // Se for mensagem do usuário, botão continua desativado
        if (text.StartsWith("IA:"))
        {
            repeatButton.gameObject.SetActive(true);

            // Define texto para o RepeatButton
            RepeatButton rb = repeatButton.GetComponent<RepeatButton>();
            if (rb != null)
            {
                // Retira o prefixo "IA: " antes de enviar para o TTS
                string cleanText = text.Substring(4).Trim();
                rb.SetMessage(cleanText);
            }
        }
        else
        {
            repeatButton.gameObject.SetActive(false);
        }

        Message newMessage = new Message
        {
            text = text,
            textObject = textComponent
        };
        messageList.Add(newMessage);
    }

    public void AddAIResponseWithTyping(string fullText, Color color, float delayPerChar = 0.05f)
    {
        if (messageList.Count >= maxMessages)
            DestroyOldestMessage();

        GameObject newTextObj = Instantiate(textPrefab, chatPanel.transform);
        TMP_Text textComponent = newTextObj.GetComponentInChildren<TMP_Text>();
        Button repeatButton = newTextObj.GetComponentInChildren<Button>(true);

        textComponent.text = "";
        textComponent.color = color;

        // Habilita botão de repetir só para IA
        repeatButton.gameObject.SetActive(true);
        RepeatButton rb = repeatButton.GetComponent<RepeatButton>();
        if (rb != null)
        {
            rb.SetMessage(fullText);
        }

        Message newMessage = new Message
        {
            text = fullText,
            textObject = textComponent
        };
        messageList.Add(newMessage);

        StartCoroutine(TypeTextCoroutine(fullText, textComponent, delayPerChar));
    }

    private IEnumerator TypeTextCoroutine(string text, TMP_Text textComponent, float delay)
    {
        foreach (char c in text)
        {
            textComponent.text += c;
            yield return new WaitForSeconds(delay);
        }
    }

    private void DestroyOldestMessage()
    {
        if (messageList.Count == 0) return;

        Destroy(messageList[0].textObject.gameObject);
        messageList.RemoveAt(0);
    }

    public TMP_Text GetLastTextComponent()
    {
        if (messageList.Count == 0) return null;
        return messageList[messageList.Count - 1].textObject;
    }

    public List<NPCVoice.AIMessage> GetFormattedMessages()
    {
        List<NPCVoice.AIMessage> aiMessages = new List<NPCVoice.AIMessage>();

        foreach (Message msg in messageList)
        {
            if (msg.text.StartsWith("Você: "))
            {
                aiMessages.Add(new NPCVoice.AIMessage
                {
                    role = "user",
                    content = msg.text.Substring(6)
                });
            }
            else if (msg.text.StartsWith("IA: "))
            {
                aiMessages.Add(new NPCVoice.AIMessage
                {
                    role = "assistant",
                    content = msg.text.Substring(4)
                });
            }
        }

        return aiMessages;
    }

    public void OnSendButtonClicked()
    {
        if (!string.IsNullOrEmpty(chatField.text))
        {
            string userMessage = chatField.text;
            AddMessageToChat("Você: " + userMessage, Color.cyan);
            chatField.text = "";

            if (npcVoice != null)
            {
                npcVoice.StartSpeechToIAProcess(userMessage);
            }
        }
    }

}

[System.Serializable]
public class Message
{
    public string text;
    public TMP_Text textObject;
}
