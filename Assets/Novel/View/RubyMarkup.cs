#nullable enable
using System.Globalization;

namespace Novel.View
{
    // <ruby=よみ>親</ruby> を TMP リッチテキストの座標トリックで「よみを親文字の上に重ねる」文字列へ展開する。
    // 親文字より先によみを描き、退避量だけ x を戻して親文字を描く（よみ・親とも文字なので Reveal の可視数に算入される）。
    public static class RubyMarkup
    {
        private const float RubyScale = 0.5f;        // よみの相対サイズ（親文字に対する割合）
        private const string RubyPercent = "50";     // size=% 表記
        private const string RubyRaiseEm = "0.9";    // 持ち上げ量（em）

        // CJK は概ね 1em/字として中央寄せのオフセットを近似する（長いよみは左寄せに丸める）。よみ・親は noparse で包む。
        public static string BuildOverlay(string baseText, string reading)
        {
            if (string.IsNullOrEmpty(reading)) return "<noparse>" + baseText + "</noparse>";

            var ci = CultureInfo.InvariantCulture;
            float baseWidth = baseText.Length;            // 親文字幅（em 近似）
            float rubyWidth = reading.Length * RubyScale; // よみ幅（em 近似）
            var offset = (baseWidth - rubyWidth) / 2f;
            if (offset < 0f) offset = 0f;
            var back = offset + rubyWidth;

            return $"<space={offset.ToString("0.###", ci)}em><voffset={RubyRaiseEm}em><size={RubyPercent}%><noparse>{reading}</noparse></size></voffset><space=-{back.ToString("0.###", ci)}em><noparse>{baseText}</noparse>";
        }
    }
}
