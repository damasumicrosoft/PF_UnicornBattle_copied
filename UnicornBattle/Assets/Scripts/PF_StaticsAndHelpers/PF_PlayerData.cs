using Facebook.Unity;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// PlayerData contains all the PlayFab API calls that relate to manipulating and 
/// </summary>
public static class PF_PlayerData
{
    // Player Level Data:
    public static string PlayerId = string.Empty;
    public static bool showAccountOptionsOnLogin = true;
    public static bool isRegisteredForPush = false;
    public static bool isPlayFabRegistered { get { return accountInfo != null && accountInfo.PrivateInfo != null && !string.IsNullOrEmpty(accountInfo.Username) && !string.IsNullOrEmpty(accountInfo.PrivateInfo.Email); } }
    public static UserAccountInfo accountInfo;
    public static readonly Dictionary<string, UserDataRecord> UserData = new Dictionary<string, UserDataRecord>();

    // this is a sorted, collated structure built from playerInventory. By default, this will only grab items that are in the primary catalog
    public static readonly Dictionary<string, InventoryCategory> inventoryByCategory = new Dictionary<string, InventoryCategory>();
    public static readonly Dictionary<string, int> virtualCurrency = new Dictionary<string, int>();
    public static readonly List<ItemInstance> playerInventory = new List<ItemInstance>();
    public static readonly Dictionary<string, int> userStatistics = new Dictionary<string, int>();

    //aggregation of player characters
    public static readonly List<CharacterResult> playerCharacters = new List<CharacterResult>();
    public static readonly Dictionary<string, UB_CharacterData> playerCharacterData = new Dictionary<string, UB_CharacterData>();
    public static readonly Dictionary<string, List<string>> characterAchievements = new Dictionary<string, List<string>>();
    public static readonly Dictionary<string, Dictionary<string, int>> characterStatistics = new Dictionary<string, Dictionary<string, int>>();

    public static readonly List<FriendInfo> playerFriends = new List<FriendInfo>();

    public enum PlayerClassTypes { Bucephelous = 0, Nightmare = 1, PegaZeus = 2 }

    // The current character being played:
    public static UB_SavedCharacter activeCharacter = null;

    #region User Data
    public static void GetUserData(List<string> keys, UnityAction<GetUserDataResult> callback = null)
    {
        var request = new GetUserDataRequest
        {
            Keys = keys,
            PlayFabId = PlayerId,
        };

        //DialogCanvasController.RequestLoadingPrompt (PlayFabAPIMethods.GetUserData);
        PlayFabClientAPI.GetUserReadOnlyData(request, result =>
        {
            if (callback != null)
                callback(result);
            PF_Bridge.RaiseCallbackSuccess(string.Empty, PlayFabAPIMethods.GetUserData, MessageDisplayStyle.none);
        }, PF_Bridge.PlayFabErrorCallback);
    }

