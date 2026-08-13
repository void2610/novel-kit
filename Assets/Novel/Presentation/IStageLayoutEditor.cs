#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Novel.Assets
{
    /// <summary>
    /// 構図の座標を読み書きする任意の口 (stage-preview ADR)。
    /// <see cref="IPortraitChannel"/> の実装が併せて実装すると、Stage Preview が座標編集と取り込みを出す。
    /// 未実装でもプレビューは動く (表示の確認だけになる)。
    ///
    /// 座標の保存先は game ごとに違うため novel-kit は場所を知らない。読み書きだけを実装へ委ねる。
    /// </summary>
    public interface IStageLayoutEditor
    {
        /// <summary>構図に保存されている slot 座標 (未定義の構図なら空)。</summary>
        IReadOnlyList<Vector2> GetLayoutPositions(string layoutId);

        /// <summary>いま画面に出ている slot の座標。シーンで動かした結果を構図へ取り込むのに使う。</summary>
        IReadOnlyList<Vector2> GetCurrentSlotPositions();

        /// <summary>構図の slot 座標を書き換える。呼び出し側が Undo と dirty 化を行う。</summary>
        void SetLayoutPositions(string layoutId, IReadOnlyList<Vector2> positions);
    }
}
