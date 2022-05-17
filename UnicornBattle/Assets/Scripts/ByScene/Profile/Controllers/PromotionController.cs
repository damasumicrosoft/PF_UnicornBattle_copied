using PlayFab;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PromotionController : MonoBehaviour
{
    public Transform AdSlotCounterBar;

    private List<LayoutElement> adSlots = new List<LayoutElement>();


    public GameObject AdObject, PrvAd, NextAd, PlayEventBtn, ViewSaleBtn, WatchAdBtn;

    public LayoutElement SlotEmpty;
    public Sprite SlotSelected;
    public Image PromoBanner;
    public Texture2D defaultVideoBanner;
    public Text selectedTitle, selectedDesc;

    private float _timeSinceMove = 99999999f;
    private float rotateDelay = 8f;
    private float _watchCD = 0.5f;
    private float _watchLastClickedAt = 0;
    private int displayPromoIndex = 0;

    private LevelPicker _levelPicker;

    // Use this for initialization
    void Start()
    {
    }

    void OnEnable()
    {
        // SupersonicEvents.OnAdRewarded += EvaluateAdState;
        InvokeRepeating("EvaluateAdState", 60, 300); //start after 1m, repeat every 5m
    }

    void OnDisable()
    {
        // SupersonicEvents.OnAdRewarded -= EvaluateAdState;
        CancelInvoke("EvaluateAdState");
    }
}
