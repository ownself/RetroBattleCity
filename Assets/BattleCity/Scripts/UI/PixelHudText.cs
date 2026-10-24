using UnityEngine;

namespace BattleCity
{
    /// <summary>Small 3x5 pixel captions for glyphs absent from SpriteCatalog; no font or texture dependency.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PixelHudText : UnityEngine.UI.MaskableGraphic
    {
        [SerializeField] private string text = "";
        public string Text
        {
            get => text;
            set { if (text == value) return; text = value; SetVerticesDirty(); }
        }

        // Row-major, top to bottom; only the HUD's uppercase caption alphabet is supported.
        private static string Glyph(char c)
        {
            switch (c)
            {
                case 'A': return "010101111101101";
                case 'C': return "111100100100111";
                case 'D': return "110101101101110";
                case 'E': return "111100110100111";
                case 'F': return "111100110100100";
                case 'G': return "111100101101111";
                case 'I': return "111010010010111";
                case 'L': return "100100100100111";
                case 'M': return "101111111101101";
                case 'N': return "101111111111101";
                case 'O': return "111101101101111";
                case 'P': return "110101110100100";
                case 'R': return "110101110101101";
                case 'S': return "111100111001111";
                case 'T': return "111010010010010";
                case 'U': return "101101101101111";
                case 'V': return "101101101101010";
                case 'W': return "101101111111101";
                case 'Y': return "101101010010010";
                case '+': return "000010111010000";
                default: return "000000000000000";
            }
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            var origin = rectTransform.rect.min;
            for (int letter = 0; letter < text.Length; letter++)
            {
                string glyph = Glyph(text[letter]);
                for (int row = 0; row < 5; row++) for (int x = 0; x < 3; x++)
                {
                    if (glyph[row * 3 + x] != '1') continue;
                    float left = origin.x + letter * 4 + x, bottom = origin.y + 4 - row;
                    int start = vh.currentVertCount;
                    vh.AddVert(new Vector3(left, bottom), color, Vector2.zero);
                    vh.AddVert(new Vector3(left, bottom + 1), color, Vector2.zero);
                    vh.AddVert(new Vector3(left + 1, bottom + 1), color, Vector2.zero);
                    vh.AddVert(new Vector3(left + 1, bottom), color, Vector2.zero);
                    vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
                }
            }
        }
    }
}
