using System;
using System.Threading.Tasks;
using SharpDX;

namespace AreWeThereYet2.Utils;

/// <summary>
/// BREAKTHROUGH: Smooth mouse movement with Bézier curves like InputHumanizer
/// This is the KEY missing piece - original uses human-like mouse curves, not direct clicks!
/// </summary>
public static class SmoothMouseMovement
{
    private static readonly Random _random = new();
    
    /// <summary>
    /// Move mouse in smooth Bézier curve like InputHumanizer library
    /// This makes movement look natural to game systems
    /// </summary>
    public static async Task MoveTo(Vector2 targetScreenPos, int durationMs = 200)
    {
        try
        {
            var startPos = Mouse.GetCursorPosition();
            var startTime = DateTime.UtcNow;
            var endTime = startTime.AddMilliseconds(durationMs);
            
            // Create Bézier curve control points for natural movement
            var controlPoint1 = GenerateControlPoint(startPos, targetScreenPos, 0.3f);
            var controlPoint2 = GenerateControlPoint(startPos, targetScreenPos, 0.7f);
            
            while (DateTime.UtcNow < endTime)
            {
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                var progress = Math.Min(elapsed / durationMs, 1.0);
                
                // Use cubic Bézier curve for smooth, natural movement
                var currentPos = CalculateBezierPoint(
                    (float)progress,
                    startPos,
                    controlPoint1,
                    controlPoint2,
                    targetScreenPos
                );
                
                Mouse.SetCursorPos((int)currentPos.X, (int)currentPos.Y);
                
                // Small delay for smooth animation
                await Task.Delay(16); // ~60 FPS updates
                
                if (progress >= 1.0) break;
            }
            
            // Ensure we end exactly at target
            Mouse.SetCursorPos((int)targetScreenPos.X, (int)targetScreenPos.Y);
        }
        catch (Exception ex)
        {
            // Fallback to direct movement if smooth movement fails
            Mouse.SetCursorPos((int)targetScreenPos.X, (int)targetScreenPos.Y);
        }
    }
    
    /// <summary>
    /// Hover mouse over target without clicking - like original AreWeThereYet behavior
    /// </summary>
    public static async Task HoverOver(Vector2 targetScreenPos)
    {
        try
        {
            await MoveTo(targetScreenPos, 150); // Quick hover movement
        }
        catch
        {
            // Fallback to direct positioning
            Mouse.SetCursorPos((int)targetScreenPos.X, (int)targetScreenPos.Y);
        }
    }
    
    /// <summary>
    /// Smooth click with natural timing like InputHumanizer
    /// </summary>
    public static async Task SmoothClick(Vector2 targetScreenPos)
    {
        try
        {
            // Move to target smoothly
            await MoveTo(targetScreenPos, 200);
            
            // Add small random delay like human behavior
            await Task.Delay(_random.Next(50, 150));
            
            // Perform click
            Mouse.LeftMouseDown((int)targetScreenPos.X, (int)targetScreenPos.Y);
            await Task.Delay(_random.Next(50, 100)); // Hold duration
            Mouse.LeftMouseUp((int)targetScreenPos.X, (int)targetScreenPos.Y);
        }
        catch (Exception ex)
        {
            // Fallback to direct click
            Mouse.LeftClick((int)targetScreenPos.X, (int)targetScreenPos.Y);
        }
    }
    
    /// <summary>
    /// Generate natural control point for Bézier curve
    /// </summary>
    private static Vector2 GenerateControlPoint(Vector2 start, Vector2 end, float position)
    {
        var midPoint = Vector2.Lerp(start, end, position);
        
        // Add random offset for natural curve
        var offset = new Vector2(
            _random.Next(-50, 50),
            _random.Next(-30, 30)
        );
        
        return midPoint + offset;
    }
    
    /// <summary>
    /// Calculate point on cubic Bézier curve
    /// </summary>
    private static Vector2 CalculateBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        var u = 1 - t;
        var tt = t * t;
        var uu = u * u;
        var uuu = uu * u;
        var ttt = tt * t;
        
        var point = uuu * p0; // (1-t)^3 * P0
        point += 3 * uu * t * p1; // 3(1-t)^2 * t * P1
        point += 3 * u * tt * p2; // 3(1-t) * t^2 * P2
        point += ttt * p3; // t^3 * P3
        
        return point;
    }
}