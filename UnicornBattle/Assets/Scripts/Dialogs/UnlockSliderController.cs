using PlayFab.ClientModels;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UnlockSliderController : MonoBehaviour, IPointerUpHandler
{
    public Slider uiSlider;
    public Text sliderMessage;
    public Button storeButton;
    public Image endIcon;
    public Image handle;
    public ItemViewerController controller;
    public Text ItemDescription;

    private float slideDelay = 0.333f;
    private float resistance = 0.05f;
    private UnityAction<UnlockContainerItemResult> afterUnlock;

    void OnEnable()
    {
        // TODO clear listeners?
        uiSlider.value = uiSlider.minValue;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!Mathf.Approximately(uiSlider.value, uiSlider.maxValue))
            StartCoroutine(SlideBack(slideDelay));
        else if (Mathf.Approximately(uiSlider.value, uiSlider.maxValue))
            CheckUnlock();
    }

    public IEnumerator SlideBack(float delay)
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);

        while (!Mathf.Approximately(uiSlider.value, uiSlider.minValue))
        {
            uiSlider.value -= resistance;
            yield return new WaitForEndOfFrame();
        }
        handle.color = Color.white;
    }

    public void SetupSlider(UnityAction<UnlockContainerItemResult> callback = null)
    {
        afterUnlock = callback;
        ItemDescription.text = controller.selectedItem.Description;

        if (controller.selectedItem.Container != null)
        {
            var keyId = controller.selectedItem.Container.KeyItemId;
            var keyReference = PF_GameData.GetCatalogItemById(keyId);
            endIcon.gameObject.SetActive(keyReference != null);
            if (keyReference != null)
            {
                var chestQty = PF_PlayerData.GetItemQty(controller.selectedItem.ItemId);
                var keyQty = PF_PlayerData.GetItemQty(keyId);
                var useColor = (chestQty > 0 && keyQty > 0) ? Color.cyan : Color.red;

                endIcon.color = useColor;
                sliderMessage.text = string.Format("{0} Required ({1} available)", keyReference.DisplayName, Mathf.Min(chestQty, keyQty));
                sliderMessage.color = useColor;

                var iconName = PF_GameData.GetIconByItemById(keyReference.ItemId, GlobalStrings.BRONZE_KEY_ICON);
                var icon = GameController.Instance.iconManager.GetIconById(iconName, IconManager.IconTypes.Item);
                handle.overrideSprite = icon;
            }
            else
            {
                handle.overrideSprite = GameController.Instance.iconManager.GetIconById(GlobalStrings.DARKSTONE_LOCK_ICON, IconManager.IconTypes.Misc);
                sliderMessage.text = GlobalStrings.UNLOCKED_MSG;
            }
        }
        else
        {
            sliderMessage.text = GlobalStrings.UNLOCKED_MSG;
            // set default key icon or lock or something...
        }
    }

    public void CheckUnlock()
    {
        var chestItemId = controller.selectedItem.ItemId;
        PF_GameData.TryOpenContainer(chestItemId, afterUnlock);
    }
}
