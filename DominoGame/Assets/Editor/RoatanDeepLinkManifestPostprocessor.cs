#if UNITY_ANDROID
using System;
using System.IO;
using System.Xml;

using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Automatically registers the roatandomino://auth OAuth deep-link intent-filter
/// on UnityPlayerActivity inside AndroidManifest.xml during Android Gradle export.
/// </summary>
public class RoatanDeepLinkManifestPostprocessor : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 10;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning($"[RoatanDeepLink] AndroidManifest.xml not found at '{manifestPath}'.");
            return;
        }

        try
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(manifestPath);

            const string androidNs = "http://schemas.android.com/apk/res/android";
            XmlNodeList activities = doc.GetElementsByTagName("activity");
            XmlElement targetActivity = null;
            foreach (XmlNode node in activities)
            {
                if (node is XmlElement el)
                {
                    string name = el.GetAttribute("name", androidNs);
                    if (!string.IsNullOrEmpty(name) && name.Contains("UnityPlayerActivity"))
                    {
                        targetActivity = el;
                        break;
                    }
                }
            }

            if (targetActivity == null && activities.Count > 0)
            {
                targetActivity = activities[0] as XmlElement;
            }

            if (targetActivity == null)
            {
                Debug.LogWarning("[RoatanDeepLink] No <activity> found in AndroidManifest.xml.");
                return;
            }

            // Avoid duplicate injection
            if (targetActivity.InnerXml.Contains("roatandomino"))
            {
                return;
            }

            XmlElement intentFilter = doc.CreateElement("intent-filter");

            XmlElement action = doc.CreateElement("action");
            action.SetAttribute("name", androidNs, "android.intent.action.VIEW");
            intentFilter.AppendChild(action);

            XmlElement catDefault = doc.CreateElement("category");
            catDefault.SetAttribute("name", androidNs, "android.intent.category.DEFAULT");
            intentFilter.AppendChild(catDefault);

            XmlElement catBrowsable = doc.CreateElement("category");
            catBrowsable.SetAttribute("name", androidNs, "android.intent.category.BROWSABLE");
            intentFilter.AppendChild(catBrowsable);

            XmlElement data = doc.CreateElement("data");
            data.SetAttribute("scheme", androidNs, "roatandomino");
            data.SetAttribute("host", androidNs, "auth");
            intentFilter.AppendChild(data);

            targetActivity.AppendChild(intentFilter);
            doc.Save(manifestPath);
            Debug.Log("[RoatanDeepLink] Injected roatandomino://auth intent-filter into AndroidManifest.xml.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[RoatanDeepLink] Failed to inject deep-link intent-filter: {ex.Message}");
        }
    }
}
#endif
