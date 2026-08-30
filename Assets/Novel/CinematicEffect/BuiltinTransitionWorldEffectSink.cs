#nullable enable
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using Novel.Runtime;
using UnityEngine;
using Void2610.CinematicEffect;

namespace Novel.Cinematic
{
    /// <summary>
    /// preamble の標準 5 種 (shake / flash / fade_out / fade_in / blackout) を CinematicEffect で実装した
    /// <see cref="IWorldEffectSink"/>。尺・強度を引数で受けるためアセットではなくコードで組む。
    /// ゲーム固有の world_effect を持つ game は自前 sink を後勝ち登録し、標準 5 種は
    /// <see cref="TryBuild"/> で組んで委譲すればよい。
    /// </summary>
    public sealed class BuiltinTransitionWorldEffectSink : IWorldEffectSink
    {
        private readonly CinematicEffectDirector _director;
        private readonly NovelPlaybackProgress _progress;
        private readonly INovelErrorHandler? _errorHandler;

        public BuiltinTransitionWorldEffectSink(CinematicEffectDirector director, NovelPlaybackProgress progress,
            INovelErrorHandler? errorHandler = null)
        {
            _director = director;
            _progress = progress;
            _errorHandler = errorHandler;
        }

        public async UniTask DispatchAsync(IWorldEffect effect, CancellationToken ct)
        {
            if (effect is not WorldEffect we) return;
            var sequence = TryBuild(we);
            if (sequence == null)
            {
                NovelDiagnostics.EffectNotFound(_errorHandler, _progress.ScenarioKey, we.Key,
                    "world_effect の標準キーは shake / flash / fade_out / fade_in / blackout です。アセット化した演出は cinematic :key で呼びます。");
                return;
            }
            await _director.RunAsync(sequence, ct);
        }

        /// <summary>標準 5 種を組む。該当しないキーは null。fade 系の色は <see cref="WorldEffect.Color"/> (既定は黒)。</summary>
        public static CinematicSequence? TryBuild(WorldEffect we)
        {
            var color = ParseColor(we.Color);
            switch (we.Key)
            {
                case "shake":
                {
                    var intensity = Mathf.Max(0f, we.Arg(0, 1f));
                    var config = new CameraShakeConfig(0.3f * intensity, 0.5f + 0.1f * Mathf.Max(0f, intensity - 1f), false);
                    return CinematicSequence.Create().PlayAndAwait<CameraShakeEffect>(config);
                }
                case "flash":
                {
                    var total = we.Arg(0, 0.3f);
                    var hold = Mathf.Max(0f, total - 0.05f - 0.2f);
                    return CinematicSequence.Create()
                        .PlayAndAwait<ImageFlashEffect>(new ImageFlashConfig(Color.white, 0.05f, 0.2f, hold, Ease.OutQuad));
                }
                case "fade_out":
                {
                    var duration = we.Arg(0, 1f);
                    return CinematicSequence.Create()
                        .Play<ScreenFadeEffect>(new ScreenFadeConfig(color, duration, 1f, 0f, Ease.InOutSine, false))
                        .Delay(duration);
                }
                case "fade_in":
                    return CinematicSequence.Create()
                        .Stop<ScreenFadeEffect>(new ScreenFadeConfig(color, 1f, we.Arg(0, 1f), 0f, Ease.InOutSine, false));
                case "blackout":
                {
                    var hold = Mathf.Max(0f, we.Arg(0, 0.5f));
                    return CinematicSequence.Create()
                        .PlayAndAwait<ScreenFadeEffect>(new ScreenFadeConfig(color, 0.15f, 0.4f, hold, Ease.InOutSine, true));
                }
                default:
                    return null;
            }
        }

        // 色名 (white / red 等) と #rrggbb を受ける。解釈できない指定は黙って黒に倒さず、そのまま黒で再生しつつ作家には Validate 側で気付かせたいが、
        // 現状 world_effect は Validate の対象外のため既定色へフォールバックする
        private static Color ParseColor(string color)
            => !string.IsNullOrEmpty(color) && ColorUtility.TryParseHtmlString(color, out var parsed) ? parsed : Color.black;
    }
}
