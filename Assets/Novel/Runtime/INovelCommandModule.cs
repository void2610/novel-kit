#nullable enable
using System;
using MRubyCS;
using VitalRouter;

namespace Novel.Runtime
{
    // game が独自コマンドを差し込む拡張口。語彙束縛（Ruby 名→C# コマンド型）とハンドラ写像を 1 クラスに束ねる。
    // 実装は [Routes] を併せ持ち、MapHandlers で生成済みの MapTo(router) をそのまま返すのが定石。
    // Ruby 糖衣（def my_cmd; cmd :my_cmd, ... end）は別 .rb を IPreambleSource として追加登録して供給する。
    public interface INovelCommandModule
    {
        // 組込語彙の後に呼ばれる。state.AddCommand<MyCommand>("my_cmd") で Ruby 名と C# コマンド型を束ねる。
        void RegisterVocabulary(MRubyState state);

        // ノベル専用 Router へ [Routes] ハンドラを写像し、その購読を返す（runner が Dispose 時に解除する）。
        IDisposable MapHandlers(ICommandSubscribable router);
    }
}
