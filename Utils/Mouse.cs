using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ExileCore;
using SharpDX;

namespace AreWeThereYet2.Utils;

/// <summary>
/// Human-like mouse movement system with curves and randomization
/// Based on the superior AreWeThereYet mouse movement implementation
/// </summary>
public static class Mouse
{
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
    private const uint MOUSEEVENTF_LEFTUP = 0x04;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
    private const uint MOUSEEVENTF_RIGHTUP = 0x10;

    private static readonly Random _random = new Random();

    /// <summary>
    /// Move mouse to target position with human-like curve
    /// </summary>
    public static async Task MoveTo(Vector2 target, int duration = 300, bool click = false)
    {
        try
        {
            var currentPos = GetCursorPosition();
            var startPos = new Vector2(currentPos.X, currentPos.Y);
            
            // Calculate curve points for natural movement
            var curvePoints = GenerateMovementCurve(startPos, target, duration);
            
            // Execute movement along curve
            for (int i = 0; i < curvePoints.Length; i++)
            {
                var point = curvePoints[i];
                SetCursorPos((int)point.X, (int)point.Y);
                
                // Variable delay for natural movement
                var delay = (duration / curvePoints.Length) + _random.Next(-5, 5);
                await Task.Delay(Math.Max(1, delay));
            }
            
            // Final position adjustment
            SetCursorPos((int)target.X, (int)target.Y);
            
            // Perform click if requested
            if (click)
            {
                await Task.Delay(_random.Next(50, 150)); // Pre-click delay
                LeftClick();
            }
        }
        catch (Exception ex)
        {
            DebugWindow.LogError($"Mouse.MoveTo error: {ex.Message}");
        }
    }

    /// <summary>
    /// Click at current mouse position
    /// </summary>
    public static void LeftClick()
    {
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        Task.Delay(_random.Next(20, 80)).Wait(); // Human-like click duration
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }

    /// <summary>
    /// Right click at current mouse position
    /// </summary>
    public static void RightClick()
    {
        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
        Task.Delay(_random.Next(20, 80)).Wait();
        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
    }

    /// <summary>
    /// Move and click at target position
    /// </summary>
    public static async Task ClickAt(Vector2 target, int moveDuration = 300)
    {
        await MoveTo(target, moveDuration, true);
    }

    /// <summary>
    /// Get current cursor position
    /// </summary>
    public static POINT GetCursorPosition()
    {
        GetCursorPos(out POINT point);
        return point;
    }

    /// <summary>
    /// Generate natural movement curve between two points
    /// </summary>
    private static Vector2[] GenerateMovementCurve(Vector2 start, Vector2 end, int duration)
    {
        var distance = Vector2.Distance(start, end);
        var steps = Math.Max(10, (int)(distance / 20)); // More steps for longer distances
        var curve = new Vector2[steps];

        // Generate control points for Bezier curve
        var controlPoint1 = Vector2.Lerp(start, end, 0.25f);
        var controlPoint2 = Vector2.Lerp(start, end, 0.75f);
        
        // Add some randomness to control points for natural movement
        controlPoint1.X += _random.Next(-50, 50);
        controlPoint1.Y += _random.Next(-30, 30);
        controlPoint2.X += _random.Next(-50, 50);
        controlPoint2.Y += _random.Next(-30, 30);

        // Generate curve points using Cubic Bezier
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / (steps - 1);
            curve[i] = CubicBezier(t, start, controlPoint1, controlPoint2, end);
        }

        return curve;
    }

    /// <summary>
    /// Calculate cubic Bezier curve point
    /// </summary>
    private static Vector2 CubicBezier(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        var u = 1 - t;
        var tt = t * t;
        var uu = u * u;
        var uuu = uu * u;
        var ttt = tt * t;

        var p = uuu * p0; // (1-t)^3 * P0
        p += 3 * uu * t * p1; // 3(1-t)^2 * t * P1
        p += 3 * u * tt * p2; // 3(1-t) * t^2 * P2
        p += ttt * p3; // t^3 * P3

        return p;
    }

    /// <summary>
    /// Convert world position to screen coordinates
    /// </summary>
    public static Vector2 WorldToScreen(Vector3 worldPos, GameController gameController)
    {
        try
        {
            var camera = gameController.Game.IngameState.Camera;
            var screenPos = camera.WorldToScreen(worldPos);
            return new Vector2(screenPos.X, screenPos.Y);
        }
        catch
        {
            return Vector2.Zero;
        }
    }
}