using System;
using UnityEngine;

namespace Mano.Data
{
    [CreateAssetMenu(fileName = "GoogleSettingSO", menuName = "ManoData/GoogleSetting")]
    public class GoogleSettingSO : ScriptableObject
    {
        [Header("Google Cloud Setting")]
        public string ClientId = "YOUR_CLIENT_ID";
        public string ClientSecret = "YOUR_CLIENT_SECRET";
        public string SpreadSheetID = "YOUR_SPREAD_SHEET_ID";

        [Header("Auth Token")]
        [ManoOnly] public string AccessToken = "";
        [ManoOnly] public string RefreshToken = "";
        [ManoOnly] public long ExpiryTime;

        public bool IsTokenExpired => DateTimeOffset.Now.ToUnixTimeSeconds() >= ExpiryTime;
    }
}