    public static void UpdateUserData(Dictionary<string, string> updates, string permission = "Public", UnityAction<UpdateUserDataResult> callback = null)
    {
        var request = new UpdateUserDataRequest
        {
            Data = updates,
            Permission = (UserDataPermission)Enum.Parse(typeof(UserDataPermission), permission),
        };

        DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.UpdateUserData);
        PlayFabClientAPI.UpdateUserData(request, result =>
        {
            if (callback != null)
                callback(result);
            PF_Bridge.RaiseCallbackSuccess(string.Empty, PlayFabAPIMethods.UpdateUserData, MessageDisplayStyle.none);
        }, PF_Bridge.PlayFabErrorCallback);
    }

    public static void GetUserInventory(Action callback = null)
    {
        DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.GetUserInventory);
        PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(), (GetUserInventoryResult result) =>
        {
            virtualCurrency.Clear();
            playerInventory.Clear();

            foreach (var pair in result.VirtualCurrency)
                virtualCurrency.Add(pair.Key, pair.Value);
            foreach (var eachItem in result.Inventory)
                playerInventory.Add(eachItem);
            inventoryByCategory.Clear();

            if (PF_GameData.catalogItems.Count > 0)
            {
                foreach (var item in playerInventory)
                {
                    if (inventoryByCategory.ContainsKey(item.ItemId))
                        continue;

                    var catalogItem = PF_GameData.GetCatalogItemById(item.ItemId);
                    if (catalogItem == null)
                        continue;

                    var items = playerInventory.FindAll(x => { return x.ItemId.Equals(item.ItemId); });
                    var customIcon = PF_GameData.GetIconByItemById(catalogItem.ItemId);
                    var icon = GameController.Instance.iconManager.GetIconById(customIcon, IconManager.IconTypes.Item);
                    inventoryByCategory.Add(item.ItemId, new InventoryCategory(item.ItemId, catalogItem, items, icon));
                }
            }

            if (callback != null)
                callback();

            PF_Bridge.RaiseCallbackSuccess("", PlayFabAPIMethods.GetUserInventory, MessageDisplayStyle.none);
        }, PF_Bridge.PlayFabErrorCallback);
    }
    #endregion

    #region User Statistics
    public static void GetUserStatistics()
    {
        GetPlayerStatisticsRequest request = new GetPlayerStatisticsRequest();
        PlayFabClientAPI.GetPlayerStatistics(request, OnGetUserStatisticsSuccess, OnGetUserStatisticsError);
    }

    private static void OnGetUserStatisticsSuccess(GetPlayerStatisticsResult result)
    {
        //TODO update to use new 

        PF_Bridge.RaiseCallbackSuccess("", PlayFabAPIMethods.GetUserStatistics, MessageDisplayStyle.none);
        foreach (var each in result.Statistics)
            userStatistics[each.StatisticName] = each.Value;
    }

    private static void OnGetUserStatisticsError(PlayFabError error)
    {
        PF_Bridge.RaiseCallbackError(error.ErrorMessage, PlayFabAPIMethods.GetUserStatistics, MessageDisplayStyle.error);
    }

    public static void UpdateUserStatistics(Dictionary<string, int> updates)
    {
        var statistics = new List<StatisticUpdate>();
        foreach (var eachUpdate in updates) // Copy the stats from the inputs to the request
        {
            int eachStat;
            userStatistics.TryGetValue(eachUpdate.Key, out eachStat);
            statistics.Add(new StatisticUpdate { StatisticName = eachUpdate.Key, Value = eachUpdate.Value }); // Send the value to the server
            userStatistics[eachUpdate.Key] = eachStat + eachUpdate.Value; // Update the local cache so that future updates are using correct values
        }

        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "SetPlayerStats",
            FunctionParameter = new { statistics = statistics },
            GeneratePlayStreamEvent = true
        };
        PlayFabClientAPI.ExecuteCloudScript(request, OnUpdateUserStatisticsSuccess, PF_Bridge.PlayFabErrorCallback);
    }

    private static void OnUpdateUserStatisticsSuccess(ExecuteCloudScriptResult result)
    {
        if (!PF_Bridge.VerifyErrorFreeCloudScriptResult(result))
            return;

        PF_Bridge.RaiseCallbackSuccess("User Statistics Uploaded", PlayFabAPIMethods.UpdateUserStatistics, MessageDisplayStyle.none);
        GetCharacterStatistics(); // Refresh stats that we just updated
    }

    #endregion

    #region User Account APIs
    public static void GetUserAccountInfo()
    {
        var request = new GetPlayerCombinedInfoRequest
        {
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams { GetUserData = true, GetUserReadOnlyData = true, GetUserInventory = true, GetUserVirtualCurrency = true, GetUserAccountInfo = true, GetPlayerStatistics = true }
        };

        DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.GetAccountInfo);
        PlayFabClientAPI.GetPlayerCombinedInfo(request, OnGetUserAccountInfoSuccess, PF_Bridge.PlayFabErrorCallback);
    }

    private static void OnGetUserAccountInfoSuccess(GetPlayerCombinedInfoResult result)
    {
        playerInventory.Clear();
        foreach (var eachItem in result.InfoResultPayload.UserInventory)
            playerInventory.Add(eachItem);
        accountInfo = result.InfoResultPayload.AccountInfo;

        if (result.InfoResultPayload.UserData.ContainsKey("IsRegisteredForPush"))
            isRegisteredForPush = result.InfoResultPayload.UserData["IsRegisteredForPush"].Value == "1";
        else
            isRegisteredForPush = false;

        if (result.InfoResultPayload.UserData.ContainsKey("ShowAccountOptionsOnLogin") && result.InfoResultPayload.UserData["ShowAccountOptionsOnLogin"].Value == "0")
        {
            showAccountOptionsOnLogin = false;
        }
        else //if (PF_Authentication.hasLoggedInOnce == false) 
        {
            //PF_Authentication.hasLoggedInOnce = true;
            DialogCanvasController.RequestAccountSettings();
        }

        inventoryByCategory.Clear();
        if (PF_GameData.catalogItems.Count > 0)
        {
            foreach (var item in playerInventory)
            {
                if (inventoryByCategory.ContainsKey(item.ItemId))
                    continue;

                var catalogItem = PF_GameData.GetCatalogItemById(item.ItemId);
                if (catalogItem == null)
                    continue;

                var items = new List<ItemInstance>(playerInventory.FindAll((x) => { return x.ItemId.Equals(item.ItemId); }));
                var customIcon = PF_GameData.GetIconByItemById(catalogItem.ItemId);
                var icon = GameController.Instance.iconManager.GetIconById(customIcon, IconManager.IconTypes.Item);
                inventoryByCategory.Add(item.ItemId, new InventoryCategory(item.ItemId, catalogItem, items, icon));
            }
        }

        if (PF_Authentication.GetDeviceId(true))
        {
            Debug.Log("Mobile Device ID Found!");

            var deviceId = string.IsNullOrEmpty(PF_Authentication.android_id) ? PF_Authentication.ios_id : PF_Authentication.android_id;
            PlayerPrefs.SetString("LastDeviceIdUsed", deviceId);
        }
        else
        {
            Debug.Log("Custom Device ID Found!");

            if (string.IsNullOrEmpty(PF_Authentication.custom_id))
                PlayerPrefs.SetString("LastDeviceIdUsed", PF_Authentication.custom_id);
        }

        virtualCurrency.Clear();
        foreach (var eachPair in result.InfoResultPayload.UserVirtualCurrency)
            virtualCurrency.Add(eachPair.Key, eachPair.Value);

        PF_Bridge.RaiseCallbackSuccess("Player Account Info Loaded", PlayFabAPIMethods.GetAccountInfo, MessageDisplayStyle.none);
    }
    #endregion

    #region Character APIs
    public static void GetCharacterData()
    {
        var JsonUtil = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);

        playerCharacterData.Clear();
        characterAchievements.Clear();

        var remainingCallbacks = playerCharacters.Count;

        if (remainingCallbacks == 0)
        {
            PF_Bridge.RaiseCallbackSuccess("", PlayFabAPIMethods.GetCharacterReadOnlyData, MessageDisplayStyle.none);
            return;
        }
        else
        {
            DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.GetCharacterReadOnlyData);
        }


        foreach (var character in playerCharacters)
        {
            var request = new GetCharacterDataRequest
            {
                PlayFabId = PlayerId,
                CharacterId = character.CharacterId,
                Keys = new List<string> { "CharacterData", "Achievements" }
            };

            PlayFabClientAPI.GetCharacterReadOnlyData(request, (result) =>
            {
                if (result.Data.ContainsKey("Achievements"))
                    characterAchievements.Add(result.CharacterId, JsonUtil.DeserializeObject<List<string>>(result.Data["Achievements"].Value));

                if (!result.Data.ContainsKey("CharacterData"))
                    return;

                playerCharacterData.Add(result.CharacterId, JsonUtil.DeserializeObject<UB_CharacterData>(result.Data["CharacterData"].Value));
                remainingCallbacks--;
                if (remainingCallbacks == 0)
                    PF_Bridge.RaiseCallbackSuccess("", PlayFabAPIMethods.GetCharacterReadOnlyData, MessageDisplayStyle.none);
            }, PF_Bridge.PlayFabErrorCallback);
        }
    }

    public static void GetCharacterDataById(string characterId)
    {
        var JsonUtil = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);

        DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.GetCharacterReadOnlyData);

        var request = new GetCharacterDataRequest
        {
            PlayFabId = PlayerId,
            CharacterId = characterId,
            Keys = new List<string> { "CharacterData" }
        };

        PlayFabClientAPI.GetCharacterReadOnlyData(request, result =>
        {
            if (result.Data.ContainsKey("CharacterData"))
            {
                playerCharacterData[result.CharacterId] = JsonUtil.DeserializeObject<UB_CharacterData>(result.Data["CharacterData"].Value);
                PF_Bridge.RaiseCallbackSuccess("", PlayFabAPIMethods.GetCharacterReadOnlyData, MessageDisplayStyle.none);
            }
        }, PF_Bridge.PlayFabErrorCallback);
    }

    public static bool DoesCharacterHaveAchievement(string charId, string achvId)
    {
        List<string> achievements;
        characterAchievements.TryGetValue(charId, out achievements);
        if (achievements == null || achievements.Count == 0)
            return false;
        return achievements.Any(i => { return string.Equals(i, achvId); });
    }

    public static void GetCharacterStatistics()
    {
        foreach (var each in playerCharacters)
        {
            var request = new GetCharacterStatisticsRequest { CharacterId = each.CharacterId };
            PlayFabClientAPI.GetCharacterStatistics(request, OnGetCharacterStatisticsSuccess, PF_Bridge.PlayFabErrorCallback);
        }
    }

    private static void OnGetCharacterStatisticsSuccess(GetCharacterStatisticsResult result)
    {
        var characterId = ((GetCharacterStatisticsRequest)result.Request).CharacterId;
        Dictionary<string, int> activeStats;
        if (!characterStatistics.TryGetValue(characterId, out activeStats))
        {
            activeStats = new Dictionary<string, int>();
            characterStatistics[characterId] = activeStats;
        }
        activeStats.Clear();

        foreach (var statPair in result.CharacterStatistics)
            activeStats.Add(statPair.Key, statPair.Value);

        if (characterStatistics.Count == playerCharacters.Count)
            PF_Bridge.RaiseCallbackSuccess("", PlayFabAPIMethods.GetCharacterStatistics, MessageDisplayStyle.none);
    }

    public static void UpdateCharacterStatistics(string characterId, Dictionary<string, int> updates)
    {
        Dictionary<string, int> activeStats;
        if (!characterStatistics.TryGetValue(characterId, out activeStats))
            return; ;

        foreach (var each in updates)
        {
            int temp;
            activeStats.TryGetValue(each.Key, out temp);
            activeStats[each.Key] = temp + each.Value;
        }

        DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.UpdateCharacterStatistics);

        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "SetCharacterStats",
            FunctionParameter = new { characterId = characterId, statistics = activeStats },
            GeneratePlayStreamEvent = true
        };
        PlayFabClientAPI.ExecuteCloudScript(request, OnUpdateCharacterStatisticsSuccess, PF_Bridge.PlayFabErrorCallback);
    }

    private static void OnUpdateCharacterStatisticsSuccess(ExecuteCloudScriptResult result)
    {
        if (!PF_Bridge.VerifyErrorFreeCloudScriptResult(result))
            return;

        PF_Bridge.RaiseCallbackSuccess("", PlayFabAPIMethods.UpdateCharacterStatistics, MessageDisplayStyle.none);
    }

    public static void GetPlayerCharacters()
    {
        var request = new ListUsersCharactersRequest();
        PlayFabClientAPI.GetAllUsersCharacters(request, OnGetPlayerCharactersSuccess, PF_Bridge.PlayFabErrorCallback);
    }

    private static void OnGetPlayerCharactersSuccess(ListUsersCharactersResult result)
    {
        playerCharacters.Clear();
        foreach (var eachChar in result.Characters)
            playerCharacters.Add(eachChar);
        PF_Bridge.RaiseCallbackSuccess("Player Characters Retrieved", PlayFabAPIMethods.GetAllUsersCharacters, MessageDisplayStyle.none);
    }

    public static void CreateNewCharacter(string name, UB_ClassDetail details)
    {
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "CreateCharacter",
            FunctionParameter = new { catalogCode = details.CatalogCode, characterName = name }
        };
        PlayFabClientAPI.ExecuteCloudScript(request, OnCreateNewCharacterSuccess, PF_Bridge.PlayFabErrorCallback);
    }

    private static void OnCreateNewCharacterSuccess(ExecuteCloudScriptResult result)
    {
        if (!PF_Bridge.VerifyErrorFreeCloudScriptResult(result))
            return;

        if ((bool)result.FunctionResult)
            PF_Bridge.RaiseCallbackSuccess("New Character Added", PlayFabAPIMethods.GrantCharacterToUser, MessageDisplayStyle.none);
        else
            PF_Bridge.RaiseCallbackError("Error Creating Character" + result.Logs.ToString(), PlayFabAPIMethods.GrantCharacterToUser, MessageDisplayStyle.error);
    }

    public static void DeleteCharacter(string cid)
    {
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "DeleteCharacter",
            FunctionParameter = new { characterId = cid }
        };
        PlayFabClientAPI.ExecuteCloudScript(request, OnDeleteCharacterSuccess, PF_Bridge.PlayFabErrorCallback);
    }

    private static void OnDeleteCharacterSuccess(ExecuteCloudScriptResult result)
    {
        if (!PF_Bridge.VerifyErrorFreeCloudScriptResult(result))
            return;

        if ((bool)result.FunctionResult)
            PF_Bridge.RaiseCallbackSuccess("Character Deleted", PlayFabAPIMethods.DeleteCharacter, MessageDisplayStyle.none);
        else
            PF_Bridge.RaiseCallbackError("Error Deleting Character" + result.Logs.ToString(), PlayFabAPIMethods.DeleteCharacter, MessageDisplayStyle.none);
    }

    public static void UpdateActiveCharacterData()
    {
        var id = activeCharacter.characterDetails.CharacterId;
        UB_CharacterData cData;
        playerCharacterData.TryGetValue(id, out cData);

        if (cData != null)
            activeCharacter.characterData = cData;

        activeCharacter.RefillVitals();
    }
    #endregion

    #region Inventory Utilities
    /// <summary>
    /// Return number of RemainingUses of an stack of itemIds in your inventory
    /// </summary>
    /// <returns>
    /// -1 => Item does not exist in the inventory
    /// 0 => The item has infinite uses
    /// else, the number of remaining uses
    /// </returns>
    public static int GetItemQty(string itemId)
    {
        var output = 0;
        foreach (var eachItem in playerInventory)
        {
            if (eachItem.ItemId != itemId)
                continue;
            if (eachItem.RemainingUses == null)
                return -1; // Unlimited uses
            if (eachItem.RemainingUses.Value > 0) // Non-Positive is probably a PlayFab api error
                output += eachItem.RemainingUses.Value;
        }
        return output;
    }
    #endregion Inventory Utilities

    #region Friend APIs
    public static void GetFriendsList(UnityAction callback = null)
    {
        var request = new GetFriendsListRequest
        {
            IncludeFacebookFriends = true,
            IncludeSteamFriends = false
        };

        //DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.GetFriendList);
        PlayFabClientAPI.GetFriendsList(request, result =>
        {
            playerFriends.Clear();
            foreach (var eachFriend in result.Friends)
                playerFriends.Add(eachFriend);
            if (callback != null)
                callback();
            PF_Bridge.RaiseCallbackSuccess(string.Empty, PlayFabAPIMethods.GetFriendList, MessageDisplayStyle.none);
        }, PF_Bridge.PlayFabErrorCallback);
    }

    public enum AddFriendMethod { DisplayName, Email, Username, PlayFabID }

    public static void AddFriend(string input, AddFriendMethod method, UnityAction<bool> callback = null)
    {
        var request = new AddFriendRequest();
        switch (method)
        {
            case AddFriendMethod.DisplayName:
                request.FriendTitleDisplayName = input; break;
            case AddFriendMethod.Email:
                request.FriendEmail = input; break;
            case AddFriendMethod.Username:
                request.FriendUsername = input; break;
            case AddFriendMethod.PlayFabID:
                request.FriendPlayFabId = input; break;
        }

        DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.AddFriend);
        PlayFabClientAPI.AddFriend(request, result =>
        {
            if (callback != null)
                callback(result.Created);
            PF_Bridge.RaiseCallbackSuccess(string.Empty, PlayFabAPIMethods.AddFriend, MessageDisplayStyle.none);
        }, PF_Bridge.PlayFabErrorCallback);
    }

    public static void RemoveFriend(string id, UnityAction callback = null)
    {
        var request = new RemoveFriendRequest { FriendPlayFabId = id };

        DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.RemoveFriend);
        PlayFabClientAPI.RemoveFriend(request, result =>
        {
            if (callback != null)
                callback();
            PF_Bridge.RaiseCallbackSuccess(string.Empty, PlayFabAPIMethods.RemoveFriend, MessageDisplayStyle.none);
        }, PF_Bridge.PlayFabErrorCallback);
    }

    public static void SetFriendTags(string id, List<string> tags, UnityAction callback = null)
    {
        var request = new SetFriendTagsRequest
        {
            FriendPlayFabId = id,
            Tags = tags
        };

        DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.SetFriendTags);
        PlayFabClientAPI.SetFriendTags(request, result =>
        {
            if (callback != null)
                callback();
            PF_Bridge.RaiseCallbackSuccess(string.Empty, PlayFabAPIMethods.SetFriendTags, MessageDisplayStyle.none);
        }, PF_Bridge.PlayFabErrorCallback);
    }
    #endregion

    #region misc
    public static void RedeemItemOffer(CatalogItem offer, string instanceToRemove, UnityAction<string> callback = null, bool onlyRemoveInstance = false)
    {
        if (onlyRemoveInstance)
        {
            // this offer has already been rewarded, need to remove from the player's invenetory.
            var request = new ExecuteCloudScriptRequest();
            request.FunctionName = "RemoveOfferItem";
            request.FunctionParameter = new { PFID = PlayerId, InstanceToRemove = instanceToRemove };

            DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.ConsumeOffer);
            PlayFabClientAPI.ExecuteCloudScript(request, result =>
            {
                if (!PF_Bridge.VerifyErrorFreeCloudScriptResult(result))
                    return;

                if (callback != null)
                    callback(null);
                PF_Bridge.RaiseCallbackSuccess(string.Empty, PlayFabAPIMethods.ConsumeOffer, MessageDisplayStyle.none);

            }, PF_Bridge.PlayFabErrorCallback);
        }
        else
        {
            // consume the item 
            var removeReq = new ExecuteCloudScriptRequest
            {
                FunctionName = "RemoveOfferItem",
                FunctionParameter = new { PFID = PlayerId, InstanceToRemove = instanceToRemove }
            };
            PlayFabClientAPI.ExecuteCloudScript(removeReq, result =>
            {
                PF_Bridge.VerifyErrorFreeCloudScriptResult(result);
            }, PF_Bridge.PlayFabErrorCallback);

            // make the award
            var awardRequest = new ExecuteCloudScriptRequest
            {
                FunctionName = "RedeemItemOffer",
                FunctionParameter = new { PFID = PlayerId, Offer = offer, SingleUse = offer.Tags.IndexOf("SingleUse") > -1 ? true : false }
            };

            DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.ConsumeOffer);
            PlayFabClientAPI.ExecuteCloudScript(awardRequest, result =>
            {
                if (!PF_Bridge.VerifyErrorFreeCloudScriptResult(result))
                    return;
                if (callback != null)
                    callback(result.FunctionResult.ToString());
                PF_Bridge.RaiseCallbackSuccess(string.Empty, PlayFabAPIMethods.ConsumeOffer, MessageDisplayStyle.none);
            }, PF_Bridge.PlayFabErrorCallback);
        }
    }
    public static void SubtractLifeFromPlayer()
    {
        DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.ExecuteCloudScript);
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName = "SubtractLife",
            FunctionParameter = new { CharacterId = activeCharacter.characterDetails.CharacterId }
        };
        PlayFabClientAPI.ExecuteCloudScript(request, result =>
        {
            if (!PF_Bridge.VerifyErrorFreeCloudScriptResult(result))
                return;
            PF_Bridge.RaiseCallbackSuccess("", PlayFabAPIMethods.ExecuteCloudScript, MessageDisplayStyle.none);

        }, PF_Bridge.PlayFabErrorCallback);
    }

    public static void ClearActiveCharacter()
    {
        activeCharacter = null;
        if (characterAchievements != null) characterAchievements.Clear();
        if (characterStatistics != null) characterStatistics.Clear();
    }

    public static void RegisterForPushNotification(string pushToken = null, UnityAction callback = null)
    {
#if UNITY_EDITOR || UNITY_EDITOR_OSX
        if (callback != null)
        {
            callback();
            return;
        }
#endif

#if UNITY_IPHONE
			string hexToken = string.Empty;
			byte[] token = UnityEngine.iOS.NotificationServices.deviceToken;
			if(token != null)
			{
				RegisterForIOSPushNotificationRequest request = new RegisterForIOSPushNotificationRequest();
				request.DeviceToken = BitConverter.ToString(token).Replace("-", "").ToLower();
				
				hexToken = request.DeviceToken;
				Debug.Log (hexToken);
				
				DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.RegisterForPush);
				PlayFabClientAPI.RegisterForIOSPushNotification(request, result => 
				                                                {
					if(callback != null)
					{
						callback();
					}
					PF_Bridge.RaiseCallbackSuccess(string.Empty, PlayFabAPIMethods.RegisterForPush, MessageDisplayStyle.none);
				}, PF_Bridge.PlayFabErrorCallback);
			}
			else
			{
				Debug.Log("Push Token was null!");
			}
#endif

#if UNITY_ANDROID
        if (!string.IsNullOrEmpty(pushToken))
        {
            Debug.Log("GCM Init Success");
            var request = new AndroidDevicePushNotificationRegistrationRequest { DeviceToken = pushToken };

            DialogCanvasController.RequestLoadingPrompt(PlayFabAPIMethods.RegisterForPush);
            PlayFabClientAPI.AndroidDevicePushNotificationRegistration(request, result =>
            {
                if (callback != null)
                    callback();
                PF_Bridge.RaiseCallbackSuccess(string.Empty, PlayFabAPIMethods.RegisterForPush, MessageDisplayStyle.none);
            }, PF_Bridge.PlayFabErrorCallback);

        }
        else
        {
            Debug.Log("Push Token was null or empty: ");
        }
#endif
    }
    #endregion
}
