using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace RelicLoadoutJournal
{
    internal sealed class JournalButtonArt : IDisposable
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        internal sealed class Plates
        {
            internal Sprite Rest, Focus, Press, Disabled;
            internal float PixelsPerUnitMultiplier;
        }

        internal Plates Create(bool cancel, ColorBlock colors)
        {
            var prefab = Resources.Load<GameObject>("UI/OverlayMessage");
            var spriteName = cancel ? "UI_23" : "UI_21";
            var template = prefab?.GetComponentsInChildren<Button>(true).FirstOrDefault(
                button => button.targetGraphic is Image image && image.sprite != null && image.sprite.name == spriteName);
            if (!(template?.targetGraphic is Image native))
                throw new InvalidOperationException("Native button template unavailable: " + spriteName);

            var sprites = template.spriteState;
            return new Plates
            {
                Rest = Recolor(native.sprite, colors.normalColor, false, spriteName + " journal rest"),
                Focus = Recolor(sprites.selectedSprite, colors.selectedColor, true, spriteName + " journal focus"),
                Press = Recolor(sprites.selectedSprite, colors.pressedColor, true, spriteName + " journal press"),
                Disabled = Recolor(sprites.disabledSprite, colors.disabledColor, false, spriteName + " journal disabled"),
                PixelsPerUnitMultiplier = native.pixelsPerUnitMultiplier
            };
        }

        private Sprite Recolor(Sprite source, Color32 fill, bool whiteEdge, string name)
        {
            var area = source.textureRect;
            int width = Mathf.RoundToInt(area.width), height = Mathf.RoundToInt(area.height);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            owned.Add(texture);
            var atlas = source.texture;
            if (atlas.isReadable)
                texture.SetPixels(atlas.GetPixels(Mathf.RoundToInt(area.x), Mathf.RoundToInt(area.y), width, height));
            else
            {
                var previous = RenderTexture.active;
                var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                try
                {
                    Graphics.Blit(atlas, target, new Vector2(area.width / atlas.width, area.height / atlas.height),
                        new Vector2(area.x / atlas.width, area.y / atlas.height));
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(target);
                }
            }

            var nativePixels = texture.GetPixels32();
            var center = nativePixels[(height / 2) * width + width / 2];
            for (int i = 0; i < nativePixels.Length; i++)
            {
                var pixel = nativePixels[i];
                if (pixel.a == 0) continue;
                bool darkEdge = pixel.r < 24 && pixel.g < 24 && pixel.b < 24;
                bool highlight = whiteEdge && pixel.r > 240 && pixel.g > 240 && pixel.b > 240;
                if (darkEdge || highlight) continue;
                // The native rest and disabled plates have a one-pixel inner bevel.
                int light = pixel.r == center.r && pixel.g == center.g && pixel.b == center.b ? 0 : 13;
                nativePixels[i] = new Color32((byte)Mathf.Min(255, fill.r + light),
                    (byte)Mathf.Min(255, fill.g + light), (byte)Mathf.Min(255, fill.b + light), pixel.a);
            }
            texture.SetPixels32(nativePixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f),
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            owned.Add(sprite);
            return sprite;
        }

        public void Dispose()
        {
            for (int i = owned.Count - 1; i >= 0; i--) UnityEngine.Object.Destroy(owned[i]);
            owned.Clear();
        }
    }
}
