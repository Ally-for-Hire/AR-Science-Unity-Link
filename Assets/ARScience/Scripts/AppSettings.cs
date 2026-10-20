using System;
using UnityEngine;

namespace ARScience
{
    /// <summary>
    /// User settings, saved on the device with PlayerPrefs.
    /// To add a setting: add a key, a property like ShowPlanes, and call Changed after saving.
    /// </summary>
    public static class AppSettings
    {
        const string k_ShowPlanesKey = "arsci.showPlanes";

        /// <summary>Raised after any setting changes so listeners can update immediately.</summary>
        public static event Action Changed;

        /// <summary>Whether detected surfaces are drawn in AR. Defaults to on.</summary>
        public static bool ShowPlanes
        {
            get => PlayerPrefs.GetInt(k_ShowPlanesKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(k_ShowPlanesKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }
    }
}
