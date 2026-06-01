using Gemstone.Gemstone;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GorillaCosmeticTracker
{
    public class CosmeticTracker : MonoBehaviour
    {
        private const string WebhookUrl = ""; // Not including webhook url

        public static CosmeticTracker Instance { get; private set; }

        private class TrackedPlayerSession
        {
            public string NickName;
            public string UserId;
            public string CosmeticName;
            public string RoomCode;
            public DateTime TimeFound;
            public bool StillPresentThisFrame;
        }

        private static Dictionary<string, TrackedPlayerSession> activeTrackedSessions = new Dictionary<string, TrackedPlayerSession>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            foreach (var session in activeTrackedSessions.Values)
            {
                session.StillPresentThisFrame = false;
            }

            foreach (VRRig rig in VRRigCache.ActiveRigs)
            {
                if (rig != null)
                {
                    HasSpecialCosmetic(rig);
                }
            }

            List<string> leftPlayerKeys = new List<string>();
            foreach (var kvp in activeTrackedSessions)
            {
                if (!kvp.Value.StillPresentThisFrame)
                {
                    leftPlayerKeys.Add(kvp.Key);
                }
            }

            foreach (string key in leftPlayerKeys)
            {
                TrackedPlayerSession session = activeTrackedSessions[key];
                TimeSpan duration = DateTime.Now - session.TimeFound;

                string durationString = string.Format("{0:D2}h {1:D2}m {2:D2}s", duration.Hours, duration.Minutes, duration.Seconds);

                StartCoroutine(SendLeaveWebhookCoroutine(session.CosmeticName, session.RoomCode, session.NickName, session.UserId, durationString));
                activeTrackedSessions.Remove(key);
            }
        }

        public static string HasSpecialCosmetic(VRRig Player)
        {
            if (Player == null) return "False";

            var cosmeticsField = AccessTools.Field(Player.GetType(), "_playerOwnedCosmetics");
            if (cosmeticsField == null) return "False";

            var cosmeticsObj = cosmeticsField.GetValue(Player);
            string concat = "";

            if (cosmeticsObj is HashSet<string> cosmeticSet)
            {
                concat = string.Join("", cosmeticSet);
            }

            List<string> foundCosmeticsList = new List<string>();

            if (concat.Contains("LMAPY."))
                foundCosmeticsList.Add("Forest Guide");
            if (concat.Contains("LBANI."))
                foundCosmeticsList.Add("Another Axiom Creator");
            if (concat.Contains("LBADE."))
                foundCosmeticsList.Add("Finger Painter");
            if (concat.Contains("LBAAD."))
                foundCosmeticsList.Add("Administrator Badge");

            if (foundCosmeticsList.Count > 0)
            {
                string roomCode = "Not In Room";
                int playerCount = 0;

                if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
                {
                    roomCode = PhotonNetwork.CurrentRoom.Name;
                    playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
                }

                string nickName = "Unknown";
                string userId = "Unknown";

                if (Player.Creator != null)
                {
                    nickName = Player.Creator.NickName;
                    userId = Player.Creator.UserId;
                }

                if (string.IsNullOrEmpty(nickName))
                {
                    nickName = "Player";
                }

                if (Instance != null)
                {
                    foreach (string cosmetic in foundCosmeticsList)
                    {
                        string trackingKey = $"{userId}_{cosmetic}_{roomCode}";

                        if (!activeTrackedSessions.ContainsKey(trackingKey))
                        {
                            TrackedPlayerSession newSession = new TrackedPlayerSession
                            {
                                NickName = nickName,
                                UserId = userId,
                                CosmeticName = cosmetic,
                                RoomCode = roomCode,
                                TimeFound = DateTime.Now,
                                StillPresentThisFrame = true
                            };

                            activeTrackedSessions.Add(trackingKey, newSession);

                            NotiLib.SendNotification($"[TRACKER] {nickName} found with {cosmetic}!", 4000f);

                            Instance.StartCoroutine(Instance.SendJoinWebhookCoroutine(cosmetic, roomCode, nickName, userId, playerCount, newSession.TimeFound.ToString("yyyy-MM-dd HH:mm:ss")));
                        }
                        else
                        {
                            activeTrackedSessions[trackingKey].StillPresentThisFrame = true;
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("[Cosmetic Tracker] Cannot send webhook because CosmeticTracker MonoBehaviour instance is missing from the scene.");
                }

                return string.Join(", ", foundCosmeticsList);
            }

            return "False";
        }

        private IEnumerator SendJoinWebhookCoroutine(string cosmeticName, string roomCode, string nickName, string userId, int playerCount, string timeTracked)
        {
            string content = $"📥 **{cosmeticName} Found!**\n" +
                             $"**Code:** {roomCode}\n" +
                             $"**Person:** {nickName}, {userId}\n" +
                             $"**Players in Room:** {playerCount}\n" +
                             $"**Time Tracked:** {timeTracked}";

            yield return StartCoroutine(PostWebhook(content));
        }

        private IEnumerator SendLeaveWebhookCoroutine(string cosmeticName, string roomCode, string nickName, string userId, string duration)
        {
            string content = $"📤 **{cosmeticName} Left Code!**\n" +
                             $"**Code:** {roomCode}\n" +
                             $"**Person:** {nickName}, {userId}\n" +
                             $"**Total Time in Room:** {duration}";

            yield return StartCoroutine(PostWebhook(content));
        }

        private IEnumerator PostWebhook(string content)
        {
            string jsonPayload = "{\"content\": \"" + EscapeJsonString(content) + "\"}";
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonPayload);

            using (UnityWebRequest request = new UnityWebRequest(WebhookUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(jsonBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[Webhook Error] Failed to send cosmetic alert: {request.error}");
                }
            }
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")
                        .Replace("\t", "\\t");
        }
    }
}