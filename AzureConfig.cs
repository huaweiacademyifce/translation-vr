using UnityEngine;

[CreateAssetMenu(fileName = "AzureConfig", menuName = "Translation VR/Azure Config", order = 1)]
public class AzureConfig : ScriptableObject
{
    [Header("Azure Speech Services")]
    [SerializeField] private string subscriptionKey = "";
    [SerializeField] private string region = "brazilsouth";
    
    public string SubscriptionKey 
    { 
        get 
        {
            // Primeiro tenta pegar da variável de ambiente
            string envKey = System.Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
            if (!string.IsNullOrEmpty(envKey))
                return envKey;
                
            // Se não encontrou na variável de ambiente, usa o valor do ScriptableObject
            if (string.IsNullOrEmpty(subscriptionKey))
            {
                Debug.LogError("Azure Subscription Key não configurada! Configure através de variável de ambiente AZURE_SPEECH_KEY ou no ScriptableObject AzureConfig.");
                return "";
            }
            
            return subscriptionKey;
        }
    }
    
    public string Region 
    { 
        get 
        {
            // Primeiro tenta pegar da variável de ambiente
            string envRegion = System.Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION");
            if (!string.IsNullOrEmpty(envRegion))
                return envRegion;
                
            return region;
        }
    }
}