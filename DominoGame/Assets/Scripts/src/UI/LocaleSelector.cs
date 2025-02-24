using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace src.UI
{
    public class LocaleSelector : MonoBehaviour
    {
        public void ChangeLocale(int localeID)
        {
        
            StartCoroutine(SetLocale(localeID));

        }
        IEnumerator SetLocale(int _localeID)
        {
            yield return LocalizationSettings.InitializationOperation;
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[_localeID];
        }
    
    }
}
