using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WineGlassMission : MonoBehaviour
{
    [Header("UI Settings")]
    public List<Image> dirtImages = new List<Image>();

    [Header("Mission Settings")]
    public float cleanSpeed = 1.5f;

    private Dictionary<Image, float> dirtAlphaMap = new Dictionary<Image, float>();

    private void Start()
    {
        foreach (Image dirt in dirtImages)
        {
            if (dirt != null)
            {
                dirtAlphaMap[dirt] = dirt.color.a;

                EventTrigger trigger = dirt.gameObject.GetComponent<EventTrigger>();
                if (trigger == null)
                {
                    trigger = dirt.gameObject.AddComponent<EventTrigger>();
                }

                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.Drag;
                entry.callback.AddListener((data) => { CleanDirt(dirt); });
                trigger.triggers.Add(entry);
            }
        }
    }

    private void CleanDirt(Image dirt)
    {
        if (!dirtAlphaMap.ContainsKey(dirt) || !dirt.gameObject.activeSelf) return;

        dirtAlphaMap[dirt] -= Time.deltaTime * cleanSpeed;

        Color color = dirt.color;
        color.a = Mathf.Clamp01(dirtAlphaMap[dirt]);
        dirt.color = color;

        if (color.a <= 0.05f)
        {
            dirt.gameObject.SetActive(false);
            CheckMissionComplete();
        }
    }

    private void CheckMissionComplete()
    {
        bool allClean = true;
        foreach (Image dirt in dirtImages)
        {
            if (dirt != null && dirt.gameObject.activeSelf)
            {
                allClean = false;
                break;
            }
        }

        if (allClean)
        {
            Debug.Log("Wine Glass Cleaning Mission Success!");
        }
    }
}