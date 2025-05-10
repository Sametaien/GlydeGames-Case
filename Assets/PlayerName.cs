#region

using Fusion;
using TMPro;
using UnityEngine;

#endregion

public class PlayerName : NetworkBehaviour
{
    [SerializeField] private TextMeshPro nameText; 
    [SerializeField] private Vector3 offset = new(0, 1f, 0); 
    [SerializeField] private float characterSpacing; 

    private Camera _mainCamera;

    [Networked] 
    private string PlayerDisplayName { get; set; }

    private void Awake()
    {
        if (nameText == null) Debug.LogError("NameText is not assigned in PlayerName script!", this);
    }

    public override void Spawned()
    {
        
        _mainCamera = Camera.main;

        if (HasStateAuthority)
        {
            
            PlayerDisplayName = "Player_" + Random.Range(1000, 9999);
            Debug.Log($"Assigned player name: {PlayerDisplayName}");
        }

        
        UpdateNameDisplay();

        
        if (HasInputAuthority)
        {
            if (nameText != null)
            {
                nameText.gameObject.SetActive(false);
                Debug.Log("Local player's name text disabled.");
            }
        }
        else
        {
           
            if (nameText != null)
            {
                nameText.gameObject.SetActive(true);
                nameText.characterSpacing = characterSpacing;
            }
        }
    }

    public override void Render()
    {
        
        if (HasInputAuthority) return;

        
        UpdateNameDisplay();

        
        if (_mainCamera != null && nameText != null)
        {
            
            nameText.transform.position = transform.position + offset;

           
            var directionToCamera = _mainCamera.transform.position - nameText.transform.position;
            directionToCamera.y = 0; 
            if (directionToCamera.sqrMagnitude > 0.01f) 
            {
                var targetRotation = Quaternion.LookRotation(-directionToCamera);
                nameText.transform.rotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0);
            }
        }
    }

    private void UpdateNameDisplay()
    {
        if (nameText != null) nameText.text = PlayerDisplayName;
    }

    
    public void SetPlayerName(string newName)
    {
        if (HasStateAuthority)
        {
            
            newName = newName.Trim();
            if (newName.Length > 20) newName = newName.Substring(0, 20);
            if (string.IsNullOrEmpty(newName)) newName = "Unnamed";

            PlayerDisplayName = newName;
            Debug.Log($"Player name updated to: {PlayerDisplayName}");
        }
    }
}