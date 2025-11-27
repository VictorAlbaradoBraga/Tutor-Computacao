using UnityEngine;
using UnityEngine.UI;

public class RepeatButton : MonoBehaviour
{
    private string messageText;       // Texto da fala que o botão deve repetir
    private TextToSpeech tts;         // Referência ao TTS

    private void Start()
    {
        tts = FindAnyObjectByType<TextToSpeech>();
        GetComponent<Button>().onClick.AddListener(OnRepeatClicked);
    }

    // Remove prefixos tipo "IA:" e espaços desnecessários
    public void SetMessage(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            messageText = text;
            return;
        }

        // remove possíveis prefixes repetidos "IA:" ou "IA: " no começo do texto
        string clean = text.Trim();
        while (clean.StartsWith("IA:"))
        {
            clean = clean.Substring(3).TrimStart(); // remove "IA:" e espaços
        }

        messageText = clean;
    }

    private void OnRepeatClicked()
    {
        if (tts != null && !string.IsNullOrEmpty(messageText))
        {
            // Ao repetir, NÃO notificar o NPCVoice para evitar criar nova entrada no chat
            tts.Speak(messageText, notify: false);
        }
        else
        {
            Debug.LogWarning("TTS ou texto não definidos no RepeatButton!");
        }
    }
}
