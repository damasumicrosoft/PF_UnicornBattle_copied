using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DialogCanvasController : Singleton<DialogCanvasController>
{
    public enum InventoryFilters { AllItems, UsableInCombat, Keys, Containers }

    protected DialogCanvasController() { } // guarantee this will be always a singleton only - can't use the constructor!

    public Button openDialogsButton;
    public Button closeDialogsButton;
    public Transform overlayTint;
    public ErrorPromptController errorPrompt;
    public ConfirmationPromptController confirmPrompt;
    public LoadingPromptController loadingPrompt;
    public TextInputPrompController textInputPrompt;
    public InterstitialController interstitialPrompt;
    public SelectorPromptController selectorPrompt;
    public ItemViewerController itemViewerPrompt;
    public FloatingStoreController floatingStorePrompt;
    public FloatingInventoryController floatingInvPrompt;
    public LeaderboardPaneController socialPrompt;
    public AccountStatusController accountSettingsPrompt;

    public StatusPromptController statusPrompt;

    public bool showOpenCloseButton = true;
    private List<OutgoingAPICounter> waitingOnRequests = new List<OutgoingAPICounter>();
    //Coroutine to manage the 10 second timeout.
    private Coroutine _timeOutCallback;
    private float timeOutLength = 10f;

    public delegate void LoadingPromptHandler(PlayFabAPIMethods method);
    public static event LoadingPromptHandler RaiseLoadingPromptRequest;

    public delegate void ConfirmationPromptHandler(string title, string message, Action<bool> responseCallback);
    public static event ConfirmationPromptHandler RaiseConfirmationPromptRequest;

    public delegate void StatusPromptHandler(string title, string message, Action responseCallback);
    public static event StatusPromptHandler RaiseStatusPromptRequest;


    public delegate void TextInputPromptHandler(string title, string message, Action<string> responseCallback, string defaultValue = null);
    public static event TextInputPromptHandler RaiseTextInputPromptRequest;

    public delegate void SelectorPromptHandler(string title, List<string> options, UnityAction<int> responseCallback);
    public static event SelectorPromptHandler RaiseSelectorPromptRequest;

    public delegate void InterstitialRequestHandler();
    public static event InterstitialRequestHandler RaiseInterstitialRequest;

    public delegate void StoreRequestHandler(string storeID);
    public static event StoreRequestHandler RaiseStoreRequest;

    public delegate void ItemViewRequestHandler(List<string> items);
    public static event ItemViewRequestHandler RaiseItemViewRequest;

    public delegate void InventoryPromptHandler(Action<string> responseCallback, InventoryFilters filter);
    public static event InventoryPromptHandler RaiseInventoryPromptRequest;

    public delegate void RequestAccountSettingsHandler();
    public static event RequestAccountSettingsHandler RaiseAccountSettingsRequest;

    public delegate void RequestSocialHandler();
    public static event RequestSocialHandler RaiseSocialRequest;

    void OnEnable()
    {
        PF_Bridge.OnPlayFabCallbackError += HandleCallbackError;
        PF_Bridge.OnPlayfabCallbackSuccess += HandleCallbackSuccess;

        PF_Authentication.OnLoginFail += HandleOnLoginFail;
        PF_Authentication.OnLoginSuccess += HandleOnLoginSuccess;

        RaiseLoadingPromptRequest += HandleLoadingPromptRequest;
        RaiseConfirmationPromptRequest += HandleConfirmationPromptRequest;
        RaiseStatusPromptRequest += HandleStatusPromptRequest;
        RaiseTextInputPromptRequest += HandleTextInputRequest;
        RaiseInterstitialRequest += HandleInterstitialRequest;
        RaiseStoreRequest += HandleStoreRequest;
        RaiseItemViewRequest += HandleItemViewerRequest;
        RaiseInventoryPromptRequest += HandleInventoryRequest;
        RaiseAccountSettingsRequest += HandleRaiseAccountSettingsRequest;
        RaiseSelectorPromptRequest += HandleSelectorPromptRequest;
        RaiseSocialRequest += HandleSocialRequest;
    }

    void OnDisable()
    {
        PF_Bridge.OnPlayFabCallbackError -= HandleCallbackError;
        PF_Bridge.OnPlayfabCallbackSuccess -= HandleCallbackSuccess;

        PF_Authentication.OnLoginFail -= HandleOnLoginFail;
        PF_Authentication.OnLoginSuccess -= HandleOnLoginSuccess;

        RaiseLoadingPromptRequest -= HandleLoadingPromptRequest;
        RaiseConfirmationPromptRequest -= HandleConfirmationPromptRequest;
        RaiseStatusPromptRequest -= HandleStatusPromptRequest;
        RaiseTextInputPromptRequest -= HandleTextInputRequest;
        RaiseInterstitialRequest -= HandleInterstitialRequest;
        RaiseStoreRequest -= HandleStoreRequest;
        RaiseItemViewRequest -= HandleItemViewerRequest;
        RaiseInventoryPromptRequest -= HandleInventoryRequest;
        RaiseAccountSettingsRequest -= HandleRaiseAccountSettingsRequest;
        RaiseSelectorPromptRequest -= HandleSelectorPromptRequest;
        RaiseSocialRequest -= HandleSocialRequest;
        
        

    }

    void HandleSocialRequest()
    {
        socialPrompt.gameObject.SetActive(true);
        socialPrompt.Init();
    }

    public static void RequestSocialPrompt()
    {
        if (RaiseSocialRequest != null)
            RaiseSocialRequest();
    }

    void HandleSelectorPromptRequest(string title, List<string> options, UnityAction<int> responseCallback)
    {
        selectorPrompt.gameObject.SetActive(true);
        selectorPrompt.InitSelector(title, options, responseCallback);
    }

    public static void RequestSelectorPrompt(string title, List<string> options, UnityAction<int> responseCallback)
    {
        if (RaiseSelectorPromptRequest != null)
            RaiseSelectorPromptRequest(title, options, responseCallback);
    }

    public static void RequestAccountSettings()
    {
        if (RaiseAccountSettingsRequest != null)
            RaiseAccountSettingsRequest();
    }

    void HandleRaiseAccountSettingsRequest()
    {
        accountSettingsPrompt.gameObject.SetActive(true);
        accountSettingsPrompt.Init();
    }

    public static void RequestInventoryPrompt(Action<string> callback = null, InventoryFilters filter = InventoryFilters.AllItems)
    {
        if (RaiseInventoryPromptRequest != null)
            RaiseInventoryPromptRequest(callback, filter);
    }

    void HandleInventoryRequest(Action<string> callback = null, InventoryFilters filter = InventoryFilters.AllItems)
    {

        Action afterGetInventory = () =>
        {
            // ENABLE THIS AFTER WE HAVE A CONSISTENT WAY TO HIDE TINTS
            //ShowTint();
            floatingInvPrompt.Init(callback, filter);
        };

        PF_PlayerData.GetUserInventory(afterGetInventory);
    }

    public static void RequestItemViewer(List<string> items)
    {
        if (RaiseItemViewRequest != null)
            RaiseItemViewRequest(items);
    }

    void HandleItemViewerRequest(List<string> items)
    {
        itemViewerPrompt.InitiateViewer(items);
    }

    public static void RequestStore(string storeId)
    {
        if (RaiseStoreRequest != null)
            RaiseStoreRequest(storeId);
    }

    void HandleStoreRequest(string storeId)
    {
        PF_GamePlay.RetrieveStoreItems(storeId, floatingStorePrompt.InitiateStore);
    }

    public static void RequestInterstitial()
    {
        if (RaiseInterstitialRequest != null)
            RaiseInterstitialRequest();
    }

    public void HandleInterstitialRequest()
    {
        interstitialPrompt.ShowInterstitial();
    }

    public static void RequestTextInputPrompt(string title, string message, Action<string> responseCallback, string defaultValue = null)
    {
        if (RaiseTextInputPromptRequest != null)
            RaiseTextInputPromptRequest(title, message, responseCallback, defaultValue);
    }

    public void HandleTextInputRequest(string title, string message, Action<string> responseCallback, string defaultValue)
    {
        //ShowTint();
        textInputPrompt.ShowTextInputPrompt(title, message, responseCallback, defaultValue);
    }


    public static void RequestConfirmationPrompt(string title, string message, Action<bool> responseCallback)
    {
        if (RaiseConfirmationPromptRequest != null)
            RaiseConfirmationPromptRequest(title, message, responseCallback);
    }

    public void HandleConfirmationPromptRequest(string title, string message, Action<bool> responseCallback)
    {
        //ShowTint();
        confirmPrompt.ShowConfirmationPrompt(title, message, responseCallback, HideTint);
    }

    public static void RequestStatusPrompt(string title, string message, Action responseCallback)
    {
        if (RaiseStatusPromptRequest != null)
            RaiseStatusPromptRequest(title, message, responseCallback);
    }

    public void HandleStatusPromptRequest(string title, string message, Action responseCallback)
    {
        //ShowTint();
        statusPrompt.ShowMessagePrompt(title, message, responseCallback);        
    }
    public static void RequestLoadingPrompt(PlayFabAPIMethods method)
    {
        if (RaiseLoadingPromptRequest != null)
            RaiseLoadingPromptRequest(method);
    }

    public void HandleLoadingPromptRequest(PlayFabAPIMethods method)
    {
        if (waitingOnRequests.Count == 0)
        {
            //ShowTint();
            loadingPrompt.RaiseLoadingPrompt();
        }
        waitingOnRequests.Add(new OutgoingAPICounter { method = method, outgoingGameTime = Time.time });

        if (_timeOutCallback == null)
        {
            _timeOutCallback = StartCoroutine(OutgoingApiTimeoutCallback());
        }
    }

    public void CloseLoadingPrompt(PlayFabAPIMethods method)
    {
        List<OutgoingAPICounter> waiting = waitingOnRequests.FindAll((i) => { return i.method == method; });

        OutgoingAPICounter itemToRemove = null;

        for (int z = 0; z < waiting.Count; z++)
        {
            // in absence of a true GUID request system, we will get the oldest requests to prevent timeouts
            if (itemToRemove != null && waiting[z].outgoingGameTime > itemToRemove.outgoingGameTime)
            {
                // shouldnt be too many times where there are multiple requests of the same type.
                itemToRemove = waiting[z];
            }
            else if (itemToRemove == null)
            {
                //first and likly only match
                itemToRemove = waiting[z];
            }
        }

        if (itemToRemove != null)
        {
            waitingOnRequests.Remove(itemToRemove);
            HideTint();
            loadingPrompt.CloseLoadingPrompt();
        }
    }

    public void CloseLoadingPromptAfterError()
    {
        waitingOnRequests.Clear();
        loadingPrompt.CloseLoadingPrompt();
    }

    void HandleOnLoginSuccess(string message, MessageDisplayStyle style)
    {
        HandleCallbackSuccess(message, PlayFabAPIMethods.GenericLogin, style);
    }

    void HandleOnLoginFail(string message, MessageDisplayStyle style)
    {
        HandleCallbackError(message, PlayFabAPIMethods.GenericLogin, style);
    }

    public void HandleCallbackError(string details, PlayFabAPIMethods method, MessageDisplayStyle style)
    {
        switch (style)
        {
            case MessageDisplayStyle.error:
                var errorMessage = string.Format("CALLBACK ERROR: {0}: {1}", method, details);
                //ShowTint();
                errorPrompt.RaiseErrorDialog(errorMessage);
                CloseLoadingPromptAfterError();
                break;

            default:
                CloseLoadingPrompt(method);
                Debug.Log(string.Format("CALLBACK ERROR: {0}: {1}", method, details));
                break;
        }
    }

    public void HandleCallbackSuccess(string details, PlayFabAPIMethods method, MessageDisplayStyle style)
    {
        CloseLoadingPrompt(method);
    }

    public void ShowTint()
    {
        overlayTint.gameObject.SetActive(true);
    }

    public void HideTint()
    {
        overlayTint.gameObject.SetActive(false);
    }


    public void OpenDialogsMenu()
    {
        ToggleOpenCloseButtons();
    }

    // NEED TO MAKE THIS A COROUTINE
    public void CloseDialogsMenu()
    {
        HideTint();
        ToggleOpenCloseButtons();
    }

    public void CloseErrorDialog()
    {
        errorPrompt.CloseErrorDialog();
        HideTint();
    }

    public void ToggleOpenCloseButtons()
    {
        if (showOpenCloseButton)
        {
            if (openDialogsButton.gameObject.activeSelf)
            {
                openDialogsButton.gameObject.SetActive(false);
                closeDialogsButton.gameObject.SetActive(true);
            }
            else
            {
                openDialogsButton.gameObject.SetActive(true);
                closeDialogsButton.gameObject.SetActive(false);
            }
        }
    }

    //Loading time-out co-routine management code
    private IEnumerator OutgoingApiTimeoutCallback()
    {
        while (waitingOnRequests.Count > 0)
        {
            for (var z = 0; z < waitingOnRequests.Count; z++)
            {
                if (Time.time > (waitingOnRequests[z].outgoingGameTime + timeOutLength))
                {
                    // time has elapsed for this request, until we can handle this more specifically, we can only reload the scene, and hope for the best.
                    var capturedDetails = waitingOnRequests[z].method;
                    PF_Bridge.RaiseCallbackError(string.Format("API Call: {0} Timed out after {1} seconds.", capturedDetails, timeOutLength), waitingOnRequests[z].method, MessageDisplayStyle.error);

                    Action<bool> afterConfirmation = response =>
                    {
                        if (response == false)
                        {
                            // user clicked cancel (to reload);
                            Debug.LogErrorFormat("Reloading scene due {0} API timing out.", capturedDetails);
                            SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
                        }
                    };

                    RequestConfirmationPrompt("Caution! Bravery Required!", string.Format("API Call: {0} Timed out. \n\tACCEPT: To proceed, may cause client instability. \n\tCANCEL: To reload this scene and hope for the best.", capturedDetails), afterConfirmation);
                }
            }

            // tick once per second while we have outbound requests. (keep enabled while debugging this feature)
            Debug.Log((int)Time.time % 2 == 0 ? "Tick" : "Tock");
            yield return new WaitForSeconds(1f);
        }

        // outgoing request queue empty
        _timeOutCallback = null;
    }
}
