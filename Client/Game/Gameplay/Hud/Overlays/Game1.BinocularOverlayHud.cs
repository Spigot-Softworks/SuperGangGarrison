#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawBinocularOverlay()
    {
        if (!_world.LocalPlayer.IsUsingBinoculars)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        
        // Create or recreate mask if needed (at half resolution for pixelation)
        var maskWidth = viewportWidth / 2;
        var maskHeight = viewportHeight / 2;
        
        if (_binocularOverlayMask == null 
            || _binocularOverlayMaskWidth != maskWidth 
            || _binocularOverlayMaskHeight != maskHeight)
        {
            _binocularOverlayMask?.Dispose();
            
            // Binocular parameters (at half resolution)
            var centerX = maskWidth / 2f;
            var centerY = maskHeight / 2f;
            var circleRadius = 90f; // Radius of each circle (half of full res)
            var circleSpacing = 140f; // Distance between circle centers (half of full res)
            
            // Calculate circle centers (overlapping in the middle)
            var leftCircleX = centerX - circleSpacing / 2f;
            var rightCircleX = centerX + circleSpacing / 2f;
            var circleY = centerY;
            
            // Create pixel array for the mask
            var pixels = new Color[maskWidth * maskHeight];
            
            // Fill with black, then clear circles (make transparent)
            for (int y = 0; y < maskHeight; y++)
            {
                for (int x = 0; x < maskWidth; x++)
                {
                    var dx1 = x - leftCircleX;
                    var dy1 = y - circleY;
                    var distanceToLeft = MathF.Sqrt(dx1 * dx1 + dy1 * dy1);
                    
                    var dx2 = x - rightCircleX;
                    var dy2 = y - circleY;
                    var distanceToRight = MathF.Sqrt(dx2 * dx2 + dy2 * dy2);
                    
                    // If inside either circle, make transparent; otherwise semi-transparent black
                    if (distanceToLeft <= circleRadius || distanceToRight <= circleRadius)
                    {
                        pixels[y * maskWidth + x] = Color.Transparent;
                    }
                    else
                    {
                        pixels[y * maskWidth + x] = new Color(0, 0, 0, 200); // Semi-transparent black
                    }
                }
            }
            
            // Create the mask texture
            _binocularOverlayMask = new Texture2D(GraphicsDevice, maskWidth, maskHeight);
            _binocularOverlayMask.SetData(pixels);
            _binocularOverlayMaskWidth = maskWidth;
            _binocularOverlayMaskHeight = maskHeight;
        }
        
        // Draw the mask scaled up 2x with point filtering (for pixelation)
        // Need to temporarily change blend state, then restore
        _spriteBatch.End();
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend, rasterizerState: RasterizerState.CullNone);
        _spriteBatch.Draw(_binocularOverlayMask, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.White);
        _spriteBatch.End();
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: RasterizerState.CullNone);
    }
}
