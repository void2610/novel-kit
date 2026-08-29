#nullable enable
using System.Collections.Generic;

namespace Novel.Editor
{
    /// <summary>
    /// プロジェクトリファレンスに opt-in アセンブリ (CinematicEffect 等) がタブを足すための口。
    /// 組込タブ (キャラ / 画像 / 構図 / BGM・SE) の後ろに登録順で並ぶ。
    /// </summary>
    public interface IProjectReferenceSection
    {
        string Title { get; }

        /// <summary>タブ見出しに出す件数 (スキャン済みでなければここで走査してよい)。</summary>
        int Count { get; }

        void Draw(ProjectReferenceWindow.Rows rows);

        /// <summary>ウィンドウの「更新」やアセット変更で呼ばれる。スキャン結果のキャッシュを捨てる。</summary>
        void Invalidate();
    }

    public static class ProjectReferenceSections
    {
        private static readonly List<IProjectReferenceSection> Registered = new();

        public static IReadOnlyList<IProjectReferenceSection> All => Registered;

        public static void Register(IProjectReferenceSection section)
        {
            if (!Registered.Contains(section)) Registered.Add(section);
        }
    }
}
