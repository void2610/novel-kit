#nullable enable
namespace Novel.Runtime
{
    // フラグ/変数/既読を単一に統合した状態モデル。choose() のユニークキー割当は Runtime 内部で行う
    public interface IStateStore
    {
        // フラグ/変数: 単一の int 名前空間。未設定キーは 0 とみなす
        int Get(string key);
        void Set(string key, int value);
        void Unset(string key);
        bool Has(string key);

        // 既読（テキストの StableId 単位）
        bool IsRead(string textId);
        void MarkRead(string textId);
    }
}
