#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Commands;
using VitalRouter;

namespace Novel.Runtime
{
    // ノベル専用 Router にマップされる DI 市民。ハンドラ await で進行が成立する（Fiber サスペンション）
    [Routes]
    public partial class NovelCommandHandler
    {
        private readonly INovelView _view;
        private readonly IStateStore _state;
        private readonly ITextResolver _text;
        private readonly ICharacterCatalog _catalog;

        public NovelCommandHandler(INovelView view, IStateStore state, ITextResolver text, ICharacterCatalog catalog)
        {
            _view = view;
            _state = state;
            _text = text;
            _catalog = catalog;
        }

        public async UniTask On(SayCommand cmd, CancellationToken ct)
        {
            var text = _text.Resolve(cmd.Text);
            var displayName = ResolveDisplayName(cmd);
            var textId = StableId.Of(cmd.SpeakerId, text);
            var alreadyRead = _state.IsRead(textId);

            await _view.ShowMessageAsync(new NovelLine(cmd.SpeakerId, displayName, text, alreadyRead), ct);

            _state.MarkRead(textId);
        }

        // command-schema の解決 3 規則: 空=ナレーション / カタログ有=表示名（DisplayAs で上書き）/ 未登録=id をそのまま
        private string? ResolveDisplayName(SayCommand cmd)
        {
            if (string.IsNullOrEmpty(cmd.SpeakerId)) return null;
            if (cmd.DisplayAs is { } overrideName) return overrideName;
            if (_catalog.TryGet(cmd.SpeakerId, out var entry)) return entry.DisplayName;
            return cmd.SpeakerId;
        }
    }
}
