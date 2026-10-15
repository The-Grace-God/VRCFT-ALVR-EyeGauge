using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VRCFaceTracking.Core.Params.Data;

namespace ALVRModule
{
    public class EyeTracking
    {
        // EyeGauge required data
        private static Process _eyeGaugeProcess;
        private static MemoryMappedFile _avgMap;
        private static MemoryMappedFile _eyeMap;
        private static MemoryMappedViewAccessor _avgAccessor;
        private static MemoryMappedViewAccessor _eyeAccessor;
        private static bool _useDilation;

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryBufferAvg
        {
            public float leftAverage;
            public float rightAverage;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryBufferEye
        {
            public float l_eyeX_n;
            public float l_eyeY_n;
            public float r_eyeX_n;
            public float r_eyeY_n;
        }
        // End
        
        private static readonly float[] Identity = new float[] { 0, 0, 0, 1 };
        
        private static ILogger? _logger;
        private static CancellationTokenSource? _eyeGaugeCancellation;

        public static void ConfigureLogger(ILogger logger)
        {
            _logger = logger;
        }

        // Code taken from https://github.com/regzo2/VRCFaceTracking-QuestProOpenXR/blob/9b1b15b4b74fc070784a2f0370da1b47b756aac6/VRCFT%20-%20Quest%20OpenXR/QuestOpenXRTrackingModule.cs#L117
        private static (float pitch, float yaw) NormalizedGaze(float[] p)
        {
            float x = p[0];
            float y = p[1];
            float z = p[2];
            float w = p[3];

            float magnitude = (float)Math.Sqrt(x * x + y * y + z * z + w * w);
            x /= magnitude;
            y /= magnitude;
            z /= magnitude;
            w /= magnitude;

            float pitch = (float)Math.Asin(2.0 * (x * z - w * y));
            float yaw = (float)Math.Atan2(2.0 * (y * z + w * x), w * w - x * x - y * y + z * z);

            return (pitch, yaw);
        }
        
        // EyeGauge
        public static void InitializeEyeGauge()
        {
            
            var directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var executablePath = Path.Combine(directoryName, "EyeGauge\\OVREyeGauge.exe");

            Task.Run(() =>
            {
                if (File.Exists(executablePath))
                {
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        UseShellExecute = true
                    };

                    try
                    {
                        _logger?.LogInformation("Starting OVREyeGauge process");
                        _eyeGaugeProcess = Process.Start(processStartInfo);
                        _logger?.LogInformation("OVREyeGauge process started successfully.");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex.Message);
                    }

                    var connected = false;
                    while (!connected)
                    {
                        try
                        {
                            _logger?.LogInformation("Mapping OVREyeGauge memory.");
                            _avgMap = MemoryMappedFile.OpenExisting("Local\\EyeGaugeAvg", MemoryMappedFileRights.ReadWrite);
                            _avgAccessor = _avgMap.CreateViewAccessor(0, Marshal.SizeOf<MemoryBufferAvg>());
                            _eyeMap = MemoryMappedFile.OpenExisting("Local\\EyeGaugeEye", MemoryMappedFileRights.ReadWrite);
                            _eyeAccessor = _eyeMap.CreateViewAccessor(0, Marshal.SizeOf<MemoryBufferEye>());
                            _useDilation = true;
                            connected = true;
                            _logger?.LogInformation("OVREyeGauge successfully hooked.");
                        }
                        catch
                        {
                            _logger?.LogError("Memory map not connected. Waiting on connection.");
                            Thread.Sleep(1000);
                        }
                    }
                }
                else
                {
                    _logger?.LogError("OVREyeGauge executable not found: " + executablePath);
                }
            });
        }
        public static void ShutdownEyeGauge()
        {
            _eyeGaugeCancellation?.Cancel();
            _eyeGaugeCancellation?.Dispose();
            _eyeGaugeCancellation = null;
            
            if (_eyeGaugeProcess != null)
            {
                try
                {
                    if (!_eyeGaugeProcess.HasExited)
                    {
                        _eyeGaugeProcess.Kill();
                    }
                }
                catch { }
                _eyeGaugeProcess.Dispose();
                _eyeGaugeProcess = null;
            }

            _avgAccessor?.Dispose();
            _avgAccessor = null;
            _avgMap?.Dispose();
            _avgMap = null;
            _eyeAccessor?.Dispose();
            _eyeAccessor = null;
            _eyeMap?.Dispose();
            _eyeMap = null;
            _useDilation = false;
        }
        // End

        public static void SetEyesQuatParams(
            FloatParams p,
            FloatWeightParams w,
            UnifiedEyeData eye)
        {
            p.Read(8);

            (eye.Left.Gaze.x, eye.Left.Gaze.y) =
                NormalizedGaze(p.Params?[0..] ?? Identity);

            (eye.Right.Gaze.x, eye.Right.Gaze.y) =
                NormalizedGaze(p.Params?[4..] ?? Identity);
        }

        public static void SetCombEyesQuatParams(FloatParams p, FloatWeightParams w, UnifiedEyeData eye)
        {
            p.Read(4);

            (eye.Left.Gaze.x, eye.Left.Gaze.y) = NormalizedGaze(p.Params ?? Identity);
            (eye.Right.Gaze.x, eye.Right.Gaze.y) = NormalizedGaze(p.Params ?? Identity);
        }

        public static void SetEyeDialation(UnifiedEyeData eye)
        {
            if (_useDilation &&
                _avgAccessor != null &&
                _eyeAccessor != null)
            {
                // Read EyeGauge pupil averages.
                _avgAccessor.Read(0, out MemoryBufferAvg avg);
                eye.Left.PupilDiameter_MM = avg.leftAverage * 10f;
                eye.Right.PupilDiameter_MM = avg.rightAverage * 10f;

                // Send the current gaze coordinates to EyeGauge.
                var gaze = new MemoryBufferEye
                {
                    l_eyeX_n = 0,
                    l_eyeY_n = 0,
                    r_eyeX_n = 0,
                    r_eyeY_n = 0,
                };

                _eyeAccessor.Write(0, ref gaze);
            }
            else
            {
                eye.Left.PupilDiameter_MM = 5f;
                eye.Right.PupilDiameter_MM = 5f;
            }

            eye._minDilation = 0;
            eye._maxDilation = 10;
        }
    }
}
