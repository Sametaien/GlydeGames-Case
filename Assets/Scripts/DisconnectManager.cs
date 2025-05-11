using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisconnectManager : MonoBehaviour
{
    public void Disconnect()
    {
        var networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager != null)
        {
            _ = networkManager.DisconnectAsync();
        }
        else
        {
            Debug.LogError("NetworkManager not found in the scene.");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Disconnect();
        }
    }
}
