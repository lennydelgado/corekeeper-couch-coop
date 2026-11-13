using System.Text;
using Pug.RP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CouchCoop
{
    /// <summary>
    /// Logs every camera and its render size, so render/letterbox issues (e.g. the title screen) can be
    /// diagnosed from Player.log. Runs on scene change, after a window layout change, and on F11.
    /// </summary>
    internal static class Diagnostics
    {
        private static float _pendingAt = -1f;

        public static void Init()
        {
            SceneManager.activeSceneChanged += (_, next) => Schedule(1.5f);
        }

        public static void Schedule(float delay) => _pendingAt = Time.unscaledTime + delay;

        public static void Tick()
        {
            if (Input.GetKeyDown(KeyCode.F11)) Dump("F11");
            if (_pendingAt >= 0f && Time.unscaledTime >= _pendingAt) { _pendingAt = -1f; Dump("auto"); }
        }

        public static void Dump(string reason)
        {
            var sb = new StringBuilder();
            sb.Append($"{CouchCoopMod.Tag} [diag:{reason}] scene={SceneManager.GetActiveScene().name} screen={Screen.width}x{Screen.height} ")
              .Append($"layout={WindowLayout.Current} fill={AspectFill.Enabled}\n");
            foreach (var cam in Camera.allCameras)
            {
                var pc = cam.GetComponent<PugCamera>();
                sb.Append($"  cam '{cam.name}' type={cam.cameraType} rect={cam.pixelRect.width}x{cam.pixelRect.height} ")
                  .Append($"ortho={cam.orthographicSize:0.##} aspect={cam.aspect:0.###} rt={(cam.targetTexture ? cam.targetTexture.width + "x" + cam.targetTexture.height : "-")}");
                if (pc != null)
                {
                    try
                    {
                        sb.Append($" pug: mode={pc.GetOutputMode(cam)} px={pc.GetPixelWidth(cam)}x{pc.GetPixelHeight(cam)} ")
                          .Append($"base={pc.outputWidth}x{pc.outputHeight} integer={pc.integerScaling}");
                    }
                    catch (System.Exception e) { sb.Append(" pug: " + e.GetType().Name); }
                }
                sb.Append('\n');
            }
            foreach (var q in Object.FindObjectsByType<BilinearQuadAutoSizer>(FindObjectsSortMode.None))
                sb.Append($"  quadSizer '{q.name}' cam={(q.bilinearQuadCamera ? q.bilinearQuadCamera.name : "-")} scale={(q.quad ? q.quad.transform.localScale.ToString() : "-")}\n");
            Debug.Log(sb.ToString());
        }
    }
}
