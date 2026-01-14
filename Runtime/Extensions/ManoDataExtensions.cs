using UnityEngine;

namespace Mano
{
    public static class ManoDataExtensions
    {
        public static Vector2 ParseVector2(string val)
        {
            var p = val.Split(',');
            return p.Length >= 2 ? new Vector2(float.Parse(p[0]), float.Parse(p[1])) : Vector2.zero;
        }

        public static Vector3 ParseVector3(string val)
        {
            var p = val.Split(',');
            return p.Length >= 3 ? new Vector3(float.Parse(p[0]), float.Parse(p[1]), float.Parse(p[2])) : Vector3.zero;
        }

        public static Color ParseColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color col)) return col;
            return Color.white;
        }
    }
}