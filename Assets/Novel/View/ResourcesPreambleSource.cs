#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using Novel.Runtime;
using UnityEngine;

namespace Novel.View
{
    // ライブラリ同梱 preamble.rb のコンパイル済み .mrb を Resources から読む既定実装
    public sealed class ResourcesPreambleSource : IPreambleSource
    {
        private readonly string _path;

        public ResourcesPreambleSource(string path = "Novel/Preamble") => _path = path;

        public UniTask<byte[]?> LoadPreambleAsync(CancellationToken ct)
        {
            foreach (var a in Resources.LoadAll<TextAsset>(_path))
                if (a.name.EndsWith(".mrb", System.StringComparison.Ordinal))
                    return UniTask.FromResult<byte[]?>(a.bytes);
            return UniTask.FromResult<byte[]?>(null);
        }
    }
}
