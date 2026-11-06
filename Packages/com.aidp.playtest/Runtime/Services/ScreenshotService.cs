using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Aidp.Playtest
{
    /// <summary>Captures d'ecran (ScreenCapture) en PNG. Sans rendu (batchmode -nographics), la capture est sautee, jamais fatale.</summary>
    public static class ScreenshotService
    {
        /// <summary>Capture a la fin de la frame ; <paramref name="onDone"/> recoit true si le fichier existe.</summary>
        public static IEnumerator CaptureTo(string absolutePath, Action<bool> onDone)
        {
            yield return new WaitForEndOfFrame();
            bool ok = false;
            Texture2D texture = null;
            try
            {
                if (Application.isBatchMode && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    throw new InvalidOperationException("no graphics device");
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture != null)
                {
                    AidpFileStore.WriteBytes(absolutePath, texture.EncodeToPNG());
                    ok = File.Exists(absolutePath);
                }
            }
            catch (Exception)
            {
                ok = false;
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }

            onDone?.Invoke(ok);
        }
    }
}
