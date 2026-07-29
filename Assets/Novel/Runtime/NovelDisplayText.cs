#nullable enable
using System.Collections.Generic;
using System.Text;

namespace Novel.Runtime
{
    // TextRevealEngine.Build の可視文字数と TMP の characterCount が一致する表示文字列構築規則をここで一元化する
    public static class NovelDisplayText
    {
        public static string Build(IReadOnlyList<NovelToken> tokens)
        {
            var sb = new StringBuilder();
            string? pendingRuby = null;   // <ruby=よみ> 受領後、直後の Text（親文字）に重ねるためのよみ

            // 親文字なしで ruby 区間が終わっても、engine は RubyPush 時点でよみを可視数に算入済みのため素テキストとして出力する
            void FlushPendingRuby()
            {
                if (string.IsNullOrEmpty(pendingRuby)) return;
                sb.Append("<noparse>").Append(pendingRuby).Append("</noparse>");
                pendingRuby = null;
            }

            foreach (var t in tokens)
            {
                switch (t.Kind)
                {
                    case NovelTokenKind.RubyPush:
                        FlushPendingRuby();
                        pendingRuby = t.Payload;
                        break;
                    case NovelTokenKind.RubyPop:
                        FlushPendingRuby();
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
            FlushPendingRuby();
            return sb.ToString();
        }
    }
}
