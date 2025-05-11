#region

using TMPro;
using UnityEngine;

#endregion

public class NameChanger : MonoBehaviour
{
    private const string PLAYER_NAME_KEY = "PlayerName";
    [SerializeField] private TMP_InputField nameInputField;

    private void Start()
    {
        if (nameInputField != null && PlayerPrefs.HasKey(PLAYER_NAME_KEY))
            nameInputField.text = PlayerPrefs.GetString(PLAYER_NAME_KEY);
    }

    public void ChangeName()
    {
        if (nameInputField == null)
        {
            LogError("TMP_InputField is not assigned in the Inspector.");
            return;
        }

        var newName = nameInputField.text.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            LogError("Player name cannot be empty.");
            nameInputField.text = string.Empty;
            return;
        }

        PlayerPrefs.SetString(PLAYER_NAME_KEY, newName);
        PlayerPrefs.Save();
        nameInputField.text = newName;
        Debug.Log($"Player name changed to: {newName}");
    }


    private void LogError(string message)
    {
        Debug.LogError($"[NameChanger] {message}");
    }
}