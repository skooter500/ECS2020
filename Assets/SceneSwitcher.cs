﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class SceneSwitcher : MonoBehaviour
{
    public string[] scenes; 

    // Update is called once per frame
    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        // Input.GetKeyDown(KeyCode.Joystick1Button6) || 
        if (keyboard.uKey.wasPressedThisFrame)
        {
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
            string[] scenes = new string[sceneCount];
            string sceneName = SceneManager.GetActiveScene().name;
            int i = 0;
            for (i = 0; i < sceneCount; i++)
            {
                scenes[i] = System.IO.Path.GetFileNameWithoutExtension(UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i));
                
            }
            for (i = 0; i < sceneCount; i++)
            {
                if (sceneName == scenes[i])
                {
                    break;
                }
            }

            i = (i + 1) % scenes.Length;

            ew.BoidBootstrap bb = FindObjectOfType<ew.BoidBootstrap>();
            if (bb != null)
            {
                //bb.DestroyEntities();
            }

            SceneManager.LoadScene(scenes[i]);            
        }
    }
}
