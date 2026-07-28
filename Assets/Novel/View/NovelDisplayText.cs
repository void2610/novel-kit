#nullable enable
using System.Collections.Generic;
using System.Text;
using Novel.Runtime;

namespace Novel.View
{
    // TextRevealEngine.Build の可視文字数と TMP の characterCount が一致する表示文字列構築規則をここで一元化する
    public static class NovelDisplayText
    {
        public static string Build(IReadOnlyList<NovelToken> tokens)
        {
            var sb = new StringBuilder();
            string? pendingRuby = null;   // <ruby=よみ> 受領後、直後の Text（親文字）に重ねるためのよみ
            foreach (var t in tokens)
            {
                switch (t.Kind)
                {
                    case NovelTokenKind.RubyPush:
                        pendingRuby = t.Payload;
                        break;
                    case NovelTokenKind.RubyPop:
                        pendingRuby = null;
                        break;
                    case NovelTokenKind.Text when pendingRuby != null:
                        sb.Append(RubyMarkup.BuildOverlay(t.Payload, pendingRuby));   // よみを親文字の上に重ねる
                        pendingRuby = null;
                        break;
                    case NovelTokenKind.Text:
                        sb.Append("<noparse>").Append(t.Payload).Append("</noparse>");
                        break;
                    case NovelTokenKind.TmpTag:
                        sb.Append(t.Payload);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
