using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Michsky.UI.Heat
{
    public static class ChapterManagerRemote
    {
        private const string CacheExtension = ".chapter";
        private const string SpriteCacheExtension = ".sprite";

        public static async Task UpdateChapters(List<ChapterManager.ChapterItem> chapters)
        {
            if (chapters == null)
                return;

            foreach (var chapter in chapters)
            {
                if (chapter == null)
                    continue;

                if (string.IsNullOrWhiteSpace(chapter.webslot))
                    continue;

                await UpdateChapter(chapter);
            }
        }

        private static async Task UpdateChapter(ChapterManager.ChapterItem chapter)
        {
            string json = await DownloadJson(chapter.webslot);

            if (!string.IsNullOrEmpty(json))
            {
                SaveCache(chapter.webslot, json);
                await ApplyRemoteJson(chapter, json);
                return;
            }

            json = LoadCache(chapter.webslot);

            if (!string.IsNullOrEmpty(json))
                await ApplyRemoteJson(chapter, json);
        }

        private static async Task ApplyRemoteJson(ChapterManager.ChapterItem chapter, string json)
        {
            try
            {
                RemoteChapterItem remote = JsonUtility.FromJson<RemoteChapterItem>(json);
                if (remote == null)
                    return;

                if (!string.IsNullOrWhiteSpace(remote.chapterID))
                    chapter.chapterID = remote.chapterID;

                if (!string.IsNullOrWhiteSpace(remote.title))
                    chapter.title = remote.title;

                if (!string.IsNullOrWhiteSpace(remote.description))
                    chapter.description = remote.description;

                if (!string.IsNullOrWhiteSpace(remote.jumpLink))
                    chapter.jumpLink = remote.jumpLink;

                if (!string.IsNullOrWhiteSpace(remote.background))
                {
                    Sprite sprite = await DownloadSprite(remote.background);
                    if (sprite != null)
                        chapter.background = sprite;
                }
                Debug.Log($"Remote chapter applied: {chapter.chapterID} | {chapter.title}");

            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Remote Chapter JSON Error\n{ex.Message}");
            }

        }

        private static async Task<Sprite> DownloadSprite(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                using UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);
                www.timeout = 10;

                UnityWebRequestAsyncOperation op = www.SendWebRequest();
                while (!op.isDone)
                    await Task.Yield();

#if UNITY_2020_1_OR_NEWER
                if (www.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(www);
                    SaveSpriteCache(url, tex);

                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f));
                }
#else
                if (!www.isNetworkError && !www.isHttpError)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(www);
                    SaveSpriteCache(url, tex);

                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f));
                }
#endif
            }
            catch { }

            Texture2D cached = LoadSpriteCache(url);
            if (cached == null)
                return null;

            return Sprite.Create(cached, new Rect(0, 0, cached.width, cached.height),
                new Vector2(0.5f, 0.5f));
        }

        private static void SaveSpriteCache(string url, Texture2D texture)
        {
            try
            {
                byte[] png = texture.EncodeToPNG();
                File.WriteAllBytes(GetSpriteCachePath(url), png);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex.Message);
            }
        }

        private static Texture2D LoadSpriteCache(string url)
        {
            try
            {
                string path = GetSpriteCachePath(url);
                if (!File.Exists(path))
                    return null;

                byte[] bytes = File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                return tex;
            }
            catch { }

            return null;
        }

        private static string GetSpriteCachePath(string url)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
                StringBuilder sb = new StringBuilder();

                foreach (byte b in bytes)
                    sb.Append(b.ToString("x2"));

                return Path.Combine(Application.persistentDataPath,
                    sb.ToString() + SpriteCacheExtension);
            }
        }

        private static async Task<string> DownloadJson(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                using UnityWebRequest www = UnityWebRequest.Get(url);
                www.timeout = 10;

                UnityWebRequestAsyncOperation op = www.SendWebRequest();
                while (!op.isDone)
                    await Task.Yield();

#if UNITY_2020_1_OR_NEWER
                if (www.result == UnityWebRequest.Result.Success)
                    return www.downloadHandler.text;
#else
                if (!www.isNetworkError && !www.isHttpError)
                    return www.downloadHandler.text;
#endif
            }
            catch { }

            return null;
        }

        private static void SaveCache(string url, string json)
        {
            try
            {
                File.WriteAllText(GetCachePath(url), json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex.Message);
            }
        }

        private static string LoadCache(string url)
        {
            try
            {
                string path = GetCachePath(url);
                if (File.Exists(path))
                    return File.ReadAllText(path);
            }
            catch { }

            return null;
        }

        private static string GetCachePath(string url)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
                StringBuilder sb = new StringBuilder();

                foreach (byte b in bytes)
                    sb.Append(b.ToString("x2"));

                return Path.Combine(Application.persistentDataPath,
                    sb.ToString() + CacheExtension);
            }
        }
    }

    [Serializable]
    public class RemoteChapterItem
    {
        public string chapterID;
        public string title;
        public string description;
        public string jumpLink;
        public string background;

        public bool enabled = true;
        public int priority = 0;
        public string startDate = "";
        public string endDate = "";
    }
}
