using System;
using System.Diagnostics;
using System.IO;

namespace Aidp.Playtest.Editor
{
    /// <summary>Assemble les images d'un scenario en GIF avec ffmpeg portable. Sans ffmpeg, ne fait rien (gif: null).</summary>
    internal static class GifEncoder
    {
        private const int EncodeTimeoutMs = 60000;
        private const int Fps = 8;
        private const int Width = 480;
        private const int MinFrames = 2;

        /// <summary>Chemin de ffmpeg.exe (variable AIDP_FFMPEG, sinon D:\DevTools\ffmpeg\bin\ffmpeg.exe) ou null s'il n'existe pas.</summary>
        public static string FindFfmpeg()
        {
            string fromEnv = Environment.GetEnvironmentVariable(PlaytestConstants.FfmpegEnvironmentVariable);
            if (!string.IsNullOrEmpty(fromEnv) && File.Exists(fromEnv))
                return fromEnv;
            return File.Exists(PlaytestConstants.FfmpegPath) ? PlaytestConstants.FfmpegPath : null;
        }

        /// <summary>Encode results/&lt;id&gt;/frames en results/&lt;id&gt;/&lt;id&gt;.gif, supprime les images, rend le chemin relatif au projet ou null.</summary>
        public static string TryEncode(string projectRoot, string id)
        {
            string captureDir = Path.Combine(projectRoot, PlaytestPaths.RelativeCaptureDir(id));
            string framesDir = Path.Combine(captureDir, PlaytestConstants.FramesDirectory);
            if (!Directory.Exists(framesDir))
                return null;

            try
            {
                string ffmpeg = FindFfmpeg();
                if (ffmpeg == null || Directory.GetFiles(framesDir, "*" + PlaytestConstants.PngExtension).Length < MinFrames)
                    return null;

                string gifPath = Path.Combine(captureDir, id + PlaytestConstants.GifExtension);
                string filter = "fps=" + Fps + ",scale=" + Width + ":-1:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse";
                string args = "-y -loglevel error -framerate " + Fps + " -i \"" + Path.Combine(framesDir, "%04d" + PlaytestConstants.PngExtension)
                              + "\" -vf \"" + filter + "\" \"" + gifPath + "\"";
                var info = new ProcessStartInfo(ffmpeg, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                using (Process process = Process.Start(info))
                {
                    process.OutputDataReceived += (s, e) => { };
                    process.ErrorDataReceived += (s, e) => { };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    if (!process.WaitForExit(EncodeTimeoutMs))
                    {
                        process.Kill();
                        return null;
                    }

                    if (process.ExitCode != 0 || !File.Exists(gifPath))
                        return null;
                }

                return PlaytestPaths.RelativeCaptureDir(id) + "/" + id + PlaytestConstants.GifExtension;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                try
                {
                    Directory.Delete(framesDir, true);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
